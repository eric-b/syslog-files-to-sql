using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyslogFilesToSql.Components.SqlImport;
using SyslogFilesToSql.Components.SqlMigration;
using SyslogFilesToSql.Components.SyslogFiles;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyslogFilesToSql.Components
{
    /// <summary>
    /// Primary component run by this program.
    /// </summary>
    internal class SyslogFilesToSqlBgService : BackgroundService
    {
        private readonly SqlMigrationRunner _migrationRunner;
        private readonly SyslogFilesWatcher _watcher;
        private readonly SyslogSqlImporter _importer;
        private readonly ILogger _logger;

        public SyslogFilesToSqlBgService(SqlMigrationRunner migrationRunner,
                                         SyslogFilesWatcher watcher,
                                         SyslogSqlImporter importer,
                                         ILogger<SyslogFilesToSqlBgService> logger)
        {
            _migrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner));
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _importer.Subscribe(_watcher);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _migrationRunner.Run();
            _logger.LogInformation("Host started");
            _watcher.Start();
            
            try 
            { 
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) 
            {
            }

            _logger.LogInformation("Host stopped");
        }

        public override void Dispose()
        {
            _importer.Dispose();
            _watcher.Dispose();
            base.Dispose();
        }
    }
}
