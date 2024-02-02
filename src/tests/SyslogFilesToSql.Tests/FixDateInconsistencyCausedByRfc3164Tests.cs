using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NUnit.Framework.Internal;
using SyslogDecode.Model;
using SyslogDecode.Parsing;
using SyslogFilesToSql.Components.SqlImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SyslogFilesToSql.Tests
{
    [TestFixture]
    public class FixDateInconsistencyCausedByRfc3164
    {
        [Test]
        public void ServerIsInAdvance()
        {
            const string input = @"<30>1 2023-12-31T23:59:55+01:00 host1 app 47362 - - app[47362]: [47362:3] info: some message
<30>1 2024-12-31T23:59:57+01:00 host1 app 47362 - - app[47362]: [47362:3] info: some message
<30>1 2024-12-31T23:59:57+01:00 host1 app 47362 - - app[47362]: [47362:3] info: some message
<30>1 2024-01-01T00:00:00+01:00 host1 app 47362 - - app[47362]: [47362:2] info: some message
";

            using var syslogStream = new StringReader(input);
            IObservable<RawSyslogMessage> rawSyslogMessageProvider = SyslogFilesToSql.Helpers.Helpers.GetRawSyslogMessageProvider(syslogStream);

            var logger = Helpers.CreateNUnitLogger<FixDateInconsistencyCausedByRfc3164>();
            var writtenRows = new SqlCopy();
            var sqlCopyObserver = new SqlCopyObserver(writtenRows, Array.Empty<byte>(), new HashSet<string>(0), logger);
            var syslogParserObserver = new SyslogStreamParser(threadCount: 1);
            
            Debug.WriteLine("subscribing from sqlCopyObserver to syslogParserObserver");
            syslogParserObserver.Subscribe(sqlCopyObserver);
            Debug.WriteLine("subscribing from syslogParserObserver to rawSyslogMessageProvider");
            rawSyslogMessageProvider.Subscribe(syslogParserObserver);
            try
            {
                Debug.WriteLine("starting syslogParserObserver");
                syslogParserObserver.Start();
                syslogParserObserver.OnCompleted();
            }
            finally
            {
                syslogParserObserver.Unsubscribe(sqlCopyObserver);
                syslogParserObserver.Stop();
            }
            Assert.That(
                writtenRows.Rows.All(t => t.createdOn.Date == new DateTime(2023,12,31, 0, 0, 0, DateTimeKind.Unspecified) || t.createdOn.Date == new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Unspecified)),
                $"Found inconsistent dates: {Environment.NewLine}{string.Join(Environment.NewLine, writtenRows.Rows.Select(t => t.createdOn))}");

            logger.LogDebug($"{string.Join(Environment.NewLine, writtenRows.Rows.Select(t => $"{t.createdOn}: {t.msg}"))}");
        }

        [Test]
        public void ServerIsLate()
        {
            const string input = @"<30>1 2023-12-31T23:59:55+01:00 host1 app 47362 - - app[47362]: [47362:3] info: some message
<30>1 2023-01-01T00:00:00+01:00 host1 app 47362 - - app[47362]: [47362:3] info: some message
<30>1 2023-01-01T00:00:00+01:00 host1 app 47362 - - app[47362]: [47362:3] info: some message
<30>1 2024-01-01T00:00:00+01:00 host1 app 47362 - - app[47362]: [47362:2] info: some message
";

            using var syslogStream = new StringReader(input);
            IObservable<RawSyslogMessage> syslogProvider = SyslogFilesToSql.Helpers.Helpers.GetRawSyslogMessageProvider(syslogStream);

            var logger = Helpers.CreateNUnitLogger<FixDateInconsistencyCausedByRfc3164>();
            var writtenRows = new SqlCopy();
            var observer = new SqlCopyObserver(writtenRows, Array.Empty<byte>(), new HashSet<string>(0), logger);
            var syslogParserObserver = new SyslogStreamParser(threadCount: 1);
            syslogParserObserver.Subscribe(observer);
            syslogProvider.Subscribe(syslogParserObserver);
            try
            {
                syslogParserObserver.Start();
                syslogParserObserver.OnCompleted();
            }
            finally
            {
                syslogParserObserver.Unsubscribe(observer);
                syslogParserObserver.Stop();
            }

            Assert.That(
                writtenRows.Rows.All(t => t.createdOn.Date == new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Unspecified) || t.createdOn.Date == new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Unspecified)), 
                $"Found inconsistent dates: {Environment.NewLine}{string.Join(Environment.NewLine, writtenRows.Rows.Select(t => t.createdOn))}");

            logger.LogDebug($"{string.Join(Environment.NewLine, writtenRows.Rows.Select(t => $"{t.createdOn}: {t.msg}"))}");
        }
    }
}