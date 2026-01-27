// SSOT: skills/devian-common/12-feature-logger/SKILL.md

using System;

namespace Devian
{
    /// <summary>
    /// Log level for filtering and categorization.
    /// </summary>
    public enum LogLevel
    {
        Debug = 10,
        Info = 20,
        Warn = 30,
        Error = 40,
    }

    /// <summary>
    /// Interface for log output sinks.
    /// Implementations can write to console, file, network, etc.
    /// </summary>
    public interface ILogSink
    {
        void Write(LogLevel level, string message);
    }

    /// <summary>
    /// Default console log sink.
    /// Format: [{LEVEL}] {message}
    /// </summary>
    public sealed class ConsoleLogSink : ILogSink
    {
        public void Write(LogLevel level, string message)
        {
            var levelStr = level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO",
                LogLevel.Warn => "WARN",
                LogLevel.Error => "ERROR",
                _ => "UNKNOWN",
            };

            Console.WriteLine($"[{levelStr}] {message}");
        }
    }

    /// <summary>
    /// Global static logger with level filtering and sink replacement.
    /// </summary>
    public static class Log
    {
        private static LogLevel _level = LogLevel.Debug;
        private static ILogSink _sink = new ConsoleLogSink();
        private static readonly object _lock = new();

        // Configuration

        public static void SetLevel(LogLevel level)
        {
            lock (_lock)
            {
                _level = level;
            }
        }

        public static LogLevel GetLevel()
        {
            lock (_lock)
            {
                return _level;
            }
        }

        public static void SetSink(ILogSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            lock (_lock)
            {
                _sink = sink;
            }
        }

        public static ILogSink GetSink()
        {
            lock (_lock)
            {
                return _sink;
            }
        }

        // Output

        public static void Debug(string message)
        {
            Output(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Output(LogLevel.Info, message);
        }

        public static void Warn(string message)
        {
            Output(LogLevel.Warn, message);
        }

        public static void Error(string message)
        {
            Output(LogLevel.Error, message);
        }

        private static void Output(LogLevel level, string message)
        {
            ILogSink sink;
            LogLevel currentLevel;

            lock (_lock)
            {
                currentLevel = _level;
                sink = _sink;
            }

            if (level < currentLevel) return;

            sink.Write(level, message);
        }
    }
}
