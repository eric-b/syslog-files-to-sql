using Microsoft.Extensions.Logging;
using System;

namespace SyslogFilesToSql.Tests
{
    public static class Helpers
    {
        public static ILogger<T> CreateEmptyLogger<T>() => new EmptyLogger<T>();

        public static ILogger<T> CreateNUnitLogger<T>()
        {
            var logger = new NUnitLogger<T>();
            return logger;
        }

        sealed class NUnitLogger<T> : ILogger<T>, IDisposable
        {
            private readonly Action<string> output = Console.WriteLine;

            public void Dispose()
            {
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => output(formatter(state, exception));

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;
        }

        sealed class EmptyLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {

            }
        }
    }
}
