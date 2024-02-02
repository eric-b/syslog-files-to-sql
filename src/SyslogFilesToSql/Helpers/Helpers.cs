using SyslogDecode.Model;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace SyslogFilesToSql.Helpers
{
    internal static class Helpers
    {
        public static IObservable<RawSyslogMessage> GetRawSyslogMessageProvider(TextReader stream)
        {
            return Observable.Using(
                () => stream,
                // Needs explicit Scheduler.CurrentThread.
                // Else IObservable<RawSyslogMessage>.Subscribe may block. Related to https://github.com/dotnet/reactive/issues/259
                // An alternative could be TaskPoolScheduler.Default to force defer execution on another thread than the caller thread.
                // All schedulers are documented here: https://introtorx.com/chapters/scheduling-and-threading
                reader => Observable.FromAsync(reader.ReadLineAsync, Scheduler.CurrentThread)
                                    .Repeat()
                                    .TakeWhile(line => line != null)
                                    .Select(line =>
                                    {
                                        return new RawSyslogMessage { Message = line };
                                    }));
        }
    }
}
