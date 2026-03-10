using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class MissionManager : CompoSingleton<MissionManager>
    {
        const string Tag = nameof(MissionManager);
        const long DayMs = 24L * 60L * 60L * 1000L;
        const int PeriodCycleDays = 10;
        const long PeriodMs = PeriodCycleDays * DayMs;
        const long DailyResetAnchorThresholdMs = 7L * DayMs;

        readonly MissionStorage _storage = new();
        MissionMessageTrigger _missionMessageSystem;
        MissionScheduler _scheduler;
        bool _initialized;

        public MissionStorage Storage => _storage;
        public bool IsInitialized => _initialized;

        protected override void onInitAwake()
        {
            _missionMessageSystem = new MissionMessageTrigger();

            _scheduler = new MissionScheduler(
                _storage,
                subscribeRuntimeTrigger,
                unSubcribeRuntimeTrigger,
                onRuntimeInitialized,
                onRuntimeChanged,
                onRuntimeClaimable,
                getCurrentDailyKey,
                getCurrentDailyPeriodIndex,
                getCurrentPeriodKey,
                getCurrentPeriodIndex,
                getCurrentPeriodElapsedDay);
        }

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            _ = ct;

            if (!TryGetServerNowUtcMs(out var nowUtcMs))
            {
                return Task.FromResult(CommonResult.Failure(
                    CommonErrorType.COMMON_SERVER,
                    "Server time is unavailable. Initialize RemoteConfigManager before MissionManager."));
            }

            if (_storage.dailyMissionStartUtcMs <= 0L)
            {
                _storage.dailyMissionStartUtcMs = nowUtcMs;
            }
            else if (Math.Abs(nowUtcMs - _storage.dailyMissionStartUtcMs) > DailyResetAnchorThresholdMs)
            {
                _storage.dailyMissionStartUtcMs = nowUtcMs;
                clearDailyScopeData();
            }

            if (_storage.periodMissionStartUtcMs <= 0L)
                _storage.periodMissionStartUtcMs = nowUtcMs;

            rebuildRuntimeBindings();
            pruneExpiredMissionState();
            _scheduler.TryActivatePeriodRuntimes();

            _initialized = true;
            return Task.FromResult(CommonResult.Ok());
        }

        public void RefreshRuntimes()
        {
            if (!_initialized)
                return;

            var dailyExpired = GetRemainTime(MISSION_TYPE.DAILY) == TimeSpan.Zero
                               || _scheduler.HasDailyRuntimeOutsideCurrentPeriod();
            var periodExpired = GetRemainTime(MISSION_TYPE.PERIOD) == TimeSpan.Zero
                                || _scheduler.HasPeriodRuntimeOutsideCurrentPeriod();
            if (dailyExpired || periodExpired)
            {
                rebuildRuntimeBindings();
                pruneExpiredMissionState();
                return;
            }

            if (_storage.runtimes.Count <= 0)
            {
                rebuildRuntimeBindings();
                pruneExpiredMissionState();
                return;
            }

            if (_scheduler.HasPeriodRuntimePendingActivation())
                _scheduler.TryActivatePeriodRuntimes();

            var runtimes = new List<MissionRuntimeBase>(_storage.runtimes.Count);
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime != null)
                    runtimes.Add(runtime);
            }

            foreach (var runtime in runtimes)
                onRuntimeInitialized(runtime);
        }

        public bool TryGetServerNowUtcMs(out long serverNowUtcMs)
        {
            serverNowUtcMs = 0L;
            if (!RemoteConfigManager.TryGet(out var remoteConfigManager)
                || remoteConfigManager == null)
            {
                return false;
            }

            return remoteConfigManager.TryGetServerNowUtcMs(out serverNowUtcMs);
        }

        public MissionRuntimeState GetMissionRuntimeState(MISSION_TYPE missionType, string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return MissionRuntimeState.NONE;

            if (_initialized)
                syncRuntimeSchedule();

            switch (missionType)
            {
                case MISSION_TYPE.DAILY:
                    return getDailyRuntimeState(missionId);

                case MISSION_TYPE.PERIOD:
                    return getPeriodRuntimeState(missionId);

                default:
                    return MissionRuntimeState.NONE;
            }
        }

        public TimeSpan GetRemainTime(MISSION_TYPE missionType)
        {
            switch (missionType)
            {
                case MISSION_TYPE.DAILY:
                    return getRemainTimeCore(_storage.dailyMissionStartUtcMs, DayMs);

                case MISSION_TYPE.PERIOD:
                    return getRemainTimeCore(_storage.periodMissionStartUtcMs, PeriodMs);

                default:
                    return default;
            }
        }

        public async Task<CommonResult> ClaimAsync(MISSION_TYPE missionType, string missionId, CancellationToken ct = default)
        {
            if (!_initialized)
                return CommonResult.Failure(CommonErrorType.SAVEDATA_SYNC_REQUIRED, "MissionManager is not initialized.");

            if (string.IsNullOrWhiteSpace(missionId))
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, "missionId is empty.");

            syncRuntimeSchedule();

            switch (missionType)
            {
                case MISSION_TYPE.DAILY:
                    return await claimDailyAsync(missionId, ct);

                case MISSION_TYPE.PERIOD:
                    return await claimPeriodAsync(missionId, ct);

                default:
                    return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Unsupported missionType: {missionType}");
            }
        }

        public void Notify(MESSAGE_MISSION_TYPE msgType)
        {
            _missionMessageSystem.Notify(msgType);
        }

        public void Notify(MESSAGE_MISSION_TYPE msgType, params object[] args)
        {
            _missionMessageSystem.Notify(msgType, args);
        }

        public void Subcribe(EntityId ownerKey, MESSAGE_MISSION_TYPE msgType, BaseTrigger<EntityId, MESSAGE_MISSION_TYPE>.Handler handler)
        {
            _missionMessageSystem.Subcribe(ownerKey, msgType, handler);
        }

        public void SubcribeOnce(EntityId ownerKey, MESSAGE_MISSION_TYPE msgType, Action<object[]> handler)
        {
            _missionMessageSystem.SubcribeOnce(ownerKey, msgType, handler);
        }

        public void UnSubcribe(EntityId ownerKey)
        {
            _missionMessageSystem.UnSubcribe(ownerKey);
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
            if (row == null || !row.IsActive || !row.ConditionValue.HasValue)
                return MissionRuntimeState.NONE;

            if (!TryResolveMessage(row.ConditionMsgId, out var message) || message.SaveType == MESSAGE_META_SAVE_TYPE.NONE)
                return MissionRuntimeState.NONE;

            var runtime = findDailyRuntime(missionId);
            if (runtime == null)
                return MissionRuntimeState.NONE;

            if (!string.Equals(runtime.periodKey, getCurrentDailyKey(), StringComparison.Ordinal))
                return MissionRuntimeState.NONE;

            return runtime.GetState();
        }

        MissionRuntimeState getPeriodRuntimeState(string missionId)
        {
            var row = TB_MISSION_PERIOD.Get(missionId);
            if (row == null || !row.IsActive || row.Day < 1 || row.Day > 7 || !row.ConditionValue.HasValue)
                return MissionRuntimeState.NONE;

            if (!TryResolveMessage(row.ConditionMsgId, out var message) || message.SaveType == MESSAGE_META_SAVE_TYPE.NONE)
                return MissionRuntimeState.NONE;

            var runtime = findPeriodRuntime(missionId);
            if (runtime == null)
                return MissionRuntimeState.NONE;

            if (!string.Equals(runtime.periodKey, getCurrentPeriodKey(), StringComparison.Ordinal))
                return MissionRuntimeState.NONE;

            return runtime.GetState();
        }

        async Task<CommonResult> claimDailyAsync(string missionId, CancellationToken ct)
        {
            var row = TB_MISSION_DAILY.Get(missionId);
            if (row == null || !row.IsActive || !row.ConditionValue.HasValue)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_FOUND, $"Daily mission not found: {missionId}");

            if (!TryResolveMessage(row.ConditionMsgId, out var message) || message.SaveType == MESSAGE_META_SAVE_TYPE.NONE)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_FOUND, $"Daily mission not found: {missionId}");

            var periodKey = getCurrentDailyKey();
            var runtime = findDailyRuntime(missionId);
            if (runtime == null)
                return CommonResult.Failure(CommonErrorType.MISSION_RUNTIME_MISSING, $"Daily runtime missing: {missionId}");

            if (!string.Equals(runtime.periodKey, periodKey, StringComparison.Ordinal))
                return CommonResult.Failure(CommonErrorType.MISSION_RUNTIME_STALE, $"Daily runtime stale: {missionId}/{runtime.periodKey}->{periodKey}");

            if (!runtime.IsClaimable)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_CLAIMABLE, $"Daily mission is not claimable: {missionId}");

            var apply = RewardManager.Instance.ApplyRewardGroup(row.RewardGroupId);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            runtime.MarkCompleted();
            _missionMessageSystem.Notify(MESSAGE_MISSION_TYPE.RUNTIME_REWARDED, runtime, apply.Value.AppliedRewards);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
            {
                Debug.LogError($"[{Tag}] Mission save failed: {save.Error}");
                return CommonResult.Failure(save.Error!);
            }

            return CommonResult.Ok();
        }

        async Task<CommonResult> claimPeriodAsync(string missionId, CancellationToken ct)
        {
            var row = TB_MISSION_PERIOD.Get(missionId);
            if (row == null || !row.IsActive || row.Day < 1 || row.Day > 7 || !row.ConditionValue.HasValue)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_FOUND, $"Period mission not found: {missionId}");

            if (!TryResolveMessage(row.ConditionMsgId, out var message) || message.SaveType == MESSAGE_META_SAVE_TYPE.NONE)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_FOUND, $"Period mission not found: {missionId}");

            var periodKey = getCurrentPeriodKey();
            var runtime = findPeriodRuntime(missionId);
            if (runtime == null)
                return CommonResult.Failure(CommonErrorType.MISSION_RUNTIME_MISSING, $"Period runtime missing: {missionId}");

            if (!string.Equals(runtime.periodKey, periodKey, StringComparison.Ordinal))
                return CommonResult.Failure(CommonErrorType.MISSION_RUNTIME_STALE, $"Period runtime stale: {missionId}/{runtime.periodKey}->{periodKey}");

            if (!runtime.IsClaimable)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_CLAIMABLE, $"Period mission is not claimable: {missionId}");

            var apply = RewardManager.Instance.ApplyRewardGroup(row.RewardGroupId);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            runtime.MarkCompleted();
            _missionMessageSystem.Notify(MESSAGE_MISSION_TYPE.RUNTIME_REWARDED, runtime, apply.Value.AppliedRewards);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
            {
                Debug.LogError($"[{Tag}] Mission save failed: {save.Error}");
                return CommonResult.Failure(save.Error!);
            }

            return CommonResult.Ok();
        }

        void onRuntimeInitialized(MissionRuntimeBase runtime)
        {
            _missionMessageSystem.Notify(MESSAGE_MISSION_TYPE.RUNTIME_INIT, runtime);
        }

        void subscribeRuntimeTrigger(int ownerKey, MESSAGE_META_TYPE messageType, BaseTrigger<int, MESSAGE_META_TYPE>.Handler handler)
        {
            MetaMessageManager.Instance.SubcribeGameMessageTrigger(ownerKey, messageType, handler);
        }

        void unSubcribeRuntimeTrigger(int ownerKey)
        {
            MetaMessageManager.Instance.UnSubcribeGameMessageTrigger(ownerKey);
        }

        void onRuntimeChanged(MissionRuntimeBase runtime)
        {
            _missionMessageSystem.Notify(MESSAGE_MISSION_TYPE.RUNTIME_PROGRESS, runtime);
        }

        void onRuntimeClaimable(MissionRuntimeBase runtime)
        {
            _missionMessageSystem.Notify(MESSAGE_MISSION_TYPE.RUNTIME_CLAIMABLE, runtime);
        }

        void detachAllRuntimes()
        {
            _scheduler.DetachAll();
        }

        void clearDailyScopeData()
        {
            _scheduler.ClearDailyScope();
            _missionMessageSystem.Notify(MESSAGE_MISSION_TYPE.DAY_RESET);
        }

        void rebuildRuntimeBindings()
        {
            var didResetDay = _scheduler.HasDailyRuntimeOutsideCurrentPeriod();
            _scheduler.RebuildBindings();

            if (didResetDay)
                _missionMessageSystem.Notify(MESSAGE_MISSION_TYPE.DAY_RESET);
        }

        void pruneExpiredMissionState()
        {
            _scheduler.PruneExpiredState();
        }

        MissionRuntimeDaily findDailyRuntime(string missionId)
        {
            return _scheduler.FindDaily(missionId);
        }

        MissionRuntimePeriod findPeriodRuntime(string missionId)
        {
            return _scheduler.FindPeriod(missionId);
        }

        static bool TryResolveMessage(string messageId, out MESSAGE_META message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            message = TB_MESSAGE_META.Get(messageId);
            return message != null;
        }

        void syncRuntimeSchedule()
        {
            var dailyExpired = GetRemainTime(MISSION_TYPE.DAILY) == TimeSpan.Zero
                               || _scheduler.HasDailyRuntimeOutsideCurrentPeriod();
            var periodExpired = GetRemainTime(MISSION_TYPE.PERIOD) == TimeSpan.Zero
                                || _scheduler.HasPeriodRuntimeOutsideCurrentPeriod();

            if (dailyExpired || periodExpired || _storage.runtimes.Count <= 0)
            {
                rebuildRuntimeBindings();
                pruneExpiredMissionState();
                return;
            }

            if (_scheduler.HasPeriodRuntimePendingActivation())
                _scheduler.TryActivatePeriodRuntimes();
        }

        TimeSpan getRemainTimeCore(long periodStartUtcMs, long periodDurationMs)
        {
            if (!TryGetServerNowUtcMs(out var serverNowUtcMs))
                return default;

            if (periodStartUtcMs <= 0L || periodDurationMs <= 0L)
                return default;

            var elapsedMs = Math.Max(0L, serverNowUtcMs - periodStartUtcMs);
            if (elapsedMs <= 0L)
                return TimeSpan.FromMilliseconds(periodDurationMs);

            var remainderMs = elapsedMs % periodDurationMs;
            if (remainderMs == 0L)
                return TimeSpan.Zero;

            var remainMs = periodDurationMs - remainderMs;
            return TimeSpan.FromMilliseconds(remainMs);
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

        string getCurrentPeriodKey()
        {
            return $"period:{getCurrentPeriodIndex()}";
        }

        int getCurrentPeriodIndex()
        {
            if (!TryGetServerNowUtcMs(out var estimatedServerNowUtcMs))
                return 0;

            if (_storage.periodMissionStartUtcMs <= 0L)
                return 0;

            var diff = Math.Max(0L, estimatedServerNowUtcMs - _storage.periodMissionStartUtcMs);
            return (int)(diff / PeriodMs);
        }

        int getCurrentPeriodElapsedDay()
        {
            if (!TryGetServerNowUtcMs(out var estimatedServerNowUtcMs))
                return 0;

            if (_storage.periodMissionStartUtcMs <= 0L)
                return 0;

            var diff = Math.Max(0L, estimatedServerNowUtcMs - _storage.periodMissionStartUtcMs);
            var remainder = diff % PeriodMs;
            return (int)(remainder / DayMs);
        }
    }
}
