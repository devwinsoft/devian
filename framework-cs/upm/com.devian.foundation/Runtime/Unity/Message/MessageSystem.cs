#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// instanceId + key 기반 메시지/트리거 시스템.
    /// 시간 기반 이벤트는 지원하지 않음 (Unity Invoke/Coroutine 사용 권장).
    /// Re-entrancy safe: 핸들러 내부에서 Register/Unregister/ClearAll 호출 가능.
    /// </summary>
    public static class MessageSystem<TKey> where TKey : unmanaged, Enum
    {
        /// <summary>
        /// 메시지 핸들러. true 반환 시 자동 해제됨.
        /// </summary>
        public delegate bool Handler(object[] args);

        private struct Entry
        {
            public TKey Key;
            public Handler Handler;
        }

        private struct PendingRegister
        {
            public int InstanceId;
            public TKey Key;
            public Handler Handler;
        }

        private struct PendingRemove
        {
            public int InstanceId;
            public int Index;
        }

        // Main storage
        private static readonly Dictionary<int, List<Entry>> _byInstanceId = new();
        private static readonly EqualityComparer<TKey> _keyComparer = EqualityComparer<TKey>.Default;

        // Re-entrancy tracking
        private static int _notifyDepth;
        private static bool _pendingClearAll;

        // Pending operations (deferred during Notify)
        private static readonly List<int> _pendingUnregisterInstanceIds = new();
        private static readonly List<PendingRegister> _pendingRegisters = new();
        private static readonly List<PendingRemove> _pendingRemoves = new();

        // Snapshot buffer for safe iteration
        private static readonly List<int> _snapshotInstanceIds = new();

        /// <summary>
        /// 모든 등록을 초기화한다.
        /// </summary>
        public static void ClearAll()
        {
            if (_notifyDepth > 0)
            {
                _pendingClearAll = true;
                return;
            }

            _byInstanceId.Clear();
        }

        /// <summary>
        /// instanceId에 key 핸들러를 등록한다.
        /// </summary>
        public static void Register(int instanceId, TKey key, Handler handler)
        {
            if (_notifyDepth > 0)
            {
                _pendingRegisters.Add(new PendingRegister
                {
                    InstanceId = instanceId,
                    Key = key,
                    Handler = handler
                });
                return;
            }

            registerImmediate(instanceId, key, handler);
        }

        /// <summary>
        /// UnityEngine.Object owner에 key 핸들러를 등록한다.
        /// owner가 null이면 무시한다.
        /// </summary>
        public static void Register(UnityEngine.Object owner, TKey key, Handler handler)
        {
            if (owner == null) return;
            Register(owner.GetInstanceID(), key, handler);
        }

        /// <summary>
        /// instanceId에 1회성 핸들러를 등록한다. 호출 후 자동 해제된다.
        /// </summary>
        public static void RegisterOnce(int instanceId, TKey key, Action<object[]> handler)
        {
            Register(instanceId, key, args =>
            {
                handler(args);
                return true; // 자동 해제
            });
        }

        /// <summary>
        /// UnityEngine.Object owner에 1회성 핸들러를 등록한다. 호출 후 자동 해제된다.
        /// owner가 null이면 무시한다.
        /// </summary>
        public static void RegisterOnce(UnityEngine.Object owner, TKey key, Action<object[]> handler)
        {
            if (owner == null) return;
            RegisterOnce(owner.GetInstanceID(), key, handler);
        }

        /// <summary>
        /// instanceId의 모든 핸들러를 해제한다.
        /// </summary>
        public static void Unregister(int instanceId)
        {
            if (_notifyDepth > 0)
            {
                _pendingUnregisterInstanceIds.Add(instanceId);
                return;
            }

            _byInstanceId.Remove(instanceId);
        }

        /// <summary>
        /// UnityEngine.Object owner의 모든 핸들러를 해제한다.
        /// owner가 null이면 무시한다.
        /// </summary>
        public static void Unregister(UnityEngine.Object owner)
        {
            if (owner == null) return;
            Unregister(owner.GetInstanceID());
        }

        /// <summary>
        /// key에 매칭되는 모든 핸들러를 호출한다.
        /// 핸들러가 true를 반환하면 자동 해제된다.
        /// </summary>
        public static void Notify(TKey key, params object[] args)
        {
            _notifyDepth++;

            try
            {
                // Snapshot instanceId keys (safe: no handler execution yet)
                _snapshotInstanceIds.Clear();
                foreach (var kvp in _byInstanceId)
                {
                    _snapshotInstanceIds.Add(kvp.Key);
                }

                // Iterate over snapshot
                for (int s = 0; s < _snapshotInstanceIds.Count; s++)
                {
                    int instanceId = _snapshotInstanceIds[s];

                    if (!_byInstanceId.TryGetValue(instanceId, out var list))
                        continue;

                    // Reverse 순회 (제거 인덱스 안전)
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var entry = list[i];

                        if (!_keyComparer.Equals(entry.Key, key))
                            continue;

                        bool shouldRemove = entry.Handler(args);

                        if (shouldRemove)
                        {
                            // Pending remove (deferred)
                            _pendingRemoves.Add(new PendingRemove
                            {
                                InstanceId = instanceId,
                                Index = i
                            });
                        }
                    }
                }
            }
            finally
            {
                _notifyDepth--;

                if (_notifyDepth == 0)
                {
                    flushPending();
                }
            }
        }

        // ========================================
        // Private helpers
        // ========================================

        private static void registerImmediate(int instanceId, TKey key, Handler handler)
        {
            if (!_byInstanceId.TryGetValue(instanceId, out var list))
            {
                list = new List<Entry>();
                _byInstanceId[instanceId] = list;
            }

            list.Add(new Entry { Key = key, Handler = handler });
        }

        private static void flushPending()
        {
            // 1. ClearAll takes priority
            if (_pendingClearAll)
            {
                _byInstanceId.Clear();
                _pendingClearAll = false;
                _pendingUnregisterInstanceIds.Clear();
                _pendingRemoves.Clear();
                _pendingRegisters.Clear();
                return;
            }

            // 2. Process pending unregisters (remove entire instanceId)
            if (_pendingUnregisterInstanceIds.Count > 0)
            {
                // Deduplicate
                var unregisterSet = new HashSet<int>(_pendingUnregisterInstanceIds);
                foreach (var instanceId in unregisterSet)
                {
                    _byInstanceId.Remove(instanceId);
                }
                _pendingUnregisterInstanceIds.Clear();

                // Remove pending removes for unregistered instanceIds
                _pendingRemoves.RemoveAll(r => unregisterSet.Contains(r.InstanceId));
            }

            // 3. Process pending removes (handler returned true)
            if (_pendingRemoves.Count > 0)
            {
                // Group by instanceId and sort indices descending
                var grouped = new Dictionary<int, List<int>>();
                foreach (var r in _pendingRemoves)
                {
                    if (!grouped.TryGetValue(r.InstanceId, out var indices))
                    {
                        indices = new List<int>();
                        grouped[r.InstanceId] = indices;
                    }
                    indices.Add(r.Index);
                }

                foreach (var kvp in grouped)
                {
                    if (!_byInstanceId.TryGetValue(kvp.Key, out var list))
                        continue;

                    // Sort descending to safely remove from back to front
                    kvp.Value.Sort((a, b) => b.CompareTo(a));

                    foreach (var index in kvp.Value)
                    {
                        if (index >= 0 && index < list.Count)
                        {
                            list.RemoveAt(index);
                        }
                    }

                    // Remove empty list
                    if (list.Count == 0)
                    {
                        _byInstanceId.Remove(kvp.Key);
                    }
                }

                _pendingRemoves.Clear();
            }

            // 4. Process pending registers
            if (_pendingRegisters.Count > 0)
            {
                foreach (var p in _pendingRegisters)
                {
                    registerImmediate(p.InstanceId, p.Key, p.Handler);
                }
                _pendingRegisters.Clear();
            }
        }
    }
}
