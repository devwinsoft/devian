using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    public enum BuildLogLevel
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public struct BuildLogEntry
    {
        public DateTime Timestamp;
        public BuildLogLevel Level;
        public string Message;

        public override string ToString()
        {
            var prefix = Level == BuildLogLevel.Info ? "INFO"
                : Level == BuildLogLevel.Warning ? "WARN"
                : "ERROR";
            return $"[{Timestamp:HH:mm:ss}] [{prefix}] {Message}";
        }
    }

    /// <summary>
    /// 빌드 자동화 파이프라인의 중앙 로그 시스템.
    /// 모든 Phase 스크립트가 이 Logger를 통해 로그를 남기고,
    /// EditorWindow가 OnLogAdded 이벤트를 구독하여 실시간 표시한다.
    /// 백그라운드 스레드에서 로그가 추가되면 메인 스레드에서 이벤트를 발생시킨다.
    ///
    /// Domain Reload 대응:
    /// Unity BuildPipeline.BuildPlayer() 완료 후 도메인 리로드가 발생하면
    /// static 필드가 초기화된다. SessionState에 JSON으로 직렬화하여
    /// 리로드 후에도 로그를 복원한다.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildAutomationLogger
    {
        private static readonly List<BuildLogEntry> _entries = new List<BuildLogEntry>();

        /// <summary>메인 스레드 ID (OnLogAdded를 메인 스레드에서만 발생시키기 위해)</summary>
        private static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>백그라운드 스레드에서 추가된 엔트리를 메인 스레드에서 처리하기 위한 큐</summary>
        private static readonly Queue<BuildLogEntry> _pendingEntries = new Queue<BuildLogEntry>();
        private static readonly object _lock = new object();

        private const string SessionStateKey = "BuildAutomationLogger_Entries";

        public static IReadOnlyList<BuildLogEntry> Entries => _entries;
        public static int Count => _entries.Count;

        /// <summary>
        /// GUI가 구독하여 Repaint 트리거로 사용한다.
        /// 항상 메인 스레드에서 호출된다.
        /// </summary>
        public static event Action<BuildLogEntry> OnLogAdded;

        /// <summary>
        /// 정적 생성자 — 도메인 리로드 시 SessionState에서 로그 복원.
        /// [InitializeOnLoad]에 의해 호출된다.
        /// </summary>
        static BuildAutomationLogger()
        {
            RestoreFromSession();
        }

        public static void Log(string message)
        {
            AddEntry(BuildLogLevel.Info, message);
        }

        public static void LogWarning(string message)
        {
            AddEntry(BuildLogLevel.Warning, message);
        }

        public static void LogError(string message)
        {
            AddEntry(BuildLogLevel.Error, message);
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _pendingEntries.Clear();
            }
            SaveToSession();
        }

        /// <summary>
        /// 전체 로그를 하나의 문자열로 반환한다. Copy All 기능에 사용.
        /// </summary>
        public static string GetAllText()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                sb.AppendLine(_entries[i].ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 외부 프로세스의 stdout/stderr을 실시간 로그로 전달한다.
        /// Process.Start() 호출 후 이 메서드를 호출한다.
        /// </summary>
        public static void StreamProcess(Process process)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Log(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    LogWarning(e.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        /// <summary>
        /// 메인 스레드에서 호출하여, 백그라운드 스레드에서 큐에 쌓인 로그 엔트리를
        /// _entries 리스트로 옮긴다.
        /// EditorApplication.update에서 호출한다.
        /// </summary>
        public static void FlushPendingEvents()
        {
            bool flushed = false;
            lock (_lock)
            {
                while (_pendingEntries.Count > 0)
                {
                    var entry = _pendingEntries.Dequeue();
                    _entries.Add(entry);
                    flushed = true;
                }
            }
            if (flushed)
                SaveToSession();
        }

        private static void AddEntry(BuildLogLevel level, string message)
        {
            var entry = new BuildLogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            // 메인 스레드: 즉시 _entries에 추가 + SessionState 저장
            // 백그라운드 스레드: 큐에 추가 (FlushPendingEvents에서 _entries로 이동)
            if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                lock (_lock)
                {
                    _entries.Add(entry);
                }
                SaveToSession();
            }
            else
            {
                lock (_lock)
                {
                    _pendingEntries.Enqueue(entry);
                }
            }
        }

        // ── SessionState 직렬화 ──────────────────────────────

        [Serializable]
        private struct SerializedLog
        {
            public List<SerializedEntry> entries;
        }

        [Serializable]
        private struct SerializedEntry
        {
            public string ts;   // ISO 8601
            public int lv;      // BuildLogLevel int
            public string msg;
        }

        private static void SaveToSession()
        {
            try
            {
                var data = new SerializedLog
                {
                    entries = new List<SerializedEntry>(_entries.Count)
                };

                lock (_lock)
                {
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        data.entries.Add(new SerializedEntry
                        {
                            ts = _entries[i].Timestamp.ToString("o"),
                            lv = (int)_entries[i].Level,
                            msg = _entries[i].Message
                        });
                    }
                }

                var json = JsonUtility.ToJson(data);
                SessionState.SetString(SessionStateKey, json);
            }
            catch
            {
                // SessionState 저장 실패는 무시 — 로그 손실보다 안전
            }
        }

        private static void RestoreFromSession()
        {
            try
            {
                var json = SessionState.GetString(SessionStateKey, "");
                if (string.IsNullOrEmpty(json))
                    return;

                var data = JsonUtility.FromJson<SerializedLog>(json);
                if (data.entries == null)
                    return;

                lock (_lock)
                {
                    for (int i = 0; i < data.entries.Count; i++)
                    {
                        var se = data.entries[i];
                        _entries.Add(new BuildLogEntry
                        {
                            Timestamp = DateTime.TryParse(se.ts, out var dt)
                                ? dt : DateTime.Now,
                            Level = (BuildLogLevel)se.lv,
                            Message = se.msg
                        });
                    }
                }
            }
            catch
            {
                // 복원 실패 시 빈 로그로 시작
            }
        }
    }
}
