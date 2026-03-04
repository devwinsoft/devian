#nullable enable
using System;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// ownerKey + msgKey 기반 메시지/트리거 시스템.
    /// 시간 기반 이벤트는 지원하지 않음 (Unity Invoke/Coroutine 사용 권장).
    /// Re-entrancy safe: 핸들러 내부에서 Subcribe/UnSubcribe/ClearAll 호출 가능.
    /// </summary>
    public class MessageSystem<TOwnerKey, TMsgKey>
        where TOwnerKey : IEquatable<TOwnerKey>, IComparable<TOwnerKey>
        where TMsgKey : unmanaged, Enum
    {
        /// <summary>
        /// 메시지 핸들러. true 반환 시 자동 해제됨.
        /// </summary>
        public delegate bool Handler(object[] args);

        private struct Entry
        {
            public TMsgKey Key;
            public Handler Handler;
        }

        private struct PendingRegister
        {
            public TOwnerKey OwnerKey;
            public TMsgKey Key;
            public Handler Handler;
        }

        private struct PendingRemove
        {
            public TOwnerKey OwnerKey;
            public int EntryIndex;
        }

        // Main storage
        private readonly Dictionary<TOwnerKey, List<Entry>> _byInstanceId = new();
        private readonly EqualityComparer<TMsgKey> _keyComparer = EqualityComparer<TMsgKey>.Default;

        // Re-entrancy tracking
        private int _notifyDepth;
        private bool _pendingClearAll;

        // Pending operations (deferred during Notify)
        private readonly List<TOwnerKey> _pendingUnregisterOwnerKeys = new();
        private readonly List<PendingRegister> _pendingRegisters = new();
        private readonly List<PendingRemove> _pendingRemoves = new();

        // Snapshot buffer for safe iteration
        private readonly List<TOwnerKey> _snapshotOwnerKeys = new();

        /// <summary>
        /// 모든 등록을 초기화한다.
        /// </summary>
        public void ClearAll()
        {
            if (_notifyDepth > 0)
            {
                _pendingClearAll = true;
                return;
            }

            _byInstanceId.Clear();
        }

        /// <summary>
        /// ownerKey에 key 핸들러를 등록한다.
        /// </summary>
        public void Subcribe(TOwnerKey ownerKey, TMsgKey key, Handler handler)
        {
            if (_notifyDepth > 0)
            {
                _pendingRegisters.Add(new PendingRegister
                {
                    OwnerKey = ownerKey,
                    Key = key,
                    Handler = handler
                });
                return;
            }

            registerImmediate(ownerKey, key, handler);
        }

        /// <summary>
        /// ownerKey에 1회성 핸들러를 등록한다. 호출 후 자동 해제된다.
        /// </summary>
        public void SubcribeOnce(TOwnerKey ownerKey, TMsgKey key, Action<object[]> handler)
        {
            Subcribe(ownerKey, key, args =>
            {
                handler(args);
                return true; // 자동 해제
            });
        }

        /// <summary>
        /// ownerKey의 모든 핸들러를 해제한다.
        /// </summary>
        public void UnSubcribe(TOwnerKey ownerKey)
        {
            if (_notifyDepth > 0)
            {
                _pendingUnregisterOwnerKeys.Add(ownerKey);
                return;
            }

            _byInstanceId.Remove(ownerKey);
        }

        /// <summary>
        /// key에 매칭되는 모든 핸들러를 호출한다.
        /// 핸들러가 true를 반환하면 자동 해제된다.
        /// </summary>
        public void Notify(TMsgKey key, params object[] args)
        {
            _notifyDepth++;

            try
            {
                // Snapshot ownerKeys (safe: no handler execution yet)
                _snapshotOwnerKeys.Clear();
                foreach (var kvp in _byInstanceId)
                {
                    _snapshotOwnerKeys.Add(kvp.Key);
                }

                // Iterate over snapshot
                for (int s = 0; s < _snapshotOwnerKeys.Count; s++)
                {
                    TOwnerKey ownerKey = _snapshotOwnerKeys[s];

                    if (!_byInstanceId.TryGetValue(ownerKey, out var list))
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
                                OwnerKey = ownerKey,
                                EntryIndex = i
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

        private void registerImmediate(TOwnerKey ownerKey, TMsgKey key, Handler handler)
        {
            if (!_byInstanceId.TryGetValue(ownerKey, out var list))
            {
                list = new List<Entry>();
                _byInstanceId[ownerKey] = list;
            }

            list.Add(new Entry { Key = key, Handler = handler });
        }

        private void flushPending()
        {
            // 1. ClearAll takes priority
            if (_pendingClearAll)
            {
                _byInstanceId.Clear();
                _pendingClearAll = false;
                _pendingUnregisterOwnerKeys.Clear();
                _pendingRemoves.Clear();
                _pendingRegisters.Clear();
                return;
            }

            // 2. Process pending unregisters (remove entire ownerKey)
            if (_pendingUnregisterOwnerKeys.Count > 0)
            {
                // Deduplicate
                var unregisterSet = new HashSet<TOwnerKey>(_pendingUnregisterOwnerKeys);
                foreach (var ownerKey in unregisterSet)
                {
                    _byInstanceId.Remove(ownerKey);
                }
                _pendingUnregisterOwnerKeys.Clear();

                // Remove pending removes for unregistered ownerKeys
                _pendingRemoves.RemoveAll(r => unregisterSet.Contains(r.OwnerKey));
            }

            // 3. Process pending removes (handler returned true)
            if (_pendingRemoves.Count > 0)
            {
                // Group by ownerKey and sort indices descending
                var grouped = new Dictionary<TOwnerKey, List<int>>();
                foreach (var r in _pendingRemoves)
                {
                    if (!grouped.TryGetValue(r.OwnerKey, out var indices))
                    {
                        indices = new List<int>();
                        grouped[r.OwnerKey] = indices;
                    }
                    indices.Add(r.EntryIndex);
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
                    registerImmediate(p.OwnerKey, p.Key, p.Handler);
                }
                _pendingRegisters.Clear();
            }
        }
    }
}
