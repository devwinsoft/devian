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
        readonly MissionTriggerSystem _triggerSystem;
        readonly Action<MissionRuntimeBase> _onInitialized;
        readonly Action<MissionRuntimeBase> _onChanged;
        readonly Action<MissionRuntimeBase> _onClaimable;
        readonly Func<string> _getCurrentDailyKey;
        readonly Func<int> _getCurrentDailyPeriodIndex;

        public MissionScheduler(
            MissionStorage storage,
            MissionTriggerSystem triggerSystem,
            Action<MissionRuntimeBase> onInitialized,
            Action<MissionRuntimeBase> onChanged,
            Action<MissionRuntimeBase> onClaimable,
            Func<string> getCurrentDailyKey,
            Func<int> getCurrentDailyPeriodIndex)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _triggerSystem = triggerSystem ?? throw new ArgumentNullException(nameof(triggerSystem));
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
            ensureAchievementRuntimes();
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
                if (kv.Value == null || kv.Value.missionType != MISSION_TYPE.DAY)
                    continue;

                kv.Value.Detach();
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
                var runtime = kv.Value;
                if (runtime == null || runtime.missionType != MISSION_TYPE.DAY)
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

                if (dailyRuntime.missionType != MISSION_TYPE.DAY)
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

        public MissionRuntimeAchieve FindAchieve(string missionId)
        {
            MissionRuntimeAchieve found = null;
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime is not MissionRuntimeAchieve achieveRuntime)
                    continue;

                if (achieveRuntime.missionType != MISSION_TYPE.ACHIEVE)
                    continue;

                if (!string.Equals(achieveRuntime.missionId, missionId, StringComparison.Ordinal))
                    continue;

                if (found != null)
                {
                    Debug.LogError($"[{Tag}] Duplicate achievement runtime detected for missionId='{missionId}'.");
                    return found;
                }

                found = achieveRuntime;
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
                if (!TryResolveMissionStat(row.MissionStatId, out var missionStat))
                {
                    Debug.LogError($"[{Tag}] MISSION_STAT not found for daily mission: missionId='{row.MissionId}', missionStatId='{row.MissionStatId}'.");
                    continue;
                }

                var existing = FindDaily(row.MissionId);
                if (existing != null)
                {
                    var restored = MissionRuntimeFactory.Restore(new MissionRuntimeRestoreArgs
                    {
                        MissionType = MISSION_TYPE.DAY,
                        MissionId = existing.missionId,
                        MissionStatId = row.MissionStatId,
                        PeriodKey = existing.periodKey,
                        MissionUid = existing.missionUid,
                        Level = 1,
                        ProgressValue = existing.progressValue,
                        IsCompleted = existing.isCompleted,
                        Index = existing.index,
                        StatType = missionStat.StatType,
                        OpType = missionStat.OpType,
                        ConditionValue = row.ConditionValue!.Value,
                        TriggerSystem = _triggerSystem,
                        OnChanged = _onChanged,
                        OnClaimable = _onClaimable,
                    });

                    _storage.runtimes[restored.missionUid] = restored;
                    initializedRuntimes.Add((MissionRuntimeDaily)restored);
                    continue;
                }

                var created = MissionRuntimeFactory.CreateDaily(new DailyMissionRuntimeCreateArgs
                {
                    MissionType = MISSION_TYPE.DAY,
                    MissionId = row.MissionId,
                    MissionStatId = row.MissionStatId,
                    PeriodKey = periodKey,
                    MissionUid = AllocateMissionUid(),
                    Index = 0,
                    StatType = missionStat.StatType,
                    OpType = missionStat.OpType,
                    ConditionValue = row.ConditionValue!.Value,
                    TriggerSystem = _triggerSystem,
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

        void ensureAchievementRuntimes()
        {
            var groupKeys = new HashSet<string>(TB_MISSION_ACHIEVE.GetGroupKeys(), StringComparer.Ordinal);
            var strayKeys = new List<int>();
            foreach (var runtime in _storage.runtimes.Values.OfType<MissionRuntimeAchieve>())
            {
                if (!groupKeys.Contains(runtime.missionId))
                    strayKeys.Add(runtime.missionUid);
            }

            foreach (var key in strayKeys)
            {
                if (!_storage.runtimes.TryGetValue(key, out var runtime) || runtime == null)
                    continue;

                runtime.Detach();
                _storage.runtimes.Remove(key);
            }

            foreach (var groupKey in groupKeys)
            {
                var existing = FindAchieve(groupKey);
                if (existing != null)
                {
                    var row = findAchievementRow(existing.missionId, existing.level);
                    if (isEligibleAchievementRow(row))
                    {
                        if (!TryResolveMissionStat(row!.MissionStatId, out var missionStat))
                        {
                            existing.Detach();
                            _storage.runtimes.Remove(existing.missionUid);
                            continue;
                        }

                        var restored = MissionRuntimeFactory.Restore(new MissionRuntimeRestoreArgs
                        {
                            MissionType = MISSION_TYPE.ACHIEVE,
                            MissionId = existing.missionId,
                            MissionStatId = row.MissionStatId,
                            PeriodKey = existing.periodKey,
                            MissionUid = existing.missionUid,
                            Level = existing.level,
                            ProgressValue = existing.progressValue,
                            IsCompleted = existing.isCompleted,
                            StatType = missionStat.StatType,
                            OpType = missionStat.OpType,
                            ConditionValue = row.ConditionValue!.Value,
                            TriggerSystem = _triggerSystem,
                            ReadProgress = createMissionStatProgressReader(row.MissionStatId),
                            OnChanged = _onChanged,
                            OnClaimable = _onClaimable,
                        });

                        _storage.runtimes[restored.missionUid] = restored;
                        _onInitialized(restored);
                        continue;
                    }

                    existing.Detach();
                    _storage.runtimes.Remove(existing.missionUid);
                }

                var startRow = findAchievementStartRow(groupKey);
                if (!isEligibleAchievementRow(startRow))
                {
                    if (TB_MISSION_ACHIEVE.GetByGroup(groupKey).Any(isEligibleAchievementRow))
                        Debug.LogError($"[{Tag}] Missing level=1 achievement row for missionId='{groupKey}'.");
                    continue;
                }

                if (!TryResolveMissionStat(startRow!.MissionStatId, out var startMissionStat))
                {
                    Debug.LogError($"[{Tag}] MISSION_STAT not found for achievement mission: missionId='{startRow.MissionId}', missionStatId='{startRow.MissionStatId}'.");
                    continue;
                }

                var created = MissionRuntimeFactory.CreateAchieve(new AchieveMissionRuntimeCreateArgs
                {
                    MissionType = MISSION_TYPE.ACHIEVE,
                    MissionId = startRow!.MissionId,
                    MissionStatId = startRow.MissionStatId,
                    Level = startRow.Level,
                    PeriodKey = "once",
                    MissionUid = AllocateMissionUid(),
                    StatType = startMissionStat.StatType,
                    OpType = startMissionStat.OpType,
                    ConditionValue = startRow.ConditionValue!.Value,
                    TriggerSystem = _triggerSystem,
                    ReadProgress = createMissionStatProgressReader(startRow.MissionStatId),
                    OnChanged = _onChanged,
                    OnClaimable = _onClaimable,
                });

                _storage.runtimes[created.missionUid] = created;
                _onInitialized(created);
            }
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

        List<MISSION_DAY> selectDailyRows(string periodKey)
        {
            var candidates = TB_MISSION_DAY.GetAll()
                .Where(isEligibleDailyRow)
                .ToList();

            var selected = new List<MISSION_DAY>(MaxDailyRuntimeCount);
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

        void assignDailyIndices(string periodKey, IReadOnlyList<MISSION_DAY> selectedRows)
        {
            var rowByMissionId = new Dictionary<string, MISSION_DAY>(StringComparer.Ordinal);
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

        static bool isEligibleDailyRow(MISSION_DAY row)
        {
            return row != null
                   && row.IsActive
                   && row.ConditionValue.HasValue
                   && TryResolveMissionStat(row.MissionStatId, out var missionStat)
                   && missionStat.OpType != MISSION_OP_TYPE.NONE;
        }

        static bool isEligibleAchievementRow(MISSION_ACHIEVE row)
        {
            return row != null
                   && row.IsActive
                   && row.ConditionValue.HasValue
                   && TryResolveMissionStat(row.MissionStatId, out var missionStat)
                   && missionStat.OpType != MISSION_OP_TYPE.NONE;
        }

        static bool TryResolveMissionStat(string missionStatId, out MISSION_STAT missionStat)
        {
            missionStat = null;
            if (string.IsNullOrWhiteSpace(missionStatId))
                return false;

            missionStat = TB_MISSION_STAT.Get(missionStatId);
            return missionStat != null;
        }

        Func<CBigInt> createMissionStatProgressReader(string missionStatId)
        {
            if (string.IsNullOrWhiteSpace(missionStatId))
                return static () => CBigInt.Zero;

            var key = missionStatId;
            return () => _storage.GetStat(key);
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

        static MISSION_ACHIEVE findAchievementRow(string missionId, int level)
        {
            foreach (var row in TB_MISSION_ACHIEVE.GetByGroup(missionId))
            {
                if (row.Level == level)
                    return row;
            }

            return null;
        }

        static MISSION_ACHIEVE findAchievementStartRow(string missionId)
        {
            foreach (var row in TB_MISSION_ACHIEVE.GetByGroup(missionId))
            {
                if (row.Level == 1)
                    return row;
            }

            return null;
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
    }
}
