using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using SyslogDecode.Parsing;
using SyslogDecode.Model;
using SyslogFilesToSql.Npgsql.Datalayer;
using System.Security.Cryptography;
using System.Collections.Generic;
using SyslogFilesToSql.Npgsql.Datalayer.Model;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Data.Common;

namespace SyslogFilesToSql.Components.SqlImport
{
    internal sealed class SyslogSqlImporter : IObserver<FileInfo>, IDisposable
    {
        private readonly ILogger _logger;

        private IDisposable? _unsubscribe;

        private readonly Db _db;
        private readonly BlockingCollection<FileInfo> _queue;

        private Task? _bgTask;
        private readonly CancellationTokenSource _bgTaskCts;

        private readonly FileHashGenerator _hashGenerator = new FileHashGenerator();

        private readonly HashSet<string> _filesProcessed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _hostOrAppToExclude;
        private readonly short _maxDaysToKeep;
        private readonly bool _enableCompressionAfterImport;

        private int _totalProcessedFileCount = 0;

        record class ProcessedFile(string path, byte[] hash);

        sealed class FileHashGenerator
        {
            private readonly SHA1 _sha1 = SHA1.Create();
            private const int HashBufferLength = 32*1024;
            private readonly byte[] _hashBuffer = new byte[HashBufferLength];

            public async Task<byte[]> ComputeHash(FileInfo file, CancellationToken cancellationToken)
            {
                _sha1.Initialize();
                using (FileStream stream = file.OpenRead())
                {
                    int bytesInBuffer = await stream.ReadAsync(_hashBuffer, 0, HashBufferLength, cancellationToken);
                    if (bytesInBuffer == 0)
                        throw new InvalidOperationException("File is empty.");

                    return _sha1.ComputeHash(_hashBuffer, 0, bytesInBuffer);
                }
            }
        }

        public SyslogSqlImporter(IOptions<SyslogSqlImporterOptions> options, Db db, ILogger<SyslogSqlImporter> logger)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            options.Value.Validate();
            _hostOrAppToExclude = options.Value.HostsOrAppsToExclude;
            _maxDaysToKeep = options.Value.MaxDaysToKeep;
            _enableCompressionAfterImport = options.Value.CompressAfterImport;
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queue = new BlockingCollection<FileInfo>();
            _bgTaskCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            try
            {
                _bgTaskCts.Cancel();
            }
            catch (ObjectDisposedException)
            {

            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex}");
            }
            _unsubscribe?.Dispose();
            _bgTaskCts.Dispose();
        }

        /// <summary>
        /// Subscribe to files pushed by <paramref name="producer"/>.
        /// </summary>
        /// <param name="producer"></param>
        public void Subscribe(IObservable<FileInfo> producer)
        {
            if (_unsubscribe != null)
                throw new InvalidOperationException($"Method {nameof(Subscribe)} can be called only once.");
            
            _unsubscribe = producer.Subscribe(this);

            _bgTask = Task.Run(QueueBackgroundTask, _bgTaskCts.Token);
        }

        async Task QueueBackgroundTask()
        {
            try
            {
                while (!_bgTaskCts.IsCancellationRequested)
                {
                    // Wait for first input
                    int errorCount = 0;
                    _logger.LogInformation("Waiting for next files to import...");
                    FileInfo? file = _queue.Take(_bgTaskCts.Token);
                    _logger.LogInformation("There are new file(s) to import...");

                    using (var tracingActivity = Tracing.OpenTelemetry.ActivitySource.StartActivity("ProcessingBatch"))
                    {
                        // Delete any stale data from intermediate table
                        try
                        {
                            await _db.TruncateSyslogImportTable(_bgTaskCts.Token);
                        }
                        catch (Exception ex)
                        {
                            // Consume all queue to avoid repeated errors.
                            // We may have a transient issue.
                            Tracing.OpenTelemetry.SetError(tracingActivity, ex);
                            _logger.LogError(ex, "Error while preparing for a new import in database. Clearing queue.");
                            while (_queue.TryTake(out _)) { };
                            continue;
                        }

                        var importedFiles = new List<FileInfo>();
                        ProcessResult result = await ProcessFile(file, _bgTaskCts.Token);
                        if (result == ProcessResult.Success)
                        {
                            importedFiles.Add(file);
                        }
                        else if (result == ProcessResult.AlreadyProcessed)
                        {
                            if (_enableCompressionAfterImport)
                            {
                                await TryCompressFile(file, _bgTaskCts.Token);
                            }
                        }
                        else if (result == ProcessResult.SqlFailure)
                        {
                            continue;
                        }

                        int remainingQueueCount = _queue.Count;
                        while (remainingQueueCount > 0 && !_bgTaskCts.IsCancellationRequested)
                        {
                            if (_queue.TryTake(out file))
                            {
                                remainingQueueCount--;
                                result = await ProcessFile(file, _bgTaskCts.Token);
                                if (result == ProcessResult.Success)
                                {
                                    importedFiles.Add(file);
                                }
                                else if (result == ProcessResult.AlreadyProcessed)
                                {
                                    if (_enableCompressionAfterImport)
                                    {
                                        await TryCompressFile(file, _bgTaskCts.Token);
                                    }
                                }
                                else if (result == ProcessResult.SqlFailure)
                                {
                                    if (++errorCount > 5)
                                    {
                                        // Consume all queue to avoid repeated errors.
                                        // Db may have a transient issue.
                                        const string errorMessage = "Too many SQL errors: clearing queue.";
                                        Tracing.OpenTelemetry.SetError(tracingActivity, errorMessage);
                                        _logger.LogWarning(errorMessage);
                                        while (_queue.TryTake(out _)) { };
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (importedFiles.Count != 0 && !_bgTaskCts.IsCancellationRequested)
                        {
                            _logger.LogInformation($"Completing import of {importedFiles.Count} file(s)...");
                            try
                            {
                                using (var childTracingActivity = Tracing.OpenTelemetry.ActivitySource.StartActivity(nameof(_db.CompleteSyslogImport)))
                                {
                                    childTracingActivity?.SetTag("file.count", importedFiles.Count);
                                    await _db.CompleteSyslogImport(_maxDaysToKeep, _bgTaskCts.Token);
                                }
                                _logger.LogInformation($"{importedFiles.Count} file(s) imported.");

                                if (_enableCompressionAfterImport)
                                {
                                    using (Tracing.OpenTelemetry.ActivitySource.StartActivity("CompressingFiles"))
                                    {
                                        foreach (FileInfo item in importedFiles)
                                        {
                                            await CompressFile(item, _bgTaskCts.Token);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Tracing.OpenTelemetry.SetError(tracingActivity, ex);
                                _logger.LogError(ex, $"Error while completing import of {importedFiles.Count} file(s).");
                            }
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error while processing background queue.");
            }
        }

        private async Task TryCompressFile(FileInfo file, CancellationToken cancellationToken)
        {
            try
            {
                await CompressFile(file, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to compress file: {file}.");
            }
        }

        async Task CompressFile(FileInfo file, CancellationToken cancellationToken)
        {
            string compressedFile = file.FullName + ".gz";
            if (File.Exists(compressedFile))
            {
                file.Delete();
                return;
            }

            using (var tracingActivity = Tracing.OpenTelemetry.ActivitySource.StartActivity(nameof(CompressFile)))
            {
                tracingActivity?.SetTag("filepath", compressedFile);
                _logger.LogDebug($"Compressing {file.Name}...");
                using (FileStream inputStream = file.OpenRead())
                using (FileStream outputStream = File.Create(compressedFile))
                using (var gzip = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    await inputStream.CopyToAsync(gzip, cancellationToken);
                    gzip.Close();
                }
            }
            
            file.Delete();
            _logger.LogInformation($"File compressed: {Path.GetFileName(compressedFile)}.");
        }

        enum ProcessResult
        {
            None,
            GenericFailure,
            SqlFailure,
            Success,
            AlreadyProcessed
        }

        async Task<ProcessResult> ProcessFile(FileInfo file, CancellationToken cancellationToken)
        {
            using var tracingActivity = Tracing.OpenTelemetry.ActivitySource.StartActivity(nameof(ProcessFile));
            tracingActivity?.SetTag("filepath", file.FullName);
            try
            {
                _logger.LogDebug($"Processing {file.Name} (#{_totalProcessedFileCount})...");
                file.Refresh();
                if (!file.Exists || file.Length == 0)
                {
                    _logger.LogWarning($"File {file.Name} cannot be processed.");
                    Tracing.OpenTelemetry.SetError(tracingActivity, "File cannot be processed.");
                    return ProcessResult.GenericFailure;
                }

                byte[] fileHash = await _hashGenerator.ComputeHash(file, cancellationToken);

                if (_filesProcessed.Count == 0)
                {
                    SyslogFileImported[] alreadyProcessedFiles = await _db.GetFilesImported(100, cancellationToken);
                    foreach (SyslogFileImported item in alreadyProcessedFiles)
                    {
                        _filesProcessed.Add(item.FilePath);
                    }
                }

                if (_filesProcessed.Contains(file.FullName))
                {
                    _logger.LogDebug($"File {file.Name} already processed.");
                    return ProcessResult.AlreadyProcessed;
                }

                using StreamReader syslogStream = file.OpenText();
                IObservable<RawSyslogMessage> rawSyslogMessageProvider = Helpers.Helpers.GetRawSyslogMessageProvider(syslogStream);

                int rowCount;
                using (global::Npgsql.NpgsqlConnection cx = await _db.OpenConnection(cancellationToken))
                {
                    using (global::Npgsql.NpgsqlBinaryImporter writer = await _db.InitializeSyslogImport(cx, cancellationToken))
                    {
                        var sqlCopy = new NpgsqlBinarySqlCopy(writer);
                        var sqlCopyObserver = new SqlCopyObserver(sqlCopy, fileHash, _hostOrAppToExclude, _logger);
                        var syslogParserObserver = new SyslogStreamParser(threadCount: 1);
                        syslogParserObserver.Subscribe(sqlCopyObserver);
                        rawSyslogMessageProvider.Subscribe(syslogParserObserver);
                        try
                        {
                            syslogParserObserver.Start();

                            // Apparently there is a synchronization issue between SyslogStreamParser.Start(), IsIdle and OnCompleted(). State probably is not fully reliable, we workaround this.
                            // Else, OnCompleted() may return before actually starting.
                            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250)))
                            {
                                try
                                {
                                    while (syslogParserObserver.IsIdle)
                                    {
                                        await Task.Delay(10, cts.Token).ConfigureAwait(false);
                                    }
                                }
                                catch (TaskCanceledException)
                                { }
                            }

                            syslogParserObserver.OnCompleted();
                        }
                        finally
                        {   
                            syslogParserObserver.Unsubscribe(sqlCopyObserver);
                            syslogParserObserver.Stop();
                        }
                        rowCount = sqlCopyObserver.RowCount;
                        await writer.CompleteAsync(cancellationToken);
                    }
                    await _db.AddFileImported(cx, file.FullName, fileHash, cancellationToken);
                }
                
                _filesProcessed.Add(file.FullName);
                if (rowCount != 0)
                {
                    _logger.LogDebug($"File {file.Name} ({rowCount} rows) copied to intermediate table.");
                }
                else
                {
                    _logger.LogWarning($"File {file.Name} could not be processed: 0 line parsed but size is {file.Length} bytes.");
                }
                _totalProcessedFileCount++;
                return ProcessResult.Success;
            }
            catch (DbException ex)
            {
                Tracing.OpenTelemetry.SetError(tracingActivity, ex);
                _logger.LogError(ex, $"SQL error while processing file: {file.Name}");
                return ProcessResult.SqlFailure;
            }
            catch (Exception ex)
            {
                Tracing.OpenTelemetry.SetError(tracingActivity, ex);
                _logger.LogError(ex, $"Error while processing file: {file.Name}");
                return ProcessResult.GenericFailure;
            }
        }

        void IObserver<FileInfo>.OnCompleted()
        {
        }

        void IObserver<FileInfo>.OnError(Exception error)
        {
            _logger.LogError(error, "Error raised by the provider.");
        }

        void IObserver<FileInfo>.OnNext(FileInfo value)
        {
            _queue.Add(value);
        }
    }
}
