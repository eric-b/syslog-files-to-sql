using FluentMigrator;
using System;

namespace SyslogFilesToSql.Migrations.Migrations._001
{
    [Migration(1, "Initial schema")]
    public sealed class Baseline : Migration
    {
        public override void Up()
        {
            Execute.EmbeddedScript("Resources._001.Tables.syslog_file_imported.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_msg_import.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_app.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_facility.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_host.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_payload_type.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_severity.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_msg.sql");
            Execute.EmbeddedScript("Resources._001.Tables.syslog_msg_stat.sql"); 

            Execute.EmbeddedScript("Resources._001.Views.v_syslog_msg.sql");
            Execute.EmbeddedScript("Resources._001.Views.v_syslog_msg_stat.sql"); 

            Execute.EmbeddedScript("Resources._001.Functions._update_syslog_partitions.sql");

            Execute.EmbeddedScript("Resources._001.Procedures.complete_syslog_msg_import.sql");
        }

        public override void Down()
        {
            throw new NotSupportedException("Rollback is not supported.");
        }
    }
}
