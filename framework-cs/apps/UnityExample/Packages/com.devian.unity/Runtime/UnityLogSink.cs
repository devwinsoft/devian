// SSOT: skills/devian-unity/20-packages/com.devian.unity/SKILL.md

using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Unity-specific log sink that outputs to Unity Console.
    /// Supports multi-threaded logging by dispatching to main thread when called from background threads.
    /// Use Logger.SetSink(new UnityLogSink()) to enable Unity console logging.
    /// </summary>
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string tag, string message, Exception? ex = null)
        {
            // Main thread: output directly
            if (UnityMainThread.IsMainThread)
            {
                OutputLogDirect(level, tag, message, ex);
                return;
            }

            // Background thread: enqueue for main thread dispatch
            // Convert exception to string here to avoid cross-thread issues
            string? exceptionText = ex?.ToString();
            UnityMainThreadDispatcher.Enqueue(new LogItem(level, tag, message, exceptionText));
        }

        /// <summary>
        /// Direct output on main thread. Called when already on main thread.
        /// </summary>
        private static void OutputLogDirect(LogLevel level, string tag, string message, Exception? ex)
        {
            var formatted = $"[{GetLevelString(level)}] {tag} - {message}";

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(formatted);
                    break;

                case LogLevel.Warn:
                    Debug.LogWarning(formatted);
                    break;

                case LogLevel.Error:
                    if (ex != null)
                    {
                        Debug.LogError(formatted + "\n" + ex.ToString());
                    }
                    else
                    {
                        Debug.LogError(formatted);
                    }
                    break;

                default:
                    Debug.Log(formatted);
                    break;
            }
        }

        private static string GetLevelString(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO",
                LogLevel.Warn => "WARN",
                LogLevel.Error => "ERROR",
                _ => "UNKNOWN",
            };
        }
    }
}
