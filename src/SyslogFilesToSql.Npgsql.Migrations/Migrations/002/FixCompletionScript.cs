using FluentMigrator;
using System;

namespace SyslogFilesToSql.Migrations.Migrations._002
{
    [Migration(2, "Fix a minor issue with completion procesure")]
    public sealed class Baseline : Migration
    {
        public override void Up()
        {
            Execute.EmbeddedScript("Resources._002.Procedures.complete_syslog_msg_import.sql");
        }

        public override void Down()
        {
            throw new NotSupportedException("Rollback is not supported.");
        }
    }
}
