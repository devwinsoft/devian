using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Achievement facade + runtime owner.
    /// Public API only accepts internal achievement IDs.
    /// </summary>
    public sealed class AchieveManager : CompoSingleton<AchieveManager>
    {
        private const string Tag = nameof(AchieveManager);
        private const int GameMessageOwnerKey = 3001001;

        private enum RuntimePlatformKind
        {
            Unsupported = 0,
            Apple = 1,
            Google = 2,
        }

        private sealed class AchievementMapEntry
        {
            public string achievementId = string.Empty;
            public bool isActive = true;
            public string appleAchievementId = string.Empty;
            public string googleAchievementId = string.Empty;

            public string InternalId => (achievementId ?? string.Empty).Trim();

            public string ResolvePlatformId(RuntimePlatformKind platform)
            {
                switch (platform)
                {
                    case RuntimePlatformKind.Apple:
                        return (appleAchievementId ?? string.Empty).Trim();
                    case RuntimePlatformKind.Google:
                        return (googleAchievementId ?? string.Empty).Trim();
                    default:
                        return string.Empty;
                }
            }
        }

        private readonly Dictionary<string, AchievementMapEntry> _achievementById
            = new Dictionary<string, AchievementMapEntry>(StringComparer.Ordinal);

        private readonly HashSet<string> _knownUnlockedAchievementIds
            = new HashSet<string>(StringComparer.Ordinal);

        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _initializeGate = new SemaphoreSlim(1, 1);

        private readonly AchieveStorage _storage = new();
        private readonly AchieveMessageTrigger _messageSystem = new();

        private IAchievePlatformAdapter _adapter;
        private bool _platformInitAttempted;
        private bool _platformInitialized;
        private CommonError _platformInitError;
        private bool _initialized;
        private bool _isGameMessageSubscribed;

        public AchieveStorage Storage => _storage;
        public bool IsInitialized => _initialized;

        public event Action<string> OnAchievementUnlocked;
        public event Action<AchieveRuntime> OnRuntimeInitialized;
        public event Action<AchieveRuntime> OnRuntimeProgress;
        public event Action<AchieveRuntime> OnRuntimeClaimable;
        public event Action<AchieveRuntime> OnRuntimeLevelUp;
        public event Action<AchieveRuntime, RewardData[]> OnRuntimeRewarded;

        protected override void onInitAwake()
        {
        }

        protected override void OnDestroy()
        {
            unSubcribeGameMessageTrigger();
            base.OnDestroy();
        }

        public async Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            await _initializeGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                    return CommonResult.Ok();

                subscribeGameMessageTrigger();
                rebuildMappingCaches();
                rebuildRuntimeBindings();

                var platformInit = await ensurePlatformInitializedAsync(ct);
                if (platformInit.IsFailure)
                    Debug.LogWarning($"[{Tag}] Platform init skipped: {platformInit.Error}");

                _initialized = true;
                return CommonResult.Ok();
            }
            finally
            {
                _initializeGate.Release();
            }
        }

        public void RefreshRuntimes()
        {
            if (!_initialized)
                return;

            rebuildMappingCaches();

            if (needsRuntimeRebuild())
            {
                rebuildRuntimeBindings();
                return;
            }

            if (_storage.runtimes.Count <= 0)
                return;

            var runtimes = new List<AchieveRuntime>(_storage.runtimes.Count);
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime != null)
                    runtimes.Add(runtime);
            }

            foreach (var runtime in runtimes)
                emitRuntimeInitialized(runtime);
        }

        bool needsRuntimeRebuild()
        {
            var groupKeys = new HashSet<string>(TB_ACHIEVE.GetGroupKeys(), StringComparer.Ordinal);
            var runtimeByAchieveId = new Dictionary<string, AchieveRuntime>(StringComparer.Ordinal);

            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime == null || string.IsNullOrWhiteSpace(runtime.achieveId))
                    return true;

                if (!groupKeys.Contains(runtime.achieveId))
                    return true;

                if (!runtimeByAchieveId.TryAdd(runtime.achieveId, runtime))
                    return true;

                var runtimeRow = findRow(runtime.achieveId, runtime.level);
                if (!isEligibleRow(runtimeRow))
                    return true;

                if (!string.Equals(runtime.messageId, runtimeRow!.ConditionMsgId, StringComparison.Ordinal))
                    return true;

                if (runtime.isCompleted && runtime.isWaiting)
                    return true;
            }

            foreach (var groupKey in groupKeys)
            {
                if (!runtimeByAchieveId.ContainsKey(groupKey))
                    return true;
            }

            return false;
        }

        public MissionRuntimeState GetRuntimeState(string achievementId)
        {
            if (string.IsNullOrWhiteSpace(achievementId))
                return MissionRuntimeState.NONE;

            var runtime = findRuntime(achievementId);
            return runtime != null
                ? runtime.GetState()
                : MissionRuntimeState.NONE;
        }

        public async Task<CommonResult> ClaimAsync(string achievementId, CancellationToken ct = default)
        {
            if (!_initialized)
                return CommonResult.Failure(CommonErrorType.SAVEDATA_SYNC_REQUIRED, "AchieveManager is not initialized.");

            if (string.IsNullOrWhiteSpace(achievementId))
                return CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, "achievementId is empty.");

            var runtime = findRuntime(achievementId);
            if (runtime == null)
                return CommonResult.Failure(CommonErrorType.MISSION_RUNTIME_MISSING, $"Achievement runtime missing: {achievementId}");

            if (!runtime.IsClaimable)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_CLAIMABLE, $"Achievement is not claimable: {achievementId}");

            var currentRow = findRow(runtime.achieveId, runtime.level);
            if (currentRow == null)
                return CommonResult.Failure(CommonErrorType.MISSION_NOT_FOUND, $"Achievement row not found: {achievementId}/{runtime.level}");

            var apply = RewardManager.Instance.ApplyRewardGroup(currentRow.RewardGroupId);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            var nextRow = findNextRow(runtime.achieveId, runtime.level);
            if (nextRow != null && isEligibleRow(nextRow))
            {
                if (hasActivationRequirement(nextRow, out _, out _))
                {
                    runtime.LevelUpToWaiting(nextRow.Level, nextRow.ConditionMsgId);
                    tryActivateRuntime(runtime, nextRow, GAME_MESSAGE_TYPE.NONE, CBigInt.Zero);
                }
                else if (TryResolveMessage(nextRow.ConditionMsgId, out var nextMessage))
                {
                    runtime.LevelUp(
                        nextRow.Level,
                        nextRow.ConditionMsgId,
                        nextMessage.MessageType,
                        nextMessage.SaveType,
                        nextRow.ConditionValue!.Value,
                        createExternalProgressReader(nextRow.ConditionMsgId, nextMessage.SaveType));
                }
                else
                {
                    runtime.MarkCompleted();
                }

                emitRuntimeLevelUp(runtime);
            }
            else
            {
                runtime.MarkCompleted();
            }

            emitRuntimeRewarded(runtime, apply.Value.AppliedRewards);

            var unlock = await UnlockAchievementAsync(runtime.achieveId, ct);
            if (unlock.IsFailure)
                Debug.LogWarning($"[{Tag}] Platform unlock failed: achieveId={runtime.achieveId}, error={unlock.Error}");

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
            {
                Debug.LogError($"[{Tag}] Save failed: {save.Error}");
                return CommonResult.Failure(save.Error!);
            }

            return CommonResult.Ok();
        }

        public void Notify(ACHIEVE_MESSAGE msgType)
        {
            _messageSystem.Notify(msgType);
        }

        public void Notify(ACHIEVE_MESSAGE msgType, params object[] args)
        {
            _messageSystem.Notify(msgType, args);
        }

        public void Subcribe(EntityId ownerKey, ACHIEVE_MESSAGE msgType, BaseTrigger<EntityId, ACHIEVE_MESSAGE>.Handler handler)
        {
            _messageSystem.Subcribe(ownerKey, msgType, handler);
        }

        public void SubcribeOnce(EntityId ownerKey, ACHIEVE_MESSAGE msgType, Action<object[]> handler)
        {
            _messageSystem.SubcribeOnce(ownerKey, msgType, handler);
        }

        public void UnSubcribe(EntityId ownerKey)
        {
            _messageSystem.UnSubcribe(ownerKey);
        }

        void subscribeGameMessageTrigger()
        {
            if (_isGameMessageSubscribed)
                return;

            var messageManager = GameMessageManager.Instance;

            foreach (GAME_MESSAGE_TYPE messageType in Enum.GetValues(typeof(GAME_MESSAGE_TYPE)))
            {
                if (messageType == GAME_MESSAGE_TYPE.NONE)
                    continue;

                var subscribedType = messageType;
                messageManager.SubcribeGameMessageTrigger(
                    GameMessageOwnerKey,
                    subscribedType,
                    args =>
                    {
                        onGameMessageTriggered(subscribedType, args);
                        return false;
                    });
            }

            _isGameMessageSubscribed = true;
        }

        void unSubcribeGameMessageTrigger()
        {
            if (!_isGameMessageSubscribed)
                return;

            GameMessageManager.Instance.UnSubcribeGameMessageTrigger(GameMessageOwnerKey);
            _isGameMessageSubscribed = false;
        }

        void onGameMessageTriggered(GAME_MESSAGE_TYPE messageType, object[] args)
        {
            if (args == null || args.Length <= 0)
                return;

            if (!tryParseMessageDelta(args[0], out var delta))
                return;

            onGameMessageNotify(messageType, delta);
        }

        static bool tryParseMessageDelta(object raw, out CBigInt delta)
        {
            switch (raw)
            {
                case CBigInt bigIntValue:
                    delta = bigIntValue;
                    return true;

                case int intValue:
                    delta = CBigInt.FromInt(intValue);
                    return true;

                case long longValue:
                    delta = CBigInt.FromLong(longValue);
                    return true;

                default:
                    delta = CBigInt.Zero;
                    return false;
            }
        }

        void onGameMessageNotify(GAME_MESSAGE_TYPE msgType, CBigInt msgValue)
        {
            tryActivateWaitingRuntimes(msgType, msgValue);
            notifyRuntimesByMessage(msgType, msgValue);
        }

        public async Task<CommonResult> UnlockAchievementAsync(string achievementId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            var platform = await ensurePlatformInitializedAsync(ct);
            if (platform.IsFailure)
                return platform;

            var resolve = tryResolveAchievement(achievementId, out var entry, out var platformAchievementId);
            if (resolve.IsFailure)
                return resolve;

            var unlock = await _adapter.UnlockAchievementAsync(platformAchievementId, ct);
            if (unlock.IsFailure)
                return unlock;

            if (markUnlockedIfNew(entry.InternalId))
                emitAchievementUnlocked(entry.InternalId);

            return CommonResult.Ok();
        }

        public async Task<CommonResult> SyncAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            var platform = await ensurePlatformInitializedAsync(ct);
            if (platform.IsFailure)
                return platform;

            var sync = await _adapter.FetchAchievementStatesAsync(ct);
            if (sync.IsFailure)
                return CommonResult.Failure(sync.Error!);

            var states = sync.Value ?? new Dictionary<string, bool>(StringComparer.Ordinal);
            var runtimePlatform = getRuntimePlatform();

            foreach (var kv in _achievementById)
            {
                var internalId = kv.Key;
                var entry = kv.Value;

                if (entry == null || !entry.isActive)
                    continue;

                var platformAchievementId = entry.ResolvePlatformId(runtimePlatform);
                if (string.IsNullOrEmpty(platformAchievementId))
                {
                    return CommonResult.Failure(
                        CommonErrorType.COMMON_INVALID_ARGUMENT,
                        $"Platform achievement ID mapping missing: {internalId}");
                }

                if (!states.TryGetValue(platformAchievementId, out var unlocked) || !unlocked)
                    continue;

                if (markUnlockedIfNew(internalId))
                    emitAchievementUnlocked(internalId);
            }

            return CommonResult.Ok();
        }

        public void ClearStorage()
        {
            detachAllRuntimes();
            _storage.Clear();
            _knownUnlockedAchievementIds.Clear();
            _initialized = false;
        }

        CommonResult ensureInitialized()
        {
            if (_initialized)
                return CommonResult.Ok();

            return CommonResult.Failure(
                CommonErrorType.COMMON_INVALID_ARGUMENT,
                "AchieveManager.InitializeAsync must be called before API use.");
        }

        async Task<CommonResult> ensurePlatformInitializedAsync(CancellationToken ct)
        {
            if (_platformInitialized)
                return CommonResult.Ok();

            if (_adapter == null)
                _adapter = createAdapter(getRuntimePlatform());

            if (_platformInitAttempted)
            {
                return _platformInitError != null
                    ? CommonResult.Failure(_platformInitError)
                    : CommonResult.Failure(CommonErrorType.COMMON_UNKNOWN, "Achieve platform initialization failed previously.");
            }

            _platformInitAttempted = true;
            var init = await _adapter.InitializeAsync(ct);
            if (init.IsFailure)
            {
                _platformInitError = init.Error;
                return CommonResult.Failure(init.Error!);
            }

            _platformInitialized = true;
            _platformInitError = null;
            return CommonResult.Ok();
        }

        CommonResult tryResolveAchievement(
            string achievementId,
            out AchievementMapEntry entry,
            out string platformAchievementId)
        {
            entry = null;
            platformAchievementId = string.Empty;

            if (string.IsNullOrWhiteSpace(achievementId))
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    "achievementId is empty.");
            }

            if (!_achievementById.TryGetValue(achievementId.Trim(), out entry) || entry == null || !entry.isActive)
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"Active achievement mapping not found: {achievementId}");
            }

            platformAchievementId = entry.ResolvePlatformId(getRuntimePlatform());
            if (string.IsNullOrEmpty(platformAchievementId))
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"Platform achievement ID mapping missing: {achievementId}");
            }

            return CommonResult.Ok();
        }

        bool markUnlockedIfNew(string achievementId)
        {
            lock (_stateLock)
            {
                return _knownUnlockedAchievementIds.Add(achievementId);
            }
        }

        void emitAchievementUnlocked(string achievementId)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE.ACHIEVEMENT_UNLOCKED, achievementId);

            var handler = OnAchievementUnlocked;
            if (handler == null)
                return;

            try
            {
                handler.Invoke(achievementId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] OnAchievementUnlocked listener threw: {ex.Message}");
            }
        }

        void emitRuntimeInitialized(AchieveRuntime runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE.RUNTIME_INIT, runtime);

            var handler = OnRuntimeInitialized;
            if (handler == null)
                return;

            try
            {
                handler.Invoke(runtime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] OnRuntimeInitialized listener threw: {ex.Message}");
            }
        }

        void emitRuntimeProgress(AchieveRuntime runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE.RUNTIME_PROGRESS, runtime);

            var handler = OnRuntimeProgress;
            if (handler == null)
                return;

            try
            {
                handler.Invoke(runtime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] OnRuntimeProgress listener threw: {ex.Message}");
            }
        }

        void emitRuntimeClaimable(AchieveRuntime runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE.RUNTIME_CLAIMABLE, runtime);

            var handler = OnRuntimeClaimable;
            if (handler == null)
                return;

            try
            {
                handler.Invoke(runtime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] OnRuntimeClaimable listener threw: {ex.Message}");
            }
        }

        void emitRuntimeLevelUp(AchieveRuntime runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE.RUNTIME_LEVEL_UP, runtime);

            var handler = OnRuntimeLevelUp;
            if (handler == null)
                return;

            try
            {
                handler.Invoke(runtime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] OnRuntimeLevelUp listener threw: {ex.Message}");
            }
        }

        void emitRuntimeRewarded(AchieveRuntime runtime, RewardData[] rewards)
        {
            var safeRewards = rewards ?? Array.Empty<RewardData>();
            _messageSystem.Notify(ACHIEVE_MESSAGE.RUNTIME_REWARDED, runtime, safeRewards);

            var handler = OnRuntimeRewarded;
            if (handler == null)
                return;

            try
            {
                handler.Invoke(runtime, safeRewards);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] OnRuntimeRewarded listener threw: {ex.Message}");
            }
        }

        void rebuildMappingCaches()
        {
            _achievementById.Clear();
            foreach (var achieveId in TB_ACHIEVE.GetGroupKeys())
            {
                var mappingRow = findAchievementMappingRow(achieveId);
                if (mappingRow == null)
                    continue;

                var entry = createAchievementMapEntry(mappingRow);
                var id = entry.InternalId;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (_achievementById.ContainsKey(id))
                    Debug.LogWarning($"[{Tag}] Duplicate achievementId mapping. override id={id}");

                _achievementById[id] = entry;
            }
        }

        static ACHIEVE findAchievementMappingRow(string achieveId)
        {
            var levelOne = findStartRow(achieveId);
            if (levelOne != null)
                return levelOne;

            foreach (var row in TB_ACHIEVE.GetByGroup(achieveId))
                return row;

            return null;
        }

        static AchievementMapEntry createAchievementMapEntry(ACHIEVE row)
        {
            var achieveId = (row?.AchieveId ?? string.Empty).Trim();
            return new AchievementMapEntry
            {
                achievementId = achieveId,
                isActive = row != null && row.IsActive,
                appleAchievementId = (row?.AppleAchievementId ?? string.Empty).Trim(),
                googleAchievementId = (row?.GoogleAchievementId ?? string.Empty).Trim(),
            };
        }

        void rebuildRuntimeBindings()
        {
            detachAllRuntimes();
            ensureAchievementRuntimes();
        }

        void detachAllRuntimes()
        {
            foreach (var runtime in _storage.runtimes.Values)
                runtime?.Detach();
        }

        void ensureAchievementRuntimes()
        {
            var groupKeys = new HashSet<string>(TB_ACHIEVE.GetGroupKeys(), StringComparer.Ordinal);
            var seenAchieveIds = new HashSet<string>(StringComparer.Ordinal);
            var removeKeys = new List<int>();

            foreach (var kv in _storage.runtimes)
            {
                var runtime = kv.Value;
                if (runtime == null
                    || string.IsNullOrWhiteSpace(runtime.achieveId)
                    || !groupKeys.Contains(runtime.achieveId)
                    || !seenAchieveIds.Add(runtime.achieveId))
                {
                    removeKeys.Add(kv.Key);
                }
            }

            foreach (var key in removeKeys)
            {
                if (_storage.runtimes.TryGetValue(key, out var runtime) && runtime != null)
                    runtime.Detach();
                _storage.runtimes.Remove(key);
            }

            foreach (var groupKey in groupKeys)
            {
                var existing = findRuntime(groupKey);
                if (existing != null)
                {
                    var row = findRow(existing.achieveId, existing.level);
                    if (isEligibleRow(row))
                    {
                        var restored = restoreRuntimeForRow(existing, row!);
                        if (restored != null)
                        {
                            _storage.runtimes[restored.achieveUid] = restored;
                            emitRuntimeInitialized(restored);
                            continue;
                        }
                    }

                    existing.Detach();
                    _storage.runtimes.Remove(existing.achieveUid);
                }

                var startRow = findStartRow(groupKey);
                if (startRow == null)
                {
                    if (TB_ACHIEVE.GetByGroup(groupKey).Count > 0)
                        Debug.LogError($"[{Tag}] Missing level=1 achieve row for achieveId='{groupKey}'.");
                    continue;
                }

                if (!isEligibleRow(startRow))
                    continue;

                var created = createRuntimeForRow(startRow);
                if (created == null)
                    continue;

                _storage.runtimes[created.achieveUid] = created;
                emitRuntimeInitialized(created);
            }

            // TOTAL_* requirement rows can be activated immediately from stored message stats.
            tryActivateWaitingRuntimes(GAME_MESSAGE_TYPE.NONE, CBigInt.Zero);
        }

        void onRuntimeChanged(AchieveRuntime runtime)
        {
            emitRuntimeProgress(runtime);
        }

        void onRuntimeClaimable(AchieveRuntime runtime)
        {
            emitRuntimeClaimable(runtime);
        }

        AchieveRuntime restoreRuntimeForRow(AchieveRuntime existing, ACHIEVE row)
        {
            if (existing == null || row == null)
                return null;

            if (existing.isWaiting && !existing.isCompleted)
            {
                return AchieveRuntimeFactory.Restore(new AchieveRuntimeRestoreArgs
                {
                    AchieveId = existing.achieveId,
                    MessageId = row.ConditionMsgId,
                    AchieveUid = existing.achieveUid,
                    Level = existing.level,
                    IsWaiting = true,
                    ProgressValue = CBigInt.Zero,
                    IsCompleted = false,
                    StatType = GAME_MESSAGE_TYPE.NONE,
                    OpType = GAME_MESSAGE_SAVE_TYPE.NONE,
                    ConditionValue = CBigInt.Zero,
                    ReadProgress = null,
                    OnChanged = onRuntimeChanged,
                    OnClaimable = onRuntimeClaimable,
                });
            }

            if (!TryResolveMessage(row.ConditionMsgId, out var message))
            {
                Debug.LogError($"[{Tag}] MESSAGE not found for achieve: achieveId='{row.AchieveId}', conditionMsgId='{row.ConditionMsgId}'.");
                return null;
            }

            return AchieveRuntimeFactory.Restore(new AchieveRuntimeRestoreArgs
            {
                AchieveId = existing.achieveId,
                MessageId = row.ConditionMsgId,
                AchieveUid = existing.achieveUid,
                Level = existing.level,
                IsWaiting = false,
                ProgressValue = existing.progressValue,
                IsCompleted = existing.isCompleted,
                StatType = message.MessageType,
                OpType = message.SaveType,
                ConditionValue = row.ConditionValue!.Value,
                ReadProgress = createExternalProgressReader(row.ConditionMsgId, message.SaveType),
                OnChanged = onRuntimeChanged,
                OnClaimable = onRuntimeClaimable,
            });
        }

        AchieveRuntime createRuntimeForRow(ACHIEVE row)
        {
            if (row == null)
                return null;

            var achieveUid = _storage.AllocateAchieveUid();
            if (hasActivationRequirement(row, out _, out _))
            {
                return AchieveRuntimeFactory.Create(new AchieveRuntimeCreateArgs
                {
                    AchieveId = row.AchieveId,
                    MessageId = row.ConditionMsgId,
                    Level = row.Level,
                    AchieveUid = achieveUid,
                    IsWaiting = true,
                    StatType = GAME_MESSAGE_TYPE.NONE,
                    OpType = GAME_MESSAGE_SAVE_TYPE.NONE,
                    ConditionValue = CBigInt.Zero,
                    ReadProgress = null,
                    OnChanged = onRuntimeChanged,
                    OnClaimable = onRuntimeClaimable,
                });
            }

            if (!TryResolveMessage(row.ConditionMsgId, out var message))
            {
                Debug.LogError($"[{Tag}] MESSAGE not found for achieve: achieveId='{row.AchieveId}', conditionMsgId='{row.ConditionMsgId}'.");
                return null;
            }

            return AchieveRuntimeFactory.Create(new AchieveRuntimeCreateArgs
            {
                AchieveId = row.AchieveId,
                MessageId = row.ConditionMsgId,
                Level = row.Level,
                AchieveUid = achieveUid,
                IsWaiting = false,
                StatType = message.MessageType,
                OpType = message.SaveType,
                ConditionValue = row.ConditionValue!.Value,
                ReadProgress = createExternalProgressReader(row.ConditionMsgId, message.SaveType),
                OnChanged = onRuntimeChanged,
                OnClaimable = onRuntimeClaimable,
            });
        }

        void tryActivateWaitingRuntimes(GAME_MESSAGE_TYPE triggeredType, CBigInt triggeredValue)
        {
            if (_storage.runtimes.Count <= 0)
                return;

            var runtimes = new List<AchieveRuntime>(_storage.runtimes.Count);
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime != null)
                    runtimes.Add(runtime);
            }

            foreach (var runtime in runtimes)
            {
                if (runtime == null || !runtime.isWaiting || runtime.isCompleted)
                    continue;

                var row = findRow(runtime.achieveId, runtime.level);
                if (!isEligibleRow(row))
                    continue;

                tryActivateRuntime(runtime, row!, triggeredType, triggeredValue);
            }
        }

        bool tryActivateRuntime(AchieveRuntime runtime, ACHIEVE row, GAME_MESSAGE_TYPE triggeredType, CBigInt triggeredValue)
        {
            if (runtime == null || row == null || runtime.isCompleted || !runtime.isWaiting)
                return false;

            if (hasActivationRequirement(row, out var reqMessage, out var reqValue)
                && !isRequirementSatisfied(row, reqMessage, reqValue, triggeredType, triggeredValue))
            {
                return false;
            }

            if (!TryResolveMessage(row.ConditionMsgId, out var conditionMessage))
            {
                Debug.LogError($"[{Tag}] MESSAGE not found for achieve: achieveId='{row.AchieveId}', conditionMsgId='{row.ConditionMsgId}'.");
                return false;
            }

            runtime.Bind(
                row.ConditionMsgId,
                conditionMessage.MessageType,
                conditionMessage.SaveType,
                row.ConditionValue!.Value,
                createExternalProgressReader(row.ConditionMsgId, conditionMessage.SaveType),
                onRuntimeChanged,
                onRuntimeClaimable);
            emitRuntimeProgress(runtime);
            return true;
        }

        void notifyRuntimesByMessage(GAME_MESSAGE_TYPE messageType, CBigInt messageDelta)
        {
            if (_storage.runtimes.Count <= 0)
                return;

            var runtimes = new List<AchieveRuntime>(_storage.runtimes.Count);
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime != null)
                    runtimes.Add(runtime);
            }

            foreach (var runtime in runtimes)
                runtime.OnMessageStatUpdated(messageType, messageDelta);
        }

        AchieveRuntime findRuntime(string achieveId)
        {
            AchieveRuntime found = null;
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime == null)
                    continue;

                if (!string.Equals(runtime.achieveId, achieveId, StringComparison.Ordinal))
                    continue;

                if (found != null)
                {
                    Debug.LogError($"[{Tag}] Duplicate achieve runtime detected for achieveId='{achieveId}'.");
                    return found;
                }

                found = runtime;
            }

            return found;
        }

        static bool isEligibleRow(ACHIEVE row)
        {
            return row != null
                   && row.IsActive
                   && row.ConditionValue.HasValue
                   && TryResolveMessage(row.ConditionMsgId, out var message)
                   && message.SaveType != GAME_MESSAGE_SAVE_TYPE.NONE;
        }

        bool hasActivationRequirement(ACHIEVE row, out MESSAGE reqMessage, out CBigInt reqValue)
        {
            reqMessage = null;
            reqValue = CBigInt.Zero;

            if (row == null
                || string.IsNullOrWhiteSpace(row.ReqMsgId)
                || !row.ReqValue.HasValue)
            {
                return false;
            }

            if (!TryResolveMessage(row.ReqMsgId, out reqMessage) || reqMessage.SaveType == GAME_MESSAGE_SAVE_TYPE.NONE)
            {
                Debug.LogError($"[{Tag}] Invalid req message for achieve: achieveId='{row.AchieveId}', reqMsgId='{row.ReqMsgId}'.");
                reqMessage = null;
                return false;
            }

            reqValue = row.ReqValue.Value;
            return true;
        }

        static bool isRequirementSatisfied(
            ACHIEVE row,
            MESSAGE reqMessage,
            CBigInt reqValue,
            GAME_MESSAGE_TYPE triggeredType,
            CBigInt triggeredValue)
        {
            if (row == null || reqMessage == null)
                return false;

            if (isTotalSaveType(reqMessage.SaveType))
            {
                if (!GameMessageManager.TryGet(out var messageManager) || messageManager == null)
                    return false;

                return messageManager.GetStat(row.ReqMsgId) >= reqValue;
            }

            if (triggeredType == GAME_MESSAGE_TYPE.NONE || reqMessage.MessageType != triggeredType)
                return false;

            return triggeredValue >= reqValue;
        }

        static bool isTotalSaveType(GAME_MESSAGE_SAVE_TYPE saveType)
        {
            return saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX;
        }

        static ACHIEVE findRow(string achieveId, int level)
        {
            foreach (var row in TB_ACHIEVE.GetByGroup(achieveId))
            {
                if (row.Level == level)
                    return row;
            }

            return null;
        }

        static ACHIEVE findStartRow(string achieveId)
        {
            foreach (var row in TB_ACHIEVE.GetByGroup(achieveId))
            {
                if (row.Level == 1)
                    return row;
            }

            return null;
        }

        static ACHIEVE findNextRow(string achieveId, int level)
        {
            ACHIEVE next = null;
            foreach (var row in TB_ACHIEVE.GetByGroup(achieveId))
            {
                if (row.Level <= level)
                    continue;

                if (next == null || row.Level < next.Level)
                    next = row;
            }

            return next;
        }

        static bool TryResolveMessage(string messageId, out MESSAGE message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            message = TB_MESSAGE.Get(messageId);
            return message != null;
        }

        static Func<CBigInt> createExternalProgressReader(string messageId, GAME_MESSAGE_SAVE_TYPE saveType)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return null;

            if (saveType != GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM
                && saveType != GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX)
            {
                return null;
            }

            var key = messageId;
            return () =>
            {
                if (!GameMessageManager.TryGet(out var messageManager) || messageManager == null)
                    return CBigInt.Zero;

                return messageManager.GetStat(key);
            };
        }

        static RuntimePlatformKind getRuntimePlatform()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return RuntimePlatformKind.Apple;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return RuntimePlatformKind.Google;
#else
            return RuntimePlatformKind.Unsupported;
#endif
        }

        static IAchievePlatformAdapter createAdapter(RuntimePlatformKind platform)
        {
            switch (platform)
            {
                case RuntimePlatformKind.Apple:
                    return new AppleAchievePlatformAdapter();
                case RuntimePlatformKind.Google:
                    return new GoogleAchievePlatformAdapter();
                default:
                    return new UnsupportedAchievePlatformAdapter();
            }
        }

    }
}
