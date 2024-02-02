using System;
using System.IO;

namespace SyslogFilesToSql.Components.SyslogFiles
{
    internal class SyslogFilesWatcherOptions
    {
        /// <summary>
        /// Directory of Syslog files to watch.
        /// </summary>
        public string? SyslogDirectory { get; set; }

        /// <summary>
        /// <para>File globbing pattern for Syslog files.
        /// Default value is "syslog_*".
        /// </para>
        /// <para>See pattern formats here: https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing#pattern-formats
        /// </para>
        /// </summary>
        public string SyslogFilePattern { get; set; } = "syslog_*";

        public void Validate()
        {
            if (string.IsNullOrEmpty(SyslogDirectory))
            {
                throw new ArgumentException($"{nameof(SyslogDirectory)} must be set.");
            }

            if (!Directory.Exists(SyslogDirectory))
            {
                throw new ArgumentException($"{nameof(SyslogDirectory)} is set with a path that does not exist or is unreachable: {SyslogDirectory}.");
            }

            if (string.IsNullOrEmpty(SyslogFilePattern))
            {
                throw new ArgumentException($"{nameof(SyslogFilePattern)} must be set.");
            }
        }
    }
}
