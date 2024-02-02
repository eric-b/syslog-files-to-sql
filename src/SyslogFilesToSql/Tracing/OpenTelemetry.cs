using System;
using System.Diagnostics;

namespace SyslogFilesToSql.Tracing
{
    static class OpenTelemetry
    {
        public const string ActivitySourceName = "SyslogFilesToSql";

        public static readonly ActivitySource ActivitySource = CreateActivitySource();

        private static ActivitySource CreateActivitySource()
        {
            return new ActivitySource(
                ActivitySourceName, 
                version: typeof(Program).Assembly.GetName().Version?.ToString());
        }

        public static void SetError(Activity? activity, Exception error)
        {
            if (activity != null)
            {
                activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", error.GetType().Name },
                    { "exception.message", error.Message }
                }));
                activity.SetTag("status", "ERROR");
            }
        }

        public static void SetError(Activity? activity, string errorMessage)
        {
            if (activity != null)
            {
                activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.message", errorMessage }
                }));
                activity.SetTag("status", "ERROR");
            }
        }
    }
}
