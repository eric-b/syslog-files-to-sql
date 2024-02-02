using Microsoft.Extensions.Logging;
using SyslogDecode.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SyslogFilesToSql.Components.SqlImport
{
    internal sealed class SqlCopyObserver(ISqlCopy sqlCopy,
                                    byte[] fileHash,
                                    HashSet<string> hostOrAppToExclude,
                                    ILogger logger) : IObserver<ParsedSyslogMessage>
    {
        private bool _isInErrorState;
        private DateTimeOffset _previousRowDate;

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {
            _isInErrorState = true;
            Tracing.OpenTelemetry.SetError(Activity.Current, error);
            logger.LogError(error, "Error raised by syslog parser.");
        }

        public void OnNext(ParsedSyslogMessage value)
        {
            if (_isInErrorState)
                return;

            if (hostOrAppToExclude.Contains(value.Header.HostName) ||
                !string.IsNullOrEmpty(value.Header.AppName) && hostOrAppToExclude.Contains(value.Header.AppName))
            {
                return;
            }

            if (value.Header.Timestamp is null)
            {
                logger.LogWarning($"Message without timestamp ignored: {value.Source.Message}");
                return;
            }

            int intValue;
            int? pid = null;
            int? msgId = null;
            if (int.TryParse(value.Header.ProcId, out intValue))
            {
                pid = intValue;
            }
            if (int.TryParse(value.Header.MsgId, out intValue))
            {
                msgId = intValue;
            }

            DateTimeOffset dateTimeOffset = FixDateInconsistencyCausedByRfc3164(ref _previousRowDate,
                                                                      value.Header.Timestamp.Value,
                                                                      value.Source.Message,
                                                                      logger);
            // DateTimeKind.Unspecified: our syslog db expects a DateTime without offset.
            var datetimeUnspecified = new DateTime(dateTimeOffset.ToUniversalTime().Ticks, DateTimeKind.Unspecified);
            sqlCopy.Write(new SqlCopyRow(fileHash,
                                         value.Facility.ToString(),
                                         value.Severity.ToString(),
                                         datetimeUnspecified,
                                         value.Header.HostName,
                                         value.Header.AppName,
                                         pid,
                                         msgId,
                                         value.Message,
                                         value.PayloadType.ToString()));
        }

        /// <summary>
        /// Bug on year may happen when a syslog client of kind RFC 3164
        /// sends messages to a syslog server of kind RFC 5424:
        /// There is no year in RFC 3164 timestamp.
        /// When timestamp is written by server conforming to RFC 5424 format with year,
        /// it includes the "current year". 
        /// When year change on 1st of january at midnight, if the two
        /// systems are not fully synced (and they are never really fully synced),
        /// messages during the difference interval can have an inconsistent date.
        /// For example a syslog server with new year in advance can
        /// write a message with date 2024-12-31 instead of 2023-12-31.
        /// A syslog server with new year late can write a message with 
        /// date 2023-01-01 instead of 2024-01-01.
        /// </summary>
        /// <param name="previousDateTime"></param>
        /// <param name="currentDateTime"></param>
        /// <param name="originalMessageForTraces"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static DateTimeOffset FixDateInconsistencyCausedByRfc3164(ref DateTimeOffset previousDateTime,
                                                                    DateTimeOffset currentDateTime,
                                                                    string originalMessageForTraces,
                                                                    ILogger logger)
        {
            if (previousDateTime == default)
            {
                previousDateTime = currentDateTime;
            }
            else if (previousDateTime.Year < currentDateTime.Year &&
                     previousDateTime.Month == currentDateTime.Month)
            {
                //
                // "2023-12-31T23:59:55+01:00"
                // "2024-12-31T23:59:57+01:00"
                //
                logger.LogWarning($"Changed an inconsistent date/time: {originalMessageForTraces}");
                currentDateTime = new DateTimeOffset(previousDateTime.Year,
                                                   currentDateTime.Month,
                                                   currentDateTime.Day,
                                                   currentDateTime.Hour,
                                                   currentDateTime.Minute,
                                                   currentDateTime.Second,
                                                   currentDateTime.Offset);
            }
            else if (previousDateTime.Month == 12 && currentDateTime.Month == 1 &&
                     previousDateTime.Year == currentDateTime.Year)
            {
                //
                // "2023-12-31T23:59:55+01:00"
                // "2023-01-01T00:00:00+01:00"
                //
                // Note this block depends on a modified version of SyslogDecode
                // which tries to keep original timestamp time zone. Else, year may be unexpected
                // due to timezone changed to UTC ("2023-01-01T00:00:00+01:00" -> 2022-12-31T23:00:00 UTC).
                logger.LogWarning($"Changed an inconsistent date/time: {originalMessageForTraces}");
                currentDateTime = new DateTimeOffset(currentDateTime.Year + 1,
                                                   currentDateTime.Month,
                                                   currentDateTime.Day,
                                                   currentDateTime.Hour,
                                                   currentDateTime.Minute,
                                                   currentDateTime.Second,
                                                   currentDateTime.Offset);
            }
            else
            {
                previousDateTime = currentDateTime;
            }

            return currentDateTime;
        }
    }
}
