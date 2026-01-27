// Unity Shared - Main Thread Dispatcher
// SSOT: skills/devian-unity/10-unity-main-thread/SKILL.md
// NOTE: 이 파일은 Generated 폴더 산출물이 아닌 고정 유틸(수기 유지)이며,
//       정본은 upm 경로다. Packages는 복사본.

using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Log item for main thread dispatch queue.
    /// </summary>
    internal readonly struct LogItem
    {
        public readonly LogLevel Level;
        public readonly string Message;

        public LogItem(LogLevel level, string message)
        {
            Level = level;
            Message = message;
        }
    }

    /// <summary>
    /// Main thread dispatcher for logging.
    /// Queues log items from background threads and processes them on the main thread.
    /// </summary>
    internal sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        /// <summary>
        /// Maximum number of log items to process per frame.
        /// Prevents frame drops from log flooding.
        /// </summary>
        private const int MaxPerFrame = 500;

        private static UnityMainThreadDispatcher? _instance;
        private static readonly ConcurrentQueue<LogItem> _queue = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("[UnityMainThreadDispatcher]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
        }

        /// <summary>
        /// Enqueues a log item for main thread processing.
        /// Thread-safe. Can be called from any thread.
        /// </summary>
        public static void Enqueue(LogItem item)
        {
            _queue.Enqueue(item);
        }

        private void Update()
        {
            int processed = 0;
            while (processed < MaxPerFrame && _queue.TryDequeue(out var item))
            {
                OutputLog(item);
                processed++;
            }
        }

        private static void OutputLog(LogItem item)
        {
            var formatted = $"[{GetLevelString(item.Level)}] {item.Message}";

            switch (item.Level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(formatted);
                    break;

                case LogLevel.Warn:
                    Debug.LogWarning(formatted);
                    break;

                case LogLevel.Error:
                    Debug.LogError(formatted);
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

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
