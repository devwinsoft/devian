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
        readonly Action<int, GAME_MESSAGE_TYPE, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler> _subscribeTrigger;
        readonly Action<int> _unsubscribeTrigger;
        readonly Action<MissionRuntimeBase> _onInitialized;
        readonly Action<MissionRuntimeBase> _onChanged;
        readonly Action<MissionRuntimeBase> _onClaimable;
        readonly Func<string> _getCurrentDailyKey;
        readonly Func<int> _getCurrentDailyPeriodIndex;
        readonly Func<string> _getCurrentPeriodKey;
        readonly Func<int> _getCurrentPeriodIndex;
        readonly Func<int> _getCurrentPeriodElapsedDay;

        public MissionScheduler(
            MissionStorage storage,
            Action<int, GAME_MESSAGE_TYPE, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler> subscribeTrigger,
            Action<int> unsubscribeTrigger,
            Action<MissionRuntimeBase> onInitialized,
            Action<MissionRuntimeBase> onChanged,
            Action<MissionRuntimeBase> onClaimable,
            Func<string> getCurrentDailyKey,
            Func<int> getCurrentDailyPeriodIndex,
            Func<string> getCurrentPeriodKey,
            Func<int> getCurrentPeriodIndex,
            Func<int> getCurrentPeriodElapsedDay)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _subscribeTrigger = subscribeTrigger ?? throw new ArgumentNullException(nameof(subscribeTrigger));
            _unsubscribeTrigger = unsubscribeTrigger ?? throw new ArgumentNullException(nameof(unsubscribeTrigger));
            _onInitialized = onInitialized ?? throw new ArgumentNullException(nameof(onInitialized));
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _onClaimable = onClaimable ?? throw new ArgumentNullException(nameof(onClaimable));
            _getCurrentDailyKey = getCurrentDailyKey ?? throw new ArgumentNullException(nameof(getCurrentDailyKey));
            _getCurrentDailyPeriodIndex = getCurrentDailyPeriodIndex ?? throw new ArgumentNullException(nameof(getCurrentDailyPeriodIndex));
            _getCurrentPeriodKey = getCurrentPeriodKey ?? throw new ArgumentNullException(nameof(getCurrentPeriodKey));
            _getCurrentPeriodIndex = getCurrentPeriodIndex ?? throw new ArgumentNullException(nameof(getCurrentPeriodIndex));
            _getCurrentPeriodElapsedDay = getCurrentPeriodElapsedDay ?? throw new ArgumentNullException(nameof(getCurrentPeriodElapsedDay));
        }

        public void RebuildBindings()
        {
            DetachAll();
            ensureDailyRuntimes();
            ensurePeriodRuntimes();
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

        public bool HasPeriodRuntimeOutsideCurrentPeriod()
        {
            var currentPeriodKey = _getCurrentPeriodKey();
            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeWeekly>())
            {
                if (!string.Equals(runtime.periodKey, currentPeriodKey, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public bool HasPeriodRuntimePendingActivation()
        {
            var currentPeriodKey = _getCurrentPeriodKey();
            var elapsedDay = _getCurrentPeriodElapsedDay();

            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeWeekly>())
            {
                if (!string.Equals(runtime.periodKey, currentPeriodKey, StringComparison.Ordinal))
                    continue;

                if (runtime.state != MissionRuntimeState.WAIT)
                    continue;

                if (!isPeriodDayActive(runtime.day, elapsedDay))
                    continue;

                return true;
            }

            return false;
        }

        public bool TryActivatePeriodRuntimes()
        {
            var currentPeriodKey = _getCurrentPeriodKey();
            var elapsedDay = _getCurrentPeriodElapsedDay();
            var activated = false;

            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeWeekly>())
            {
                if (!string.Equals(runtime.periodKey, currentPeriodKey, StringComparison.Ordinal))
                    continue;

                if (runtime.state != MissionRuntimeState.WAIT)
                    continue;

                if (!isPeriodDayActive(runtime.day, elapsedDay))
                    continue;

                if (!runtime.TryActivate())
                    continue;

                activated = true;
                _onInitialized(runtime);
            }

            return activated;
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

        public void ClearPeriodScope()
        {
            var removeRuntimeKeys = new List<int>();
            foreach (var kv in _storage.runtimes)
            {
                if (kv.Value is not MissionRuntimeWeekly runtime)
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
            var currentPeriodIndex = _getCurrentPeriodIndex();

            var removeRuntimeKeys = new HashSet<int>();
            foreach (var kv in _storage.runtimes)
            {
                switch (kv.Value)
                {
                    case MissionRuntimeDaily dailyRuntime:
                        if (!TryParseDailyPeriodIndex(dailyRuntime.periodKey, out var runtimeDailyIndex))
                            continue;

                        if (runtimeDailyIndex >= currentDailyIndex - 1)
                            continue;

                        dailyRuntime.Detach();
                        removeRuntimeKeys.Add(kv.Key);
                        break;

                    case MissionRuntimeWeekly periodRuntime:
                        if (!TryParsePeriodIndex(periodRuntime.periodKey, out var runtimePeriodIndex))
                            continue;

                        if (runtimePeriodIndex >= currentPeriodIndex - 1)
                            continue;

                        periodRuntime.Detach();
                        removeRuntimeKeys.Add(kv.Key);
                        break;
                }
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

        public MissionRuntimeWeekly FindPeriod(string missionId)
        {
            MissionRuntimeWeekly found = null;
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime is not MissionRuntimeWeekly periodRuntime)
                    continue;

                if (!string.Equals(periodRuntime.missionId, missionId, StringComparison.Ordinal))
                    continue;

                if (found != null)
                {
                    Debug.LogError($"[{Tag}] Duplicate period runtime detected for missionId='{missionId}'.");
                    return found;
                }

                found = periodRuntime;
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
            var initializedRuntimes = new List<MissionRuntimeBase>();

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
                    Debug.LogError($"[{Tag}] GAME_MESSAGE not found for daily mission: missionId='{row.MissionId}', messageId='{row.ConditionMsgId}'.");
                    continue;
                }

                var existing = FindDaily(row.MissionId);
                if (existing != null)
                {
                    var restored = MissionRuntimeFactory.Restore(new MissionRuntimeRestoreArgs
                    {
                        MissionType = MISSION_TYPE.DAILY,
                        MissionId = existing.missionId,
                        PeriodKey = existing.periodKey,
                        MissionUid = existing.missionUid,
                        ProgressValue = existing.progressValue,
                        State = existing.state,
                        Index = existing.index,
                        Day = 1,
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
                    initializedRuntimes.Add(restored);
                    continue;
                }

                var created = MissionRuntimeFactory.CreateDaily(new DailyMissionRuntimeCreateArgs
                {
                    MissionId = row.MissionId,
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

        void ensurePeriodRuntimes()
        {
            var periodKey = _getCurrentPeriodKey();
            var elapsedDay = _getCurrentPeriodElapsedDay();
            removePeriodRuntimesOutsidePeriod(periodKey);

            var selectedRows = selectPeriodRows();
            var selectedIds = new HashSet<string>(selectedRows.Select(row => row.MissionId), StringComparer.Ordinal);
            var initializedRuntimes = new List<MissionRuntimeBase>();

            var removeKeys = new List<int>();
            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeWeekly>())
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
                    Debug.LogError($"[{Tag}] GAME_MESSAGE not found for period mission: missionId='{row.MissionId}', messageId='{row.ConditionMsgId}'.");
                    continue;
                }

                var shouldWait = !isPeriodDayActive(row.Day, elapsedDay);
                var existing = FindPeriod(row.MissionId);
                if (existing != null)
                {
                    var restoreState = existing.state;
                    if (restoreState == MissionRuntimeState.WAIT && !shouldWait)
                        restoreState = MissionRuntimeState.ACTIVE;

                    var restored = MissionRuntimeFactory.Restore(new MissionRuntimeRestoreArgs
                    {
                        MissionType = MISSION_TYPE.WEEKLY,
                        MissionId = existing.missionId,
                        PeriodKey = existing.periodKey,
                        MissionUid = existing.missionUid,
                        ProgressValue = existing.progressValue,
                        State = restoreState,
                        Index = 0,
                        Day = row.Day,
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
                    initializedRuntimes.Add(restored);
                    continue;
                }

                var created = MissionRuntimeFactory.CreatePeriod(new PeriodMissionRuntimeCreateArgs
                {
                    MissionId = row.MissionId,
                    PeriodKey = periodKey,
                    MissionUid = AllocateMissionUid(),
                    Day = row.Day,
                    IsWaiting = shouldWait,
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

        void removePeriodRuntimesOutsidePeriod(string periodKey)
        {
            var removeKeys = new List<int>();
            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeWeekly>())
            {
                if (string.Equals(runtime.periodKey, periodKey, StringComparison.Ordinal))
                    continue;

                runtime.Detach();
                removeKeys.Add(runtime.missionUid);
            }

            foreach (var key in removeKeys)
                _storage.runtimes.Remove(key);
        }

        List<MISSION_DAILY> selectDailyRows(string periodKey)
        {
            var candidates = TB_MISSION_DAILY.GetAll()
                .Where(isEligibleDailyRow)
                .ToList();

            var selected = new List<MISSION_DAILY>(MaxDailyRuntimeCount);
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

        List<MISSION_WEEKLY> selectPeriodRows()
        {
            return TB_MISSION_WEEKLY.GetAll()
                .Where(isEligiblePeriodRow)
                .OrderBy(row => row.Day)
                .ThenBy(row => row.MissionId, StringComparer.Ordinal)
                .ToList();
        }

        void assignDailyIndices(string periodKey, IReadOnlyList<MISSION_DAILY> selectedRows)
        {
            var rowByMissionId = new Dictionary<string, MISSION_DAILY>(StringComparer.Ordinal);
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

        static bool isEligibleDailyRow(MISSION_DAILY row)
        {
            return row != null
                   && row.IsActive
                   && row.ConditionValue.HasValue
                   && TryResolveMessage(row.ConditionMsgId, out var message)
                   && message.SaveType != GAME_MESSAGE_SAVE_TYPE.NONE;
        }

        static bool isEligiblePeriodRow(MISSION_WEEKLY row)
        {
            return row != null
                   && row.IsActive
                   && row.Day >= 1
                   && row.Day <= 7
                   && row.ConditionValue.HasValue
                   && TryResolveMessage(row.ConditionMsgId, out var message)
                   && message.SaveType != GAME_MESSAGE_SAVE_TYPE.NONE;
        }

        static bool TryResolveMessage(string messageId, out GAME_MESSAGE message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            message = TB_GAME_MESSAGE.Get(messageId);
            return message != null;
        }

        static bool isPeriodDayActive(int runtimeDay, int elapsedDay)
        {
            var requiredElapsedDay = Math.Max(0, runtimeDay - 1);
            return elapsedDay >= requiredElapsedDay;
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

        static bool TryParsePeriodIndex(string periodKey, out int periodIndex)
        {
            periodIndex = 0;
            if (string.IsNullOrWhiteSpace(periodKey))
                return false;

            if (!periodKey.StartsWith("period:", StringComparison.Ordinal))
                return false;

            return int.TryParse(periodKey.Substring(7), out periodIndex);
        }

        static Func<CBigInt> createExternalProgressReader(string messageId, GAME_MESSAGE_SAVE_TYPE saveType)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return null;

            if (!GameMessageRule.IsTotalSaveType(saveType))
                return null;

            var key = messageId;
            return () =>
            {
                if (!GameMessageManager.TryGet(out var messageManager) || messageManager == null)
                    return CBigInt.Zero;

                return messageManager.GetStat(key);
            };
        }
    }
}
