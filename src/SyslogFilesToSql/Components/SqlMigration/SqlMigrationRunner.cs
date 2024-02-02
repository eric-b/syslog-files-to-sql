using FluentMigrator.Runner;
using Microsoft.Extensions.Logging;
using System;

namespace SyslogFilesToSql.Components.SqlMigration
{
    /// <summary>
    /// SQL Migrations (based on Fluent Migrator in-process Runner).
    /// </summary>
    internal sealed class SqlMigrationRunner
    {
        private readonly IMigrationRunner _migrationRunner;

        private readonly ILogger _logger;

        public SqlMigrationRunner(IMigrationRunner migrationRunner,
                                  ILogger<SqlMigrationRunner> logger)
        {
            _migrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run()
        {
            _logger.LogInformation("Applying SQL migrations if any...");
            _migrationRunner.MigrateUp();
            _logger.LogInformation("All SQL migrations have been applied.");
        }
    }
}
