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
        readonly Action<MissionRuntimeBase> _onChanged;
        readonly Action<MissionRuntimeBase> _onClaimable;
        readonly Func<string> _getCurrentDailyKey;
        readonly Func<int> _getCurrentDailyPeriodIndex;

        public MissionScheduler(
            MissionStorage storage,
            MissionTriggerSystem triggerSystem,
            Action<MissionRuntimeBase> onChanged,
            Action<MissionRuntimeBase> onClaimable,
            Func<string> getCurrentDailyKey,
            Func<int> getCurrentDailyPeriodIndex)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _triggerSystem = triggerSystem ?? throw new ArgumentNullException(nameof(triggerSystem));
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
                if (kv.Value == null || kv.Value.missionKind != MISSION_TYPE.DAILY)
                    continue;

                kv.Value.Detach();
                removeRuntimeKeys.Add(kv.Key);
            }

            foreach (var key in removeRuntimeKeys)
                _storage.runtimes.Remove(key);

            var removeClaimKeys = new List<string>();
            foreach (var kv in _storage.claimRecords)
            {
                if (kv.Value != null && kv.Value.missionKind == MISSION_TYPE.DAILY)
                    removeClaimKeys.Add(kv.Key);
            }

            foreach (var key in removeClaimKeys)
                _storage.claimRecords.Remove(key);
        }

        public void PruneExpiredState()
        {
            var currentDailyIndex = _getCurrentDailyPeriodIndex();
            var removeRuntimeKeys = new List<int>();
            foreach (var kv in _storage.runtimes)
            {
                var runtime = kv.Value;
                if (runtime == null || runtime.missionKind != MISSION_TYPE.DAILY)
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

            var removeClaimKeys = new List<string>();
            foreach (var kv in _storage.claimRecords)
            {
                var record = kv.Value;
                if (record == null || record.missionKind != MISSION_TYPE.DAILY)
                    continue;

                if (!TryParseDailyPeriodIndex(record.periodKey, out var claimDailyIndex))
                    continue;

                if (claimDailyIndex >= currentDailyIndex - 1)
                    continue;

                removeClaimKeys.Add(kv.Key);
            }

            foreach (var key in removeClaimKeys)
                _storage.claimRecords.Remove(key);
        }

        public MissionRuntimeDaily FindDaily(string missionId)
        {
            MissionRuntimeDaily found = null;
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime is not MissionRuntimeDaily dailyRuntime)
                    continue;

                if (dailyRuntime.missionKind != MISSION_TYPE.DAILY)
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

                if (achieveRuntime.missionKind != MISSION_TYPE.ACHIEVEMENT)
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
                var existing = FindDaily(row.MissionId);
                if (existing != null)
                {
                    var restored = MissionRuntimeFactory.Restore(new MissionRuntimeRestoreArgs
                    {
                        MissionKind = MISSION_TYPE.DAILY,
                        MissionId = existing.missionId,
                        PeriodKey = existing.periodKey,
                        MissionUid = existing.missionUid,
                        Level = 1,
                        StartValue = CBigInt.Zero,
                        ProgressValue = existing.progressValue,
                        IsCompleted = existing.isCompleted,
                        ConditionType = row.ConditionType,
                        ConditionOp = row.ConditionOp,
                        ConditionValue = row.ConditionValue!.Value,
                        RewardGroupId = row.RewardGroupId,
                        TriggerSystem = _triggerSystem,
                        OnChanged = _onChanged,
                        OnClaimable = _onClaimable,
                    });

                    _storage.runtimes[restored.missionUid] = restored;
                    continue;
                }

                var created = MissionRuntimeFactory.CreateDaily(new DailyMissionRuntimeCreateArgs
                {
                    MissionKind = MISSION_TYPE.DAILY,
                    MissionId = row.MissionId,
                    PeriodKey = periodKey,
                    MissionUid = AllocateMissionUid(),
                    ConditionType = row.ConditionType,
                    ConditionOp = row.ConditionOp,
                    ConditionValue = row.ConditionValue!.Value,
                    RewardGroupId = row.RewardGroupId,
                    TriggerSystem = _triggerSystem,
                    OnChanged = _onChanged,
                    OnClaimable = _onClaimable,
                });

                _storage.runtimes[created.missionUid] = created;
            }
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
                        var restored = MissionRuntimeFactory.Restore(new MissionRuntimeRestoreArgs
                        {
                            MissionKind = MISSION_TYPE.ACHIEVEMENT,
                            MissionId = existing.missionId,
                            PeriodKey = existing.periodKey,
                            MissionUid = existing.missionUid,
                            Level = existing.level,
                            StartValue = existing.startValue,
                            ProgressValue = existing.progressValue,
                            IsCompleted = existing.isCompleted,
                            ConditionType = row.ConditionType,
                            ConditionOp = row.ConditionOp,
                            ConditionValue = row.ConditionValue!.Value,
                            RewardGroupId = row.RewardGroupId,
                            TriggerSystem = _triggerSystem,
                            OnChanged = _onChanged,
                            OnClaimable = _onClaimable,
                        });

                        _storage.runtimes[restored.missionUid] = restored;
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

                var created = MissionRuntimeFactory.CreateAchieve(new AchieveMissionRuntimeCreateArgs
                {
                    MissionKind = MISSION_TYPE.ACHIEVEMENT,
                    MissionId = startRow!.MissionId,
                    Level = startRow.Level,
                    PeriodKey = "once",
                    MissionUid = AllocateMissionUid(),
                    StartValue = CBigInt.Zero,
                    ConditionType = startRow.ConditionType,
                    ConditionOp = startRow.ConditionOp,
                    ConditionValue = startRow.ConditionValue!.Value,
                    RewardGroupId = startRow.RewardGroupId,
                    TriggerSystem = _triggerSystem,
                    OnChanged = _onChanged,
                    OnClaimable = _onClaimable,
                });

                _storage.runtimes[created.missionUid] = created;
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

        static bool isEligibleDailyRow(MISSION_DAILY row)
        {
            return row != null && row.IsActive && row.ConditionOp != MISSION_OP_TYPE.NONE && row.ConditionValue.HasValue;
        }

        static bool isEligibleAchievementRow(MISSION_ACHIEVE row)
        {
            return row != null && row.IsActive && row.ConditionOp != MISSION_OP_TYPE.NONE && row.ConditionValue.HasValue;
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
