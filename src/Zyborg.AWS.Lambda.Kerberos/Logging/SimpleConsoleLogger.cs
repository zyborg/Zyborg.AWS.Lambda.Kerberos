using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("Zyborg.AWS.Lambda.Kerberos.Logging.ConsoleTest")]

namespace Zyborg.AWS.Lambda.Kerberos.Logging
{
    internal class SimpleConsoleLogger : ILogger
    {
        public static LogLevel MinLogLevel { get; set; } = LogLevel.Information;

        public SimpleConsoleLogger(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new SimpleConsoleLoggerScope<TState>(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= MinLogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var message = formatter(state, exception);
                if (!string.IsNullOrEmpty(message) || exception != null)
                {
                    var logBuilder = new StringBuilder();
                    logBuilder.Append(ToString(logLevel));
                    logBuilder.Append(Name)
                        .Append("[")
                        .Append(eventId.ToString())
                        .Append("]");

                    if (!string.IsNullOrEmpty(message))
                    {
                        logBuilder.AppendLine(message);
                    }
                    if (exception != null)
                    {
                        logBuilder.AppendLine(exception.ToString());
                    }

                    Console.Write(logBuilder.ToString());
                    Console.Out.Flush();
                }
            }
        }

        public static string ToString(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical: return "[crit]";
                case LogLevel.Error: return "[fail]";
                case LogLevel.Warning: return "[warn]";
                case LogLevel.Information: return "[info]";
                case LogLevel.Debug: return "[dbug]";
                case LogLevel.Trace: return "[trce]";
                case LogLevel.None: return "[NONE]";
            }
            return "[" + logLevel + "]";
        }
    }

    internal class SimpleConsoleLoggerScope<TState> : IDisposable
    {
        public SimpleConsoleLoggerScope(TState state)
        {
            State = state;
        }

        public TState State { get; private set; }

        public void Dispose()
        {
            // Does nothing
        }
    }
}
