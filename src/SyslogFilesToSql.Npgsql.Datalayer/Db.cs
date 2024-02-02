using Microsoft.Extensions.Options;
using Npgsql;
using SyslogFilesToSql.Npgsql.Datalayer.Model;
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using System.Collections.Generic;
using System.Linq;

namespace SyslogFilesToSql.Npgsql.Datalayer
{
    public class Db : IDisposable
    {
        private readonly NpgsqlDataSource _dataSource;

        static Db()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public Db(IOptions<DbOptions> options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _dataSource = NpgsqlDataSource.Create(options.Value.GetConnectionStringWithPassword());
        }

        public async Task<SyslogFileImported[]> GetFilesImported(int maxCount, CancellationToken cancellationToken)
        {
            using var cx = await _dataSource.OpenConnectionAsync(cancellationToken);
            IEnumerable<SyslogFileImported> rows = await cx.QueryAsync<SyslogFileImported>("SELECT id, file_hash, is_complete, file_path FROM public.syslog_file_imported WHERE is_complete ORDER BY ID DESC LIMIT @maxCount", new { maxCount });
            return rows.ToArray();
        }

        public Task AddFileImported(NpgsqlConnection cx, string filePath, byte[] fileHash, CancellationToken cancellationToken)
        {
            return cx.ExecuteAsync("INSERT INTO public.syslog_file_imported (file_hash, file_path) VALUES (@fileHash, @filePath) ON CONFLICT (file_hash) DO NOTHING",
                new
                {
                    fileHash,
                    filePath
                });
        }

        public async Task CompleteSyslogImport(short maxDaysToKeep, CancellationToken cancellationToken)
        {
            using var cx = await _dataSource.OpenConnectionAsync(cancellationToken);
            await cx.ExecuteAsync("CALL public.complete_syslog_msg_import (@maxDaysToKeep)",
                new
                {
                    maxDaysToKeep
                });
        }

        /// <summary>
        /// Open a new connection to database.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken) 
        {
            return _dataSource.OpenConnectionAsync(cancellationToken);
        }

        /// <summary>
        /// Returns a <see cref="NpgsqlBinaryImporter"/> ready to write a binary copy of syslog messages in intermediate table "syslog_msg_import".
        /// Caller must write rows then complete by calling <see cref="NpgsqlBinaryImporter.CompleteAsync(CancellationToken)"/>,
        /// then <see cref="NpgsqlBinaryImporter.Dispose"/>.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<NpgsqlBinaryImporter> InitializeSyslogImport(NpgsqlConnection cx, CancellationToken cancellationToken)
        {
            return cx.BeginBinaryImportAsync("COPY public.syslog_msg_import (file_hash, facility, severity, created_on, host, payload_type, app, pid, msg_id, msg) FROM STDIN (FORMAT BINARY)", cancellationToken);
        }

        /// <summary>
        /// Clears intermediate table "syslog_msg_import"
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task TruncateSyslogImportTable(CancellationToken cancellationToken)
        {
            using (var cmd = _dataSource.CreateCommand(@"
TRUNCATE TABLE public.syslog_msg_import;
DELETE FROM public.syslog_file_imported WHERE NOT is_complete;
"))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            _dataSource.Dispose();
        }
    }
}
