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
        const long ResetAnchorThresholdMs = 7L * DayMs;
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
                getCurrentDailyPeriodIndex);
        }


        public async Task<CommonResult> InitializeAsync(
            MissionClockSnapshot preloadedClock = null, CancellationToken ct = default)
        {
            var refresh = preloadedClock != null
                ? applyClockSnapshot(preloadedClock)
                : await RefreshClockAsync(ct);
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
            return applyClockSnapshot(clock.Value);
        }

        public void RefreshRuntimes()
        {
            if (!_initialized)
                return;

            var dayExpired = GetRemainTime(MISSION_TYPE.DAY) == TimeSpan.Zero
                             || _scheduler.HasDailyRuntimeOutsideCurrentPeriod();
            if (dayExpired)
            {
                rebuildRuntimeBindings();
                pruneExpiredMissionState();
                return;
            }

            if (_storage.runtimes.Count <= 0)
                return;

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
            var snapshot = _storage.clockSnapshot;
            if (snapshot == null || snapshot.serverNowUtcMs <= 0L)
                return false;

            var clientNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            serverNowUtcMs = snapshot.serverNowUtcMs + Math.Max(0L, clientNowUtcMs - _storage.clockReceivedAtClientUtcMs);
            return true;
        }

        public MissionRuntimeState GetMissionRuntimeState(MISSION_TYPE missionType, string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return MissionRuntimeState.NONE;

            switch (missionType)
            {
                case MISSION_TYPE.DAY:
                    return getDailyRuntimeState(missionId);

                default:
                    return MissionRuntimeState.NONE;
            }
        }

        public TimeSpan GetRemainTime(MISSION_TYPE missionType)
        {
            switch (missionType)
            {
                case MISSION_TYPE.DAY:
                    if (!TryGetServerNowUtcMs(out var serverNowUtcMs))
                        return default;

                    if (_storage.dailyMissionStartUtcMs <= 0L)
                        return default;

                    var elapsedMs = Math.Max(0L, serverNowUtcMs - _storage.dailyMissionStartUtcMs);
                    if (elapsedMs <= 0L)
                        return TimeSpan.FromMilliseconds(DayMs);

                    var remainderMs = elapsedMs % DayMs;
                    if (remainderMs == 0L)
                        return TimeSpan.Zero;

                    var remainMs = DayMs - remainderMs;

                    return TimeSpan.FromMilliseconds(remainMs);

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

            switch (missionType)
            {
                case MISSION_TYPE.DAY:
                    return await claimDailyAsync(missionId, ct);

                default:
                    return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, $"Unsupported missionType: {missionType}");
            }
        }

        public void Notify(MISSION_MESSAGE msgType)
        {
            _missionMessageSystem.Notify(msgType);
        }

        public void Notify(MISSION_MESSAGE msgType, params object[] args)
        {
            _missionMessageSystem.Notify(msgType, args);
        }

        public void Subcribe(EntityId ownerKey, MISSION_MESSAGE msgType, BaseTrigger<EntityId, MISSION_MESSAGE>.Handler handler)
        {
            _missionMessageSystem.Subcribe(ownerKey, msgType, handler);
        }

        public void SubcribeOnce(EntityId ownerKey, MISSION_MESSAGE msgType, Action<object[]> handler)
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
            var row = TB_MISSION.Get(missionId);
            if (row == null || !row.IsActive || !row.ConditionValue.HasValue)
                return MissionRuntimeState.NONE;

            if (!TryResolveMessage(row.MessageId, out var message) || message.SaveType == GAME_MESSAGE_SAVE_TYPE.NONE)
                return MissionRuntimeState.NONE;

            var runtime = findDailyRuntime(missionId);
            if (runtime == null)
                return MissionRuntimeState.NONE;

            if (!string.Equals(runtime.periodKey, getCurrentDailyKey(), StringComparison.Ordinal))
                return MissionRuntimeState.NONE;

            return runtime.GetState();
        }

        async Task<CommonResult> claimDailyAsync(string missionId, CancellationToken ct)
        {
            var row = TB_MISSION.Get(missionId);
            if (row == null || !row.IsActive || !row.ConditionValue.HasValue)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_FOUND, $"Daily mission not found: {missionId}");

            if (!TryResolveMessage(row.MessageId, out var message) || message.SaveType == GAME_MESSAGE_SAVE_TYPE.NONE)
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
            _missionMessageSystem.Notify(MISSION_MESSAGE.RUNTIME_REWARDED, runtime, apply.Value.AppliedRewards);

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
            _missionMessageSystem.Notify(MISSION_MESSAGE.RUNTIME_INIT, runtime);
        }

        void subscribeRuntimeTrigger(int ownerKey, GAME_MESSAGE_TYPE messageType, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler handler)
        {
            GameMessageManager.Instance.SubcribeGameMessageTrigger(ownerKey, messageType, handler);
        }

        void unSubcribeRuntimeTrigger(int ownerKey)
        {
            GameMessageManager.Instance.UnSubcribeGameMessageTrigger(ownerKey);
        }

        void onRuntimeChanged(MissionRuntimeBase runtime)
        {
            _missionMessageSystem.Notify(MISSION_MESSAGE.RUNTIME_PROGRESS, runtime);
        }

        void onRuntimeClaimable(MissionRuntimeBase runtime)
        {
            _missionMessageSystem.Notify(MISSION_MESSAGE.RUNTIME_CLAIMABLE, runtime);
        }

        void detachAllRuntimes()
        {
            _scheduler.DetachAll();
        }

        void clearDailyScopeData()
        {
            _scheduler.ClearDailyScope();
            _missionMessageSystem.Notify(MISSION_MESSAGE.DAY_RESET);
        }

        void rebuildRuntimeBindings()
        {
            var didResetDay = _scheduler.HasDailyRuntimeOutsideCurrentPeriod();
            _scheduler.RebuildBindings();
            if (didResetDay)
                _missionMessageSystem.Notify(MISSION_MESSAGE.DAY_RESET);
        }

        void pruneExpiredMissionState()
        {
            _scheduler.PruneExpiredState();
        }

        MissionRuntimeDaily findDailyRuntime(string missionId)
        {
            return _scheduler.FindDaily(missionId);
        }

        static bool TryResolveMessage(string messageId, out MESSAGE message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            message = TB_MESSAGE.Get(messageId);
            return message != null;
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

        Task<CommonResult<MissionClockSnapshot>> getMissionClockAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            return Task.FromResult(CommonResult<MissionClockSnapshot>.Success(
                new MissionClockSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
#else
            return FirebaseManager.Instance.GetMissionClockAsync(ct);
#endif
        }

        CommonResult<MissionClockSnapshot> applyClockSnapshot(MissionClockSnapshot snapshot)
        {
            _storage.clockSnapshot = snapshot;
            _storage.clockReceivedAtClientUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_initialized)
            {
                rebuildRuntimeBindings();
                pruneExpiredMissionState();
            }

            return CommonResult<MissionClockSnapshot>.Success(snapshot);
        }

    }
}
