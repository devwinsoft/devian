// SSOT: skills/devian/21-unity-common-upm/SKILL.md

using System;
using UnityEngine;
using Devian.Module.Common;

namespace Devian.Unity.Common
{
    /// <summary>
    /// Unity-specific log sink that outputs to Unity Console.
    /// Use Logger.SetSink(new UnityLogSink()) to enable Unity console logging.
    /// </summary>
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string tag, string message, Exception? ex = null)
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
