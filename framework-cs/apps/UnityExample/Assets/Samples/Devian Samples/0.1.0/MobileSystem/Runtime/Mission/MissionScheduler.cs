using System;
using System.Collections.Generic;
using System.Linq;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class MissionScheduler
    {
        const string Tag = nameof(MissionScheduler);
        const int MaxDailyRuntimeCount = 5;

        readonly MissionStorage _storage;
        readonly Action<int, MESSAGE_META_TYPE, BaseTrigger<int, MESSAGE_META_TYPE>.Handler> _subscribeTrigger;
        readonly Action<int> _unsubscribeTrigger;
        readonly Action<MissionRuntimeBase> _onInitialized;
        readonly Action<MissionRuntimeBase> _onChanged;
        readonly Action<MissionRuntimeBase> _onClaimable;
        readonly Func<string> _getCurrentDailyKey;
        readonly Func<int> _getCurrentDailyPeriodIndex;

        public MissionScheduler(
            MissionStorage storage,
            Action<int, MESSAGE_META_TYPE, BaseTrigger<int, MESSAGE_META_TYPE>.Handler> subscribeTrigger,
            Action<int> unsubscribeTrigger,
            Action<MissionRuntimeBase> onInitialized,
            Action<MissionRuntimeBase> onChanged,
            Action<MissionRuntimeBase> onClaimable,
            Func<string> getCurrentDailyKey,
            Func<int> getCurrentDailyPeriodIndex)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _subscribeTrigger = subscribeTrigger ?? throw new ArgumentNullException(nameof(subscribeTrigger));
            _unsubscribeTrigger = unsubscribeTrigger ?? throw new ArgumentNullException(nameof(unsubscribeTrigger));
            _onInitialized = onInitialized ?? throw new ArgumentNullException(nameof(onInitialized));
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _onClaimable = onClaimable ?? throw new ArgumentNullException(nameof(onClaimable));
            _getCurrentDailyKey = getCurrentDailyKey ?? throw new ArgumentNullException(nameof(getCurrentDailyKey));
            _getCurrentDailyPeriodIndex = getCurrentDailyPeriodIndex ?? throw new ArgumentNullException(nameof(getCurrentDailyPeriodIndex));
        }

        public void RebuildBindings()
        {
            DetachAll();
            ensureDailyRuntimes();
        }

        public bool HasDailyRuntimeOutsideCurrentPeriod()
        {
            var currentPeriodKey = _getCurrentDailyKey();
            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeDaily>())
            {
                if (!string.Equals(runtime.periodKey, currentPeriodKey, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public void DetachAll()
        {
            foreach (var runtime in _storage.runtimes.Values)
                runtime?.Detach();
        }

        public void ClearDailyScope()
        {
            var removeRuntimeKeys = new List<int>();
            foreach (var kv in _storage.runtimes)
            {
                if (kv.Value is not MissionRuntimeDaily runtime)
                    continue;

                runtime.Detach();
                removeRuntimeKeys.Add(kv.Key);
            }

            foreach (var key in removeRuntimeKeys)
                _storage.runtimes.Remove(key);
        }

        public void PruneExpiredState()
        {
            var currentDailyIndex = _getCurrentDailyPeriodIndex();
            var removeRuntimeKeys = new List<int>();
            foreach (var kv in _storage.runtimes)
            {
                if (kv.Value is not MissionRuntimeDaily runtime)
                    continue;

                if (!TryParseDailyPeriodIndex(runtime.periodKey, out var runtimeDailyIndex))
                    continue;

                if (runtimeDailyIndex >= currentDailyIndex - 1)
                    continue;

                runtime.Detach();
                removeRuntimeKeys.Add(kv.Key);
            }

            foreach (var key in removeRuntimeKeys)
                _storage.runtimes.Remove(key);
        }

        public MissionRuntimeDaily FindDaily(string missionId)
        {
            MissionRuntimeDaily found = null;
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime is not MissionRuntimeDaily dailyRuntime)
                    continue;

                if (!string.Equals(dailyRuntime.missionId, missionId, StringComparison.Ordinal))
                    continue;

                if (found != null)
                {
                    Debug.LogError($"[{Tag}] Duplicate daily runtime detected for missionId='{missionId}'.");
                    return found;
                }

                found = dailyRuntime;
            }

            return found;
        }

        public int AllocateMissionUid()
        {
            if (_storage.nextMissionUid <= 0)
                _storage.nextMissionUid = 1;

            var candidate = _storage.nextMissionUid;
            while (_storage.runtimes.ContainsKey(candidate))
                candidate++;

            _storage.nextMissionUid = candidate + 1;
            return candidate;
        }

        void ensureDailyRuntimes()
        {
            var periodKey = _getCurrentDailyKey();
            removeDailyRuntimesOutsidePeriod(periodKey);

            var selectedRows = selectDailyRows(periodKey);
            var selectedIds = new HashSet<string>(selectedRows.Select(row => row.MissionId), StringComparer.Ordinal);
            var initializedRuntimes = new List<MissionRuntimeDaily>();

            var removeKeys = new List<int>();
            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeDaily>())
            {
                if (!string.Equals(runtime.periodKey, periodKey, StringComparison.Ordinal))
                    continue;

                if (selectedIds.Contains(runtime.missionId))
                    continue;

                runtime.Detach();
                removeKeys.Add(runtime.missionUid);
            }

            foreach (var key in removeKeys)
                _storage.runtimes.Remove(key);

            foreach (var row in selectedRows)
            {
                if (!TryResolveMessage(row.ConditionMsgId, out var message))
                {
                    Debug.LogError($"[{Tag}] MESSAGE_META not found for daily mission: missionId='{row.MissionId}', messageId='{row.ConditionMsgId}'.");
                    continue;
                }

                var existing = FindDaily(row.MissionId);
                if (existing != null)
                {
                    var restored = MissionRuntimeFactory.Restore(new MissionRuntimeRestoreArgs
                    {
                        MissionId = existing.missionId,
                        MessageId = row.ConditionMsgId,
                        PeriodKey = existing.periodKey,
                        MissionUid = existing.missionUid,
                        ProgressValue = existing.progressValue,
                        IsCompleted = existing.isCompleted,
                        Index = existing.index,
                        StatType = message.MessageType,
                        OpType = message.SaveType,
                        ConditionOpType = row.ConditionOp,
                        ConditionValue = row.ConditionValue!.Value,
                        SubscribeTrigger = _subscribeTrigger,
                        UnsubscribeTrigger = _unsubscribeTrigger,
                        ReadExternalProgress = createExternalProgressReader(row.ConditionMsgId, message.SaveType),
                        OnChanged = _onChanged,
                        OnClaimable = _onClaimable,
                    });

                    _storage.runtimes[restored.missionUid] = restored;
                    initializedRuntimes.Add((MissionRuntimeDaily)restored);
                    continue;
                }

                var created = MissionRuntimeFactory.CreateDaily(new DailyMissionRuntimeCreateArgs
                {
                    MissionId = row.MissionId,
                    MessageId = row.ConditionMsgId,
                    PeriodKey = periodKey,
                    MissionUid = AllocateMissionUid(),
                    Index = 0,
                    StatType = message.MessageType,
                    OpType = message.SaveType,
                    ConditionOpType = row.ConditionOp,
                    ConditionValue = row.ConditionValue!.Value,
                    SubscribeTrigger = _subscribeTrigger,
                    UnsubscribeTrigger = _unsubscribeTrigger,
                    ReadExternalProgress = createExternalProgressReader(row.ConditionMsgId, message.SaveType),
                    OnChanged = _onChanged,
                    OnClaimable = _onClaimable,
                });

                _storage.runtimes[created.missionUid] = created;
                initializedRuntimes.Add(created);
            }

            assignDailyIndices(periodKey, selectedRows);

            foreach (var runtime in initializedRuntimes)
                _onInitialized(runtime);
        }

        void removeDailyRuntimesOutsidePeriod(string periodKey)
        {
            var removeKeys = new List<int>();
            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeDaily>())
            {
                if (string.Equals(runtime.periodKey, periodKey, StringComparison.Ordinal))
                    continue;

                runtime.Detach();
                removeKeys.Add(runtime.missionUid);
            }

            foreach (var key in removeKeys)
                _storage.runtimes.Remove(key);
        }

        List<MISSION> selectDailyRows(string periodKey)
        {
            var candidates = TB_MISSION.GetAll()
                .Where(isEligibleDailyRow)
                .ToList();

            var selected = new List<MISSION>(MaxDailyRuntimeCount);
            foreach (var row in candidates.Where(row => row.Fixed))
            {
                if (selected.Count >= MaxDailyRuntimeCount)
                {
                    Debug.LogError($"[{Tag}] Too many fixed daily missions. max={MaxDailyRuntimeCount}");
                    break;
                }

                selected.Add(row);
            }

            if (selected.Count >= MaxDailyRuntimeCount)
                return selected;

            var remaining = candidates.Where(row => !row.Fixed).ToList();
            var random = createDailySelectionRandom(periodKey);
            shuffleInPlace(remaining, random);

            foreach (var row in remaining)
            {
                if (selected.Count >= MaxDailyRuntimeCount)
                    break;

                selected.Add(row);
            }

            return selected;
        }

        void assignDailyIndices(string periodKey, IReadOnlyList<MISSION> selectedRows)
        {
            var rowByMissionId = new Dictionary<string, MISSION>(StringComparer.Ordinal);
            foreach (var row in selectedRows)
                rowByMissionId[row.MissionId] = row;

            var orderedRuntimes = _storage.runtimes.Values
                .OfType<MissionRuntimeDaily>()
                .Where(runtime => string.Equals(runtime.periodKey, periodKey, StringComparison.Ordinal))
                .Where(runtime => rowByMissionId.ContainsKey(runtime.missionId))
                .OrderBy(runtime => rowByMissionId[runtime.missionId].OrderNum)
                .ThenBy(runtime => runtime.missionId, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < orderedRuntimes.Count; i++)
                orderedRuntimes[i].index = i;
        }

        static bool isEligibleDailyRow(MISSION row)
        {
            return row != null
                   && row.IsActive
                   && row.ConditionValue.HasValue
                   && TryResolveMessage(row.ConditionMsgId, out var message)
                   && message.SaveType != MESSAGE_META_SAVE_TYPE.NONE;
        }

        static bool TryResolveMessage(string messageId, out MESSAGE_META message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            message = TB_MESSAGE_META.Get(messageId);
            return message != null;
        }

        static System.Random createDailySelectionRandom(string periodKey)
        {
            var periodIndex = 0;
            TryParseDailyPeriodIndex(periodKey, out periodIndex);
            return new System.Random(periodIndex);
        }

        static void shuffleInPlace<T>(IList<T> list, System.Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static bool TryParseDailyPeriodIndex(string periodKey, out int periodIndex)
        {
            periodIndex = 0;
            if (string.IsNullOrWhiteSpace(periodKey))
                return false;

            if (!periodKey.StartsWith("day:", StringComparison.Ordinal))
                return false;

            return int.TryParse(periodKey.Substring(4), out periodIndex);
        }

        static Func<CBigInt> createExternalProgressReader(string messageId, MESSAGE_META_SAVE_TYPE saveType)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return null;

            if (!GameMessageRule.IsTotalSaveType(saveType))
            {
                return null;
            }

            var key = messageId;
            return () =>
            {
                if (!MetaMessageManager.TryGet(out var messageManager) || messageManager == null)
                    return CBigInt.Zero;

                return messageManager.GetStat(key);
            };
        }
    }
}
