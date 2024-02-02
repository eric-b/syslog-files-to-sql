using System;
using System.Collections.Generic;

namespace SyslogFilesToSql.Components.SqlImport
{
    sealed class SyslogSqlImporterOptions
    {
        /// <summary>
        /// Allows to exclude some applications or hosts (not imported).
        /// </summary>
        public HashSet<string> HostsOrAppsToExclude { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Default: 90 days (3 months).
        /// </summary>
        public short MaxDaysToKeep { get; set; } = 90;

        /// <summary>
        /// If enabled, files will be compressed after import (file name will
        /// have suffix ".gz").
        /// There is no automatic deletion implemented in this program.
        /// You typically need to run a cron job to delete these compressed files.
        /// Knowing they are compressed means they have been imported.
        /// </summary>
        public bool CompressAfterImport { get; set; }

        public void Validate()
        {
            if (MaxDaysToKeep < 1)
                throw new ArgumentException($"{nameof(MaxDaysToKeep)} must be greater than 0.");
        }
    }
}
