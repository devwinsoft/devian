using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Firebase.Functions;
using UnityEngine;

namespace Devian
{
    public sealed class MissionManager : CompoSingleton<MissionManager>
    {
        const string Tag = nameof(MissionManager);
        const string PeriodOnce = "once";
        const long DayMs = 24L * 60L * 60L * 1000L;
        const long ResetAnchorThresholdMs = 7L * DayMs;
        string _functionsRegion = string.Empty;

        readonly MissionStorage _storage = new();
        readonly MissionTriggerSystem _triggerSystem = new();
        MissionScheduler _scheduler;
        bool _initialized;

        public MissionStorage Storage => _storage;
        public MissionTriggerSystem triggerSystem => _triggerSystem;
        public bool IsInitialized => _initialized;

        protected override void Awake()
        {
            base.Awake();
            _scheduler = new MissionScheduler(
                _storage,
                _triggerSystem,
                onRuntimeChanged,
                onRuntimeClaimable,
                getCurrentDailyKey,
                getCurrentDailyPeriodIndex);
        }

        public void SetFunctionsRegion(string region)
        {
            _functionsRegion = string.IsNullOrWhiteSpace(region) ? string.Empty : region.Trim();
        }

        public async Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            var refresh = await RefreshClockAsync(ct);
            if (refresh.IsFailure)
                return CommonResult.Failure(refresh.Error!);

            var nowUtcMs = refresh.Value.serverNowUtcMs;
            if (_storage.dailyMissionStartUtcMs <= 0L)
            {
                _storage.dailyMissionStartUtcMs = nowUtcMs;
            }
            else if (Math.Abs(nowUtcMs - _storage.dailyMissionStartUtcMs) > ResetAnchorThresholdMs)
            {
                _storage.dailyMissionStartUtcMs = nowUtcMs;
                clearDailyScopeData();
            }

            rebuildRuntimeBindings();
            pruneExpiredMissionState();

            _initialized = true;
            return CommonResult.Ok();
        }

        public async Task<CommonResult<MissionClockSnapshot>> RefreshClockAsync(CancellationToken ct = default)
        {
            var clock = await getMissionClockAsync(ct);
            if (clock.IsFailure)
                return clock;

            _storage.clockSnapshot = clock.Value;
            _storage.clockReceivedAtClientUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_initialized)
            {
                rebuildRuntimeBindings();
                pruneExpiredMissionState();
            }

            return clock;
        }

        public bool TryGetServerNowUtcMs(out long serverNowUtcMs)
        {
            serverNowUtcMs = 0L;
            var snapshot = _storage.clockSnapshot;
            if (snapshot == null || snapshot.serverNowUtcMs <= 0L)
                return false;

            var clientNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            serverNowUtcMs = snapshot.serverNowUtcMs + Math.Max(0L, clientNowUtcMs - _storage.clockReceivedAtClientUtcMs);
            return true;
        }

        public MissionRuntimeState GetMissionRuntimeState(MISSION_TYPE missionKind, string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return MissionRuntimeState.NONE;

            switch (missionKind)
            {
                case MISSION_TYPE.DAILY:
                    return getDailyRuntimeState(missionId);

                case MISSION_TYPE.ACHIEVEMENT:
                    return getAchievementRuntimeState(missionId);

                default:
                    return MissionRuntimeState.NONE;
            }
        }

        public async Task<CommonResult> ClaimAsync(MISSION_TYPE missionKind, string missionId, CancellationToken ct = default)
        {
            if (!_initialized)
                return CommonResult.Failure(CommonErrorType.SAVEDATA_SYNC_REQUIRED, "MissionManager is not initialized.");

            if (string.IsNullOrWhiteSpace(missionId))
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, "missionId is empty.");

            switch (missionKind)
            {
                case MISSION_TYPE.DAILY:
                    return await claimDailyAsync(missionId, ct);

                case MISSION_TYPE.ACHIEVEMENT:
                    return await claimAchievementAsync(missionId, ct);

                default:
                    return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Unsupported missionKind: {missionKind}");
            }
        }

        public void ClearStorage()
        {
            detachAllRuntimes();
            _storage.Clear();
            _initialized = false;
        }

        MissionRuntimeState getDailyRuntimeState(string missionId)
        {
            var row = TB_MISSION_DAILY.Get(missionId);
            if (row == null || !row.IsActive || row.ConditionOp == MISSION_OP_TYPE.NONE || !row.ConditionValue.HasValue)
                return MissionRuntimeState.NONE;

            var runtime = findDailyRuntime(missionId);
            if (runtime == null)
                return MissionRuntimeState.NONE;

            if (!string.Equals(runtime.periodKey, getCurrentDailyKey(), StringComparison.Ordinal))
                return MissionRuntimeState.NONE;

            return runtime.GetState();
        }

        MissionRuntimeState getAchievementRuntimeState(string missionId)
        {
            var runtime = findAchievementRuntime(missionId);
            if (runtime == null)
                return MissionRuntimeState.NONE;

            return runtime.GetState();
        }

        async Task<CommonResult> claimDailyAsync(string missionId, CancellationToken ct)
        {
            var row = TB_MISSION_DAILY.Get(missionId);
            if (row == null || !row.IsActive || row.ConditionOp == MISSION_OP_TYPE.NONE || !row.ConditionValue.HasValue)
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Daily mission not found: {missionId}");

            var periodKey = getCurrentDailyKey();
            var runtime = findDailyRuntime(missionId);
            if (runtime == null)
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Daily runtime missing: {missionId}");

            if (!string.Equals(runtime.periodKey, periodKey, StringComparison.Ordinal))
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Daily runtime stale: {missionId}/{runtime.periodKey}->{periodKey}");

            if (!runtime.IsClaimable)
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Daily mission is not claimable: {missionId}");

            var grantId = buildGrantId(MISSION_TYPE.DAILY, missionId, 1, periodKey);
            if (_storage.claimRecords.ContainsKey(grantId))
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Daily mission already claimed: {grantId}");

            var apply = RewardManager.Instance.ApplyRewardGroup(runtime.rewardGroupId);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            runtime.MarkCompleted();
            _storage.claimRecords[grantId] = createClaimRecord(MISSION_TYPE.DAILY, missionId, 1, grantId, periodKey, runtime.rewardGroupId);

            return await saveMissionStateAsync(ct);
        }

        async Task<CommonResult> claimAchievementAsync(string missionId, CancellationToken ct)
        {
            var runtime = findAchievementRuntime(missionId);
            if (runtime == null)
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Achievement runtime missing: {missionId}");

            if (!runtime.IsClaimable)
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Achievement is not claimable: {missionId}");

            var grantId = buildGrantId(MISSION_TYPE.ACHIEVEMENT, missionId, runtime.level, PeriodOnce);
            if (_storage.claimRecords.ContainsKey(grantId))
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Achievement already claimed: {grantId}");

            var apply = RewardManager.Instance.ApplyRewardGroup(runtime.rewardGroupId);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            _storage.claimRecords[grantId] = createClaimRecord(
                MISSION_TYPE.ACHIEVEMENT,
                missionId,
                runtime.level,
                grantId,
                PeriodOnce,
                runtime.rewardGroupId);

            var nextRow = findAchievementNextRow(runtime.missionId, runtime.level);
            if (nextRow != null && nextRow.IsActive && nextRow.ConditionOp != MISSION_OP_TYPE.NONE && nextRow.ConditionValue.HasValue)
            {
                runtime.LevelUp(
                    nextRow.Level,
                    runtime.progressValue,
                    nextRow.ConditionType,
                    nextRow.ConditionOp,
                    nextRow.ConditionValue.Value,
                    nextRow.RewardGroupId);
            }
            else
            {
                runtime.MarkCompleted();
            }

            return await saveMissionStateAsync(ct);
        }

        async Task<CommonResult> saveMissionStateAsync(CancellationToken ct)
        {
            var save = await SaveDataManager.Instance.SaveGameStorageAsync(ct);
            if (save.IsFailure)
            {
                Debug.LogError($"[{Tag}] Mission save failed: {save.Error}");
                return CommonResult.Failure(save.Error!);
            }

            return CommonResult.Ok();
        }

        void onRuntimeChanged(MissionRuntimeBase runtime)
        {
        }

        void onRuntimeClaimable(MissionRuntimeBase runtime)
        {
        }

        void detachAllRuntimes()
        {
            _scheduler.DetachAll();
        }

        void clearDailyScopeData()
        {
            _scheduler.ClearDailyScope();
        }

        void rebuildRuntimeBindings()
        {
            _scheduler.RebuildBindings();
        }

        void pruneExpiredMissionState()
        {
            _scheduler.PruneExpiredState();
        }

        int allocateMissionUid()
        {
            return _scheduler.AllocateMissionUid();
        }

        MissionRuntimeDaily findDailyRuntime(string missionId)
        {
            return _scheduler.FindDaily(missionId);
        }

        MissionRuntimeAchieve findAchievementRuntime(string missionId)
        {
            return _scheduler.FindAchieve(missionId);
        }

        static MISSION_ACHIEVE findAchievementRow(string missionId, int level)
        {
            var rows = TB_MISSION_ACHIEVE.GetByGroup(missionId);
            foreach (var row in rows)
            {
                if (row.Level == level)
                    return row;
            }

            return null;
        }

        static MISSION_ACHIEVE findAchievementNextRow(string missionId, int level)
        {
            MISSION_ACHIEVE next = null;
            foreach (var row in TB_MISSION_ACHIEVE.GetByGroup(missionId))
            {
                if (row.Level <= level)
                    continue;

                if (next == null || row.Level < next.Level)
                    next = row;
            }

            return next;
        }

        string getCurrentDailyKey()
        {
            return $"day:{getCurrentDailyPeriodIndex()}";
        }

        int getCurrentDailyPeriodIndex()
        {
            if (!TryGetServerNowUtcMs(out var estimatedServerNowUtcMs))
                return 0;

            if (_storage.dailyMissionStartUtcMs <= 0L)
                return 0;

            var diff = Math.Max(0L, estimatedServerNowUtcMs - _storage.dailyMissionStartUtcMs);
            return (int)(diff / DayMs);
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

        static string buildGrantId(MISSION_TYPE missionKind, string missionId, int level, string periodKey)
        {
            return missionKind switch
            {
                MISSION_TYPE.DAILY => $"mission:daily:{missionId}:{periodKey}",
                MISSION_TYPE.ACHIEVEMENT => $"mission:achievement:{missionId}:{level}:{periodKey}",
                _ => $"mission:{missionKind}:{missionId}:{periodKey}",
            };
        }

        static MissionClaimRecord createClaimRecord(
            MISSION_TYPE missionKind,
            string missionId,
            int level,
            string grantId,
            string periodKey,
            string rewardGroupId)
        {
            return new MissionClaimRecord
            {
                missionKind = missionKind,
                missionId = missionId ?? string.Empty,
                level = level,
                grantId = grantId ?? string.Empty,
                periodKey = periodKey ?? string.Empty,
                rewardGroupId = rewardGroupId ?? string.Empty,
                claimedAtClientUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        async Task<CommonResult<MissionClockSnapshot>> getMissionClockAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            await Task.CompletedTask;
            return CommonResult<MissionClockSnapshot>.Success(
                new MissionClockSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
#else
            try
            {
                var functions = string.IsNullOrEmpty(_functionsRegion)
                    ? FirebaseFunctions.DefaultInstance
                    : FirebaseFunctions.GetInstance(_functionsRegion);
                var callable = functions.GetHttpsCallable("getMissionClock");
                var result = await callable.CallAsync();
                ct.ThrowIfCancellationRequested();

                var response = normalizeCallableResponse(result.Data);
                if (response == null)
                    return CommonResult<MissionClockSnapshot>.Failure(
                        CommonErrorType.COMMON_SERVER,
                        "getMissionClock returned unsupported response.");

                var serverNowUtcMs = readLong(response, "serverNowUtcMs");
                if (serverNowUtcMs <= 0L)
                    serverNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                return CommonResult<MissionClockSnapshot>.Success(new MissionClockSnapshot(serverNowUtcMs));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FunctionsException fex) when (
                fex.ErrorCode == FunctionsErrorCode.Unavailable ||
                fex.ErrorCode == FunctionsErrorCode.DeadlineExceeded)
            {
                return CommonResult<MissionClockSnapshot>.Failure(CommonErrorType.COMMON_NETWORK, "Mission clock network unavailable.");
            }
            catch (FunctionsException fex) when (
                fex.ErrorCode == FunctionsErrorCode.Unauthenticated ||
                fex.ErrorCode == FunctionsErrorCode.PermissionDenied)
            {
                return CommonResult<MissionClockSnapshot>.Failure(CommonErrorType.COMMON_AUTH, fex.Message);
            }
            catch (Exception ex)
            {
                return CommonResult<MissionClockSnapshot>.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
            }
#endif
        }

        static Dictionary<string, object> normalizeCallableResponse(object data)
        {
            if (data == null)
                return null;

            if (data is IDictionary<string, object> stringObjectMap)
                return normalizeStringObjectMap(stringObjectMap);

            if (data is IDictionary anyMap)
                return normalizeAnyMap(anyMap);

            return null;
        }

        static Dictionary<string, object> normalizeStringObjectMap(IDictionary<string, object> source)
        {
            var result = new Dictionary<string, object>(source.Count);
            foreach (var kv in source)
                result[kv.Key] = normalizeCallableValue(kv.Value);
            return result;
        }

        static Dictionary<string, object> normalizeAnyMap(IDictionary source)
        {
            var result = new Dictionary<string, object>(source.Count);
            foreach (DictionaryEntry entry in source)
            {
                if (entry.Key == null)
                    continue;

                result[entry.Key.ToString()] = normalizeCallableValue(entry.Value);
            }

            return result;
        }

        static object normalizeCallableValue(object value)
        {
            if (value == null)
                return null;

            if (value is IDictionary<string, object> stringObjectMap)
                return normalizeStringObjectMap(stringObjectMap);

            if (value is IDictionary anyMap)
                return normalizeAnyMap(anyMap);

            if (value is IList list && value is not string)
            {
                var normalized = new List<object>(list.Count);
                foreach (var item in list)
                    normalized.Add(normalizeCallableValue(item));
                return normalized;
            }

            return value;
        }

        static long readLong(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var raw) || raw == null)
                return 0L;

            switch (raw)
            {
                case long longValue:
                    return longValue;
                case int intValue:
                    return intValue;
                case double doubleValue:
                    return Convert.ToInt64(doubleValue);
                case float floatValue:
                    return Convert.ToInt64(floatValue);
                case string stringValue when long.TryParse(stringValue, out var parsed):
                    return parsed;
                default:
                    return 0L;
            }
        }
    }
}
