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
        private static readonly EntityId InventoryMessageOwnerKey = 3001002;

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

        private sealed class AchieveTableRow
        {
            public ACHIEVE_TYPE achieveType;
            public string AchieveId = string.Empty;
            public bool IsActive;
            public int Level;
            public int OrderNum;
            public string ReqMsgId = string.Empty;
            public CBigInt? ReqValue;
            public string ReqPassId = string.Empty;
            public string ReqSeasonId = string.Empty;
            public string ConditionMsgId = string.Empty;
            public GAME_MESSAGE_OP_TYPE ConditionOp = GAME_MESSAGE_OP_TYPE.GTE;
            public CBigInt? ConditionValue;
            public string RewardGroupId = string.Empty;
            public string AppleAchievementId = string.Empty;
            public string GoogleAchievementId = string.Empty;
        }

        private readonly Dictionary<string, AchievementMapEntry> _achievementById
            = new Dictionary<string, AchievementMapEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<AchieveTableRow>> _rowsByAchieveId
            = new Dictionary<string, List<AchieveTableRow>>(StringComparer.Ordinal);

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
        private bool _isInventoryMessageSubscribed;

        public AchieveStorage Storage => _storage;
        public bool IsInitialized => _initialized;

        public event Action<string> OnAchievementUnlocked;
        public event Action<AchieveRuntimeBase> OnRuntimeInitialized;
        public event Action<AchieveRuntimeBase> OnRuntimeActive;
        public event Action<AchieveRuntimeBase> OnRuntimeProgress;
        public event Action<AchieveRuntimeBase> OnRuntimeClaimable;
        public event Action<AchieveRuntimeBase> OnRuntimeLevelUp;
        public event Action<AchieveRuntimeBase, RewardData[]> OnRuntimeRewarded;

        protected override void onInitAwake()
        {
        }

        protected override void onDestroy()
        {
            if (!BaseApplication.IsApplicationQuitting)
            {
                unSubcribeGameMessageTrigger();
                unSubcribeInventoryMessageTrigger();
            }
        }

        public async Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            await _initializeGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                    return CommonResult.Ok();

                subscribeGameMessageTrigger();
                subscribeInventoryMessageTrigger();
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

            var runtimes = new List<AchieveRuntimeBase>(_storage.runtimes.Count);
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
            var groupKeys = new HashSet<string>(_rowsByAchieveId.Keys, StringComparer.Ordinal);
            var runtimeByAchieveId = new Dictionary<string, AchieveRuntimeBase>(StringComparer.Ordinal);

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

                if (runtime.RuntimeType != runtimeRow!.achieveType)
                    return true;

                if (runtime.state == MissionRuntimeState.NONE)
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
                return CommonResult.Failure(COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED, "AchieveManager is not initialized.");

            if (string.IsNullOrWhiteSpace(achievementId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT, "achievementId is empty.");

            var runtime = findRuntime(achievementId);
            if (runtime == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.MISSION_RUNTIME_MISSING, $"Achievement runtime missing: {achievementId}");

            if (!runtime.IsClaimable)
                return CommonResult.Failure(COMMON_ERROR_TYPE.MISSION_NOT_CLAIMABLE, $"Achievement is not claimable: {achievementId}");

            var currentRow = findRow(runtime.achieveId, runtime.level);
            if (currentRow == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.MISSION_NOT_FOUND, $"Achievement row not found: {achievementId}/{runtime.level}");

            var apply = RewardManager.Instance.ApplyRewardGroup(currentRow.RewardGroupId);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            var nextRow = findNextRow(runtime.achieveId, runtime.level);
            if (nextRow != null && isEligibleRow(nextRow))
            {
                if (hasActivationRequirement(nextRow, out _, out _, out _, out _, out _))
                {
                    runtime.LevelUpToWaiting(nextRow.Level, toRuntimeIndex(nextRow));
                    tryActivateRuntime(runtime, nextRow, GAME_MESSAGE_TYPE.NONE, CBigInt.Zero);
                }
                else if (tryResolveRuntimeBinding(nextRow, true, out var nextStatType, out var nextOpType, out var nextConditionOpType, out var nextConditionValue, out var nextReader))
                {
                    runtime.LevelUp(
                        nextRow.Level,
                        toRuntimeIndex(nextRow),
                        nextStatType,
                        nextOpType,
                        nextConditionOpType,
                        nextConditionValue,
                        nextReader);
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

            if (runtime.RuntimeType == ACHIEVE_TYPE.SOCIAL)
            {
                var unlock = await UnlockAchievementAsync(runtime.achieveId, ct);
                if (unlock.IsFailure)
                    Debug.LogWarning($"[{Tag}] Platform unlock failed: achieveId={runtime.achieveId}, error={unlock.Error}");
            }

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
            {
                Debug.LogError($"[{Tag}] Save failed: {save.Error}");
                return CommonResult.Failure(save.Error!);
            }

            return CommonResult.Ok();
        }

        public void Notify(ACHIEVE_MESSAGE_TYPE msgType)
        {
            _messageSystem.Notify(msgType);
        }

        public void Notify(ACHIEVE_MESSAGE_TYPE msgType, params object[] args)
        {
            _messageSystem.Notify(msgType, args);
        }

        public void Subcribe(EntityId ownerKey, ACHIEVE_MESSAGE_TYPE msgType, BaseTrigger<EntityId, ACHIEVE_MESSAGE_TYPE>.Handler handler)
        {
            _messageSystem.Subcribe(ownerKey, msgType, handler);
        }

        public void SubcribeOnce(EntityId ownerKey, ACHIEVE_MESSAGE_TYPE msgType, Action<object[]> handler)
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

        void subscribeInventoryMessageTrigger()
        {
            if (_isInventoryMessageSubscribed)
                return;

            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
                return;

            inventoryManager.Subcribe(
                InventoryMessageOwnerKey,
                INVENTORY_MESSAGE_TYPE.PASS_CHANGED,
                args =>
                {
                    onInventoryPassChanged(args);
                    return false;
                });

            _isInventoryMessageSubscribed = true;
        }

        void unSubcribeInventoryMessageTrigger()
        {
            if (!_isInventoryMessageSubscribed)
                return;

            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager != null)
                inventoryManager.UnSubcribe(InventoryMessageOwnerKey);

            _isInventoryMessageSubscribed = false;
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

        void onInventoryPassChanged(object[] args)
        {
            if (args == null || args.Length <= 0)
                return;

            if (args[0] is not string)
                return;

            tryActivateWaitingRuntimes(GAME_MESSAGE_TYPE.NONE, CBigInt.Zero);
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
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
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
                COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
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
                    : CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_UNKNOWN, "Achieve platform initialization failed previously.");
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
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "achievementId is empty.");
            }

            if (!_achievementById.TryGetValue(achievementId.Trim(), out entry) || entry == null || !entry.isActive)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Active achievement mapping not found: {achievementId}");
            }

            platformAchievementId = entry.ResolvePlatformId(getRuntimePlatform());
            if (string.IsNullOrEmpty(platformAchievementId))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
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
            _messageSystem.Notify(ACHIEVE_MESSAGE_TYPE.RUNTIME_UNLOCKED, achievementId);

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

        void emitRuntimeInitialized(AchieveRuntimeBase runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE_TYPE.RUNTIME_INIT, runtime);

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

        void emitRuntimeProgress(AchieveRuntimeBase runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE_TYPE.RUNTIME_PROGRESS, runtime);

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

        void emitRuntimeActive(AchieveRuntimeBase runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE_TYPE.RUNTIME_ACTIVE, runtime);

            var handler = OnRuntimeActive;
            if (handler == null)
                return;

            try
            {
                handler.Invoke(runtime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] OnRuntimeActive listener threw: {ex.Message}");
            }
        }

        void emitRuntimeClaimable(AchieveRuntimeBase runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE_TYPE.RUNTIME_CLAIMABLE, runtime);

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

        void emitRuntimeLevelUp(AchieveRuntimeBase runtime)
        {
            _messageSystem.Notify(ACHIEVE_MESSAGE_TYPE.RUNTIME_LEVEL_UP, runtime);

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

        void emitRuntimeRewarded(AchieveRuntimeBase runtime, RewardData[] rewards)
        {
            var safeRewards = rewards ?? Array.Empty<RewardData>();
            _messageSystem.Notify(ACHIEVE_MESSAGE_TYPE.RUNTIME_REWARDED, runtime, safeRewards);

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
            rebuildAchieveRows();

            _achievementById.Clear();
            foreach (var achieveId in _rowsByAchieveId.Keys)
            {
                var mappingRow = findAchievementMappingRow(achieveId);
                if (mappingRow == null || mappingRow.achieveType != ACHIEVE_TYPE.SOCIAL)
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

        void rebuildAchieveRows()
        {
            _rowsByAchieveId.Clear();

            foreach (var row in TB_ACHIEVE_SOCIAL.GetAll())
            {
                addAchieveRow(new AchieveTableRow
                {
                    achieveType = ACHIEVE_TYPE.SOCIAL,
                    AchieveId = row.AchieveId ?? string.Empty,
                    IsActive = row.IsActive,
                    Level = row.Level,
                    OrderNum = row.OrderNum,
                    ReqMsgId = row.ReqMsgId ?? string.Empty,
                    ReqValue = row.ReqValue,
                    ReqPassId = string.Empty,
                    ReqSeasonId = string.Empty,
                    ConditionMsgId = row.ConditionMsgId ?? string.Empty,
                    ConditionOp = row.ConditionOp,
                    ConditionValue = row.ConditionValue,
                    RewardGroupId = row.RewardGroupId ?? string.Empty,
                    AppleAchievementId = row.AppleAchievementId ?? string.Empty,
                    GoogleAchievementId = row.GoogleAchievementId ?? string.Empty,
                });
            }

            foreach (var row in TB_ACHIEVE_PASS.GetAll())
            {
                addAchieveRow(new AchieveTableRow
                {
                    achieveType = ACHIEVE_TYPE.PASS,
                    AchieveId = row.AchieveId ?? string.Empty,
                    IsActive = row.IsActive,
                    Level = row.Level,
                    OrderNum = row.OrderNum,
                    ReqMsgId = string.Empty,
                    ReqValue = null,
                    ReqPassId = row.ReqPassId ?? string.Empty,
                    ReqSeasonId = row.ReqSeasonId ?? string.Empty,
                    ConditionMsgId = row.ConditionMsgId ?? string.Empty,
                    ConditionOp = row.ConditionOp,
                    ConditionValue = row.ConditionValue,
                    RewardGroupId = row.RewardGroupId ?? string.Empty,
                    AppleAchievementId = string.Empty,
                    GoogleAchievementId = string.Empty,
                });
            }

            foreach (var rows in _rowsByAchieveId.Values)
            {
                rows.Sort((x, y) =>
                {
                    var levelCompare = x.Level.CompareTo(y.Level);
                    if (levelCompare != 0)
                        return levelCompare;

                    var orderCompare = x.OrderNum.CompareTo(y.OrderNum);
                    if (orderCompare != 0)
                        return orderCompare;

                    return x.achieveType.CompareTo(y.achieveType);
                });
            }
        }

        void addAchieveRow(AchieveTableRow row)
        {
            if (row == null)
                return;

            var achieveId = (row.AchieveId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(achieveId))
                return;

            row.AchieveId = achieveId;

            if (!_rowsByAchieveId.TryGetValue(achieveId, out var rows))
            {
                rows = new List<AchieveTableRow>();
                _rowsByAchieveId[achieveId] = rows;
            }

            if (rows.Count > 0 && rows[0].achieveType != row.achieveType)
            {
                Debug.LogError($"[{Tag}] Mixed achieve types in same group: achieveId='{achieveId}'.");
                return;
            }

            rows.Add(row);
        }

        AchieveTableRow findAchievementMappingRow(string achieveId)
        {
            var levelOne = findStartRow(achieveId);
            if (levelOne != null)
                return levelOne;

            if (!_rowsByAchieveId.TryGetValue(achieveId, out var rows) || rows == null || rows.Count <= 0)
                return null;

            return rows[0];
        }

        static AchievementMapEntry createAchievementMapEntry(AchieveTableRow row)
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
            var groupKeys = new HashSet<string>(_rowsByAchieveId.Keys, StringComparer.Ordinal);
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
                    if (_rowsByAchieveId.TryGetValue(groupKey, out var rows) && rows.Count > 0)
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

            tryActivateWaitingRuntimes(GAME_MESSAGE_TYPE.NONE, CBigInt.Zero);
        }

        void onRuntimeChanged(AchieveRuntimeBase runtime)
        {
            emitRuntimeProgress(runtime);
        }

        void onRuntimeClaimable(AchieveRuntimeBase runtime)
        {
            emitRuntimeClaimable(runtime);
        }

        AchieveRuntimeBase restoreRuntimeForRow(AchieveRuntimeBase existing, AchieveTableRow row)
        {
            if (existing == null || row == null)
                return null;

            if (existing.state == MissionRuntimeState.WAIT)
            {
                return AchieveRuntimeFactory.Restore(new AchieveRuntimeRestoreArgs
                {
                    AchieveType = row.achieveType,
                    AchieveId = existing.achieveId,
                    AchieveUid = existing.achieveUid,
                    Level = existing.level,
                    Index = toRuntimeIndex(row),
                    State = MissionRuntimeState.WAIT,
                    ProgressValue = CBigInt.Zero,
                    StatType = GAME_MESSAGE_TYPE.NONE,
                    OpType = GAME_MESSAGE_SAVE_TYPE.NONE,
                    ConditionOpType = GAME_MESSAGE_OP_TYPE.GTE,
                    ConditionValue = CBigInt.Zero,
                    ReadProgress = null,
                    OnChanged = onRuntimeChanged,
                    OnClaimable = onRuntimeClaimable,
                });
            }

            if (!tryResolveRuntimeBinding(row, true, out var statType, out var opType, out var conditionOpType, out var conditionValue, out var readProgress))
                return null;

            return AchieveRuntimeFactory.Restore(new AchieveRuntimeRestoreArgs
            {
                AchieveType = row.achieveType,
                AchieveId = existing.achieveId,
                AchieveUid = existing.achieveUid,
                Level = existing.level,
                Index = toRuntimeIndex(row),
                State = existing.state,
                ProgressValue = existing.progressValue,
                StatType = statType,
                OpType = opType,
                ConditionOpType = conditionOpType,
                ConditionValue = conditionValue,
                ReadProgress = readProgress,
                OnChanged = onRuntimeChanged,
                OnClaimable = onRuntimeClaimable,
            });
        }

        AchieveRuntimeBase createRuntimeForRow(AchieveTableRow row)
        {
            if (row == null)
                return null;

            var achieveUid = _storage.AllocateAchieveUid();
            if (hasActivationRequirement(row, out _, out _, out _, out _, out _))
            {
                return AchieveRuntimeFactory.Create(new AchieveRuntimeCreateArgs
                {
                    AchieveType = row.achieveType,
                    AchieveId = row.AchieveId,
                    Level = row.Level,
                    Index = toRuntimeIndex(row),
                    AchieveUid = achieveUid,
                    State = MissionRuntimeState.WAIT,
                    StatType = GAME_MESSAGE_TYPE.NONE,
                    OpType = GAME_MESSAGE_SAVE_TYPE.NONE,
                    ConditionOpType = GAME_MESSAGE_OP_TYPE.GTE,
                    ConditionValue = CBigInt.Zero,
                    ReadProgress = null,
                    OnChanged = onRuntimeChanged,
                    OnClaimable = onRuntimeClaimable,
                });
            }

            if (!tryResolveRuntimeBinding(row, true, out var statType, out var opType, out var conditionOpType, out var conditionValue, out var readProgress))
                return null;

            return AchieveRuntimeFactory.Create(new AchieveRuntimeCreateArgs
            {
                AchieveType = row.achieveType,
                AchieveId = row.AchieveId,
                Level = row.Level,
                Index = toRuntimeIndex(row),
                AchieveUid = achieveUid,
                State = MissionRuntimeState.ACTIVE,
                StatType = statType,
                OpType = opType,
                ConditionOpType = conditionOpType,
                ConditionValue = conditionValue,
                ReadProgress = readProgress,
                OnChanged = onRuntimeChanged,
                OnClaimable = onRuntimeClaimable,
            });
        }

        void tryActivateWaitingRuntimes(GAME_MESSAGE_TYPE triggeredType, CBigInt triggeredValue)
        {
            if (_storage.runtimes.Count <= 0)
                return;

            var runtimes = new List<AchieveRuntimeBase>(_storage.runtimes.Count);
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime != null)
                    runtimes.Add(runtime);
            }

            foreach (var runtime in runtimes)
            {
                if (runtime == null || runtime.state != MissionRuntimeState.WAIT)
                    continue;

                var row = findRow(runtime.achieveId, runtime.level);
                if (!isEligibleRow(row))
                    continue;

                tryActivateRuntime(runtime, row!, triggeredType, triggeredValue);
            }
        }

        bool tryActivateRuntime(AchieveRuntimeBase runtime, AchieveTableRow row, GAME_MESSAGE_TYPE triggeredType, CBigInt triggeredValue)
        {
            if (runtime == null || row == null || runtime.state != MissionRuntimeState.WAIT)
                return false;

            if (hasActivationRequirement(row, out var reqMessage, out var reqValue, out var reqPassId, out var reqSeasonId, out var hasReqMessage)
                && !isRequirementSatisfied(row, hasReqMessage, reqMessage, reqValue, reqPassId, reqSeasonId, triggeredType, triggeredValue))
            {
                return false;
            }

            if (!tryResolveRuntimeBinding(row, true, out var statType, out var opType, out var conditionOpType, out var conditionValue, out var readProgress))
                return false;

            runtime.state = MissionRuntimeState.ACTIVE;
            runtime.Bind(
                statType,
                opType,
                conditionOpType,
                conditionValue,
                readProgress,
                onRuntimeChanged,
                onRuntimeClaimable);
            emitRuntimeActive(runtime);
            return true;
        }

        void notifyRuntimesByMessage(GAME_MESSAGE_TYPE messageType, CBigInt messageDelta)
        {
            if (_storage.runtimes.Count <= 0)
                return;

            var runtimes = new List<AchieveRuntimeBase>(_storage.runtimes.Count);
            foreach (var runtime in _storage.runtimes.Values)
            {
                if (runtime != null)
                    runtimes.Add(runtime);
            }

            foreach (var runtime in runtimes)
                runtime.OnMessageStatUpdated(messageType, messageDelta);
        }

        AchieveRuntimeBase findRuntime(string achieveId)
        {
            AchieveRuntimeBase found = null;
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

        bool isEligibleRow(AchieveTableRow row)
        {
            if (row == null || !row.IsActive)
                return false;

            return tryResolveRuntimeBinding(row, false, out _, out _, out _, out _, out _);
        }

        bool hasActivationRequirement(
            AchieveTableRow row,
            out GAME_MESSAGE reqMessage,
            out CBigInt reqValue,
            out string reqPassId,
            out string reqSeasonId,
            out bool hasReqMessage)
        {
            reqMessage = null;
            reqValue = CBigInt.Zero;
            reqPassId = string.Empty;
            reqSeasonId = string.Empty;
            hasReqMessage = false;

            if (row == null)
                return false;

            reqPassId = (row.ReqPassId ?? string.Empty).Trim();
            reqSeasonId = (row.ReqSeasonId ?? string.Empty).Trim();
            var hasReqPass = !string.IsNullOrWhiteSpace(reqPassId);
            var hasReqSeason = !string.IsNullOrWhiteSpace(reqSeasonId);

            if (hasReqSeason && TB_SEASON.Get(reqSeasonId) == null)
            {
                Debug.LogError($"[{Tag}] Invalid req season for achieve: achieveId='{row.AchieveId}', reqSeasonId='{row.ReqSeasonId}'.");
                return true;
            }

            if (row.achieveType == ACHIEVE_TYPE.PASS)
                return hasReqPass || hasReqSeason;

            hasReqMessage = !string.IsNullOrWhiteSpace(row.ReqMsgId);
            if (!hasReqMessage)
                return hasReqPass || hasReqSeason;

            if (!row.ReqValue.HasValue)
            {
                Debug.LogError($"[{Tag}] Invalid req condition for achieve: achieveId='{row.AchieveId}', reqMsgId='{row.ReqMsgId}', reqValue='{row.ReqValue}'.");
                return true;
            }

            if (!TryResolveMessage(row.ReqMsgId, out reqMessage) || reqMessage.SaveType == GAME_MESSAGE_SAVE_TYPE.NONE)
            {
                Debug.LogError($"[{Tag}] Invalid req message for achieve: achieveId='{row.AchieveId}', reqMsgId='{row.ReqMsgId}'.");
                reqMessage = null;
                return true;
            }

            reqValue = row.ReqValue.Value;
            return true;
        }

        bool isRequirementSatisfied(
            AchieveTableRow row,
            bool hasReqMessage,
            GAME_MESSAGE reqMessage,
            CBigInt reqValue,
            string reqPassId,
            string reqSeasonId,
            GAME_MESSAGE_TYPE triggeredType,
            CBigInt triggeredValue)
        {
            if (row == null)
                return false;

            if (!string.IsNullOrWhiteSpace(reqPassId))
            {
                var inventoryManager = InventoryManager.Instance;
                if (inventoryManager == null)
                    return false;

                if (!string.IsNullOrWhiteSpace(reqPassId)
                    && !inventoryManager.Storage.HasPass(reqPassId))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(reqSeasonId)
                && !isSeasonRequirementSatisfied(reqSeasonId))
            {
                return false;
            }

            if (!hasReqMessage)
                return true;

            if (reqMessage == null)
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

        static bool isSeasonRequirementSatisfied(string reqSeasonId)
        {
            if (string.IsNullOrWhiteSpace(reqSeasonId))
                return false;

            var season = TB_SEASON.Get(reqSeasonId);
            if (season == null)
                return false;

            var seasonStartUtcMs = season.StartUtcTime?.utcTimeMs ?? 0L;
            var seasonEndUtcMs = season.EndUtcTime?.utcTimeMs ?? 0L;
            if (seasonStartUtcMs <= 0L || seasonEndUtcMs <= seasonStartUtcMs)
                return false;

            var serverNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (serverNowUtcMs <= 0L)
                return false;

            return serverNowUtcMs >= seasonStartUtcMs && serverNowUtcMs < seasonEndUtcMs;
        }

        static bool isTotalSaveType(GAME_MESSAGE_SAVE_TYPE saveType)
        {
            return saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MIN;
        }

        AchieveTableRow findRow(string achieveId, int level)
        {
            if (!_rowsByAchieveId.TryGetValue(achieveId, out var rows))
                return null;

            foreach (var row in rows)
            {
                if (row.Level == level)
                    return row;
            }

            return null;
        }

        AchieveTableRow findStartRow(string achieveId)
        {
            if (!_rowsByAchieveId.TryGetValue(achieveId, out var rows))
                return null;

            foreach (var row in rows)
            {
                if (row.Level == 1)
                    return row;
            }

            return null;
        }

        AchieveTableRow findNextRow(string achieveId, int level)
        {
            if (!_rowsByAchieveId.TryGetValue(achieveId, out var rows))
                return null;

            AchieveTableRow next = null;
            foreach (var row in rows)
            {
                if (row.Level <= level)
                    continue;

                if (next == null || row.Level < next.Level)
                    next = row;
            }

            return next;
        }

        static int toRuntimeIndex(AchieveTableRow row)
        {
            return row != null ? Math.Max(0, row.OrderNum - 1) : 0;
        }

        bool tryResolveRuntimeBinding(
            AchieveTableRow row,
            bool logError,
            out GAME_MESSAGE_TYPE statType,
            out GAME_MESSAGE_SAVE_TYPE opType,
            out GAME_MESSAGE_OP_TYPE conditionOpType,
            out CBigInt conditionValue,
            out Func<CBigInt> readProgress)
        {
            statType = GAME_MESSAGE_TYPE.NONE;
            opType = GAME_MESSAGE_SAVE_TYPE.NONE;
            conditionOpType = GAME_MESSAGE_OP_TYPE.GTE;
            conditionValue = CBigInt.Zero;
            readProgress = null;

            if (row == null)
                return false;

            var conditionMsgId = (row.ConditionMsgId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(conditionMsgId))
            {
                if (row.achieveType == ACHIEVE_TYPE.SOCIAL)
                {
                    if (logError)
                    {
                        Debug.LogError(
                            $"[{Tag}] Invalid once achieve condition: achieveId='{row.AchieveId}', conditionMsgId='{row.ConditionMsgId}', conditionValue='{row.ConditionValue}'.");
                    }

                    return false;
                }

                conditionOpType = row.ConditionOp;
                conditionValue = CBigInt.Zero;
                return true;
            }

            if (!row.ConditionValue.HasValue)
            {
                if (logError)
                {
                    Debug.LogError(
                        $"[{Tag}] Invalid achieve condition value: achieveId='{row.AchieveId}', conditionMsgId='{conditionMsgId}', conditionValue='{row.ConditionValue}'.");
                }

                return false;
            }

            if (!TryResolveMessage(conditionMsgId, out var message) || message.SaveType == GAME_MESSAGE_SAVE_TYPE.NONE)
            {
                if (logError)
                    Debug.LogError($"[{Tag}] GAME_MESSAGE not found for achieve: achieveId='{row.AchieveId}', conditionMsgId='{conditionMsgId}'.");
                return false;
            }

            statType = message.MessageType;
            opType = message.SaveType;
            conditionOpType = row.ConditionOp;
            conditionValue = row.ConditionValue.Value;
            readProgress = createExternalProgressReader(conditionMsgId, message.SaveType);
            return true;
        }

        static bool TryResolveMessage(string messageId, out GAME_MESSAGE message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            message = TB_GAME_MESSAGE.Get(messageId);
            return message != null;
        }

        static Func<CBigInt> createExternalProgressReader(string messageId, GAME_MESSAGE_SAVE_TYPE saveType)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return null;

            if (saveType != GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM
                && saveType != GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX
                && saveType != GAME_MESSAGE_SAVE_TYPE.TOTAL_MIN)
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
