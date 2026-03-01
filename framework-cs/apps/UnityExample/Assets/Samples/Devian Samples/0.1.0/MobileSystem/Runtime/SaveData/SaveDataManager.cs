using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;

namespace Devian
{
    public enum SyncState
    {
        Success,
        Conflict,
        Initial,
    }

    public sealed class SyncResult
    {
        public SyncState State { get; }
        public SaveLocalPayload LocalPayload { get; }
        public SaveCloudPayload CloudPayload { get; }
        public string LocalDeviceId { get; }
        public string CloudDeviceId { get; }

        public SyncResult(SyncState state,
            SaveLocalPayload localPayload = null, SaveCloudPayload cloudPayload = null,
            string localDeviceId = null, string cloudDeviceId = null)
        {
            State = state;
            LocalPayload = localPayload;
            CloudPayload = cloudPayload;
            LocalDeviceId = localDeviceId;
            CloudDeviceId = cloudDeviceId;
        }
    }

    public enum SyncResolution
    {
        UseLocal,
        UseCloud,
    }

    public sealed class SaveDataManager : CompoSingleton<SaveDataManager>
    {
        private const int SchemaVersion = 1;
        private const string UpdateTimeFormat = "yyyyMMdd:HHmmss";
        private const string DeviceIdPrefsKey = "Devian.DeviceId";
        private const string SaveSeqPrefsKey = "Devian.SaveSeq";

        [Header("Local Storage")]
        [SerializeField] private SaveLocalRoot _localRoot = SaveLocalRoot.PersistentData;

        [Header("Primary Save")]
        [SerializeField] private string _primaryLocalFilename = "save/main.json";
        [SerializeField] private string _primaryCloudSlot = "main";

        private ISaveCloudClient _cloudClient;

        private bool _hasPrimarySaveContext;
        private bool _needsCloudSave;

        /// <summary>
        /// 저장하지 못한 구매 내역이 있는지 나타내는 상태 조회용 플래그 (인메모리).
        /// 앱 재시작 시 false로 초기화된다.
        /// </summary>
        public bool NeedsCloudSave => _needsCloudSave;

        /// <summary>
        /// 구매/환불 성공 후 cloud 저장이 실패했을 때 호출.
        /// </summary>
        public void MarkNeedsCloudSave()
        {
            _needsCloudSave = true;
        }

        /// <summary>
        /// Cloud 저장 성공 또는 Resolve 완료 시 호출하여 플래그를 해제한다.
        /// </summary>
        public void ClearNeedsCloudSave()
        {
            _needsCloudSave = false;
        }

        // ──────────────────────────────────────────────
        //  Public: Sync API
        // ──────────────────────────────────────────────

        public async Task<CommonResult<SyncResult>> SyncAsync(CancellationToken ct)
        {
            var result = await syncPrimaryCoreAsync(ct);

            if (result.IsFailure)
            {
                _hasPrimarySaveContext = false;
                return result;
            }

            _hasPrimarySaveContext = result.Value.State != SyncState.Conflict;

            if (result.Value.State == SyncState.Success
                && result.Value.LocalPayload?.payload != null)
            {
                LoadFromPayload(result.Value.LocalPayload.payload);
                await postSyncEntitlementsAsync(ct);
            }

            return result;
        }

        private async Task<CommonResult<SyncResult>> syncPrimaryCoreAsync(CancellationToken ct)
        {
            var accountManager = AccountManager.Instance;

            // NONE/Guest/Editor: local-only. primary save를 로드하여 payload를 채워 반환.
            if (accountManager.IsLocalOnlySaveMode)
            {
                var localR = await loadPrimaryLocalRecordAsync(ct);
                if (localR.IsFailure)
                    return CommonResult<SyncResult>.Failure(localR.Error!);

                var local = localR.Value;
                if (local == null)
                    return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Initial));

                // 로컬 파일의 AccountMeta에서 loginType 확인.
                // 런타임 loginType은 아직 NONE이지만, 파일에 저장된 loginType이
                // NONE이 아니면 이전에 로그인 성공한 데이터이므로 Success로 처리한다.
                var persistedLoginType = local.account?.loginType ?? LoginType.NONE;
                if (persistedLoginType == LoginType.NONE)
                    return CommonResult<SyncResult>.Success(new SyncResult(
                        SyncState.Initial, local, null, local.deviceId, null));

                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Success,
                    local,
                    null,
                    local.deviceId,
                    null));
            }

            // Cloud init 시도. 실패하면 local-only로 진행하되 primary save 기준으로만 판정한다.
            // primary local 데이터가 없으면 실패를 반환한다.
            {
                var init = await _initializeCloudAsync(ct);
                if (init.IsFailure)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[SaveDataManager] SyncAsync: cloud init failed, proceeding local-only. error={init.Error}");

                    var localR = await loadPrimaryLocalRecordAsync(ct);
                    if (localR.IsFailure)
                        return CommonResult<SyncResult>.Failure(localR.Error!);

                    var local = localR.Value;
                    if (local == null)
                        return CommonResult<SyncResult>.Failure(init.Error!);

                    return CommonResult<SyncResult>.Success(new SyncResult(
                        SyncState.Success,
                        local,
                        null,
                        local.deviceId,
                        null));
                }
            }

            // Cloud 사용 가능: primary save 1개만 sync 처리하고, 가능한 경우 payload를 채워 반환.
            var localR2 = await loadPrimaryLocalRecordAsync(ct);
            if (localR2.IsFailure)
            {
                return CommonResult<SyncResult>.Failure(
                    new CommonError(CommonErrorType.SAVEDATA_SYNC_LOAD_LOCAL_FAILED, "Sync load local failed for primary save.", localR2.Error!.ToString()));
            }

            var cloudR2 = await loadPrimaryCloudRecordAsync(ct);
            if (cloudR2.IsFailure)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] SyncAsync load cloud failed for primary save. " +
                    $"Failing sync. error={cloudR2.Error}");
                return CommonResult<SyncResult>.Failure(cloudR2.Error!);
            }

            var local2 = localR2.Value;
            var cloud2 = cloudR2.IsSuccess ? cloudR2.Value : null;

            // both missing
            if (local2 == null && cloud2 == null)
            {
                return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Initial));
            }

            // cloud -> local
            if (local2 == null && cloud2 != null)
            {
                var jsonR = decryptCloudPayloadToJson(cloud2);
                if (jsonR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_LOCAL_FAILED, "Sync decrypt cloud failed for primary save.", jsonR.Error!.ToString()));
                }

                _needsCloudSave = false;
                var saveLocalR = await savePrimaryLocalAsync(jsonR.Value, ct);
                if (saveLocalR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_LOCAL_FAILED, "Sync save local failed for primary save.", saveLocalR.Error!.ToString()));
                }

                // Reload local to return the newly-written saveSeq/deviceId.
                var reLocal = await loadPrimaryLocalRecordAsync(ct);
                var lp = reLocal.IsSuccess ? reLocal.Value : null;
                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Success, lp, cloud2, lp?.deviceId, cloud2?.DeviceId));
            }

            // local -> cloud
            if (local2 != null && cloud2 == null)
            {
                var jsonR = decryptLocalPayloadToJson(local2);
                if (jsonR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_CLOUD_FAILED, "Sync decrypt local failed for primary save.", jsonR.Error!.ToString()));
                }

                var saveCloudR = await savePrimaryCloudAsync(jsonR.Value, ct);
                if (saveCloudR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_CLOUD_FAILED, "Sync save cloud failed for primary save.", saveCloudR.Error!.ToString()));
                }

                var reCloud = await loadPrimaryCloudRecordAsync(ct);
                var cp = reCloud.IsSuccess ? reCloud.Value : null;
                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Success, local2, cp, local2.deviceId, cp?.DeviceId));
            }

            // both exist
            if (local2 != null && cloud2 != null)
            {
                if (hasSameObfuscatedPayload(local2, cloud2))
                {
                    return CommonResult<SyncResult>.Success(new SyncResult(
                        SyncState.Success, local2, cloud2, local2.deviceId, cloud2.DeviceId));
                }

                var localDeviceId = local2.deviceId ?? string.Empty;
                var cloudDeviceId = cloud2.DeviceId ?? string.Empty;

                if (!string.Equals(localDeviceId, cloudDeviceId, StringComparison.Ordinal))
                {
                    return CommonResult<SyncResult>.Success(new SyncResult(
                            SyncState.Conflict, local2, cloud2, localDeviceId, cloudDeviceId));
                }

                if (TryCompareSaveSeq(local2, cloud2, out var seqCompare))
                {
                    if (seqCompare > 0)
                    {
                        var jsonR = decryptLocalPayloadToJson(local2);
                        if (jsonR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_CLOUD_FAILED, "Sync decrypt local failed for primary save.", jsonR.Error!.ToString()));
                        }

                        var saveCloudR = await savePrimaryCloudAsync(jsonR.Value, ct);
                        if (saveCloudR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_CLOUD_FAILED, "Sync save cloud failed for primary save.", saveCloudR.Error!.ToString()));
                        }

                        var reCloud = await loadPrimaryCloudRecordAsync(ct);
                        var cp = reCloud.IsSuccess ? reCloud.Value : cloud2;
                        return CommonResult<SyncResult>.Success(new SyncResult(
                            SyncState.Success, local2, cp, localDeviceId, cp?.DeviceId));
                    }

                    {
                        var jsonR = decryptCloudPayloadToJson(cloud2);
                        if (jsonR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_LOCAL_FAILED, "Sync decrypt cloud failed for primary save.", jsonR.Error!.ToString()));
                        }

                        _needsCloudSave = false;
                        var saveLocalR = await savePrimaryLocalAsync(jsonR.Value, ct);
                        if (saveLocalR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.SAVEDATA_SYNC_SAVE_LOCAL_FAILED, "Sync save local failed for primary save.", saveLocalR.Error!.ToString()));
                        }

                        var reLocal = await loadPrimaryLocalRecordAsync(ct);
                        var lp = reLocal.IsSuccess ? reLocal.Value : local2;
                        return CommonResult<SyncResult>.Success(new SyncResult(
                            SyncState.Success, lp, cloud2, lp?.deviceId, cloudDeviceId));
                    }
                }

                // Same device but payload differs and saveSeq is missing/invalid/tied.
                // Fall back to explicit user conflict resolution.
                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Conflict, local2, cloud2, localDeviceId, cloudDeviceId));
            }

            // fallback (should not reach)
            return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Success));
        }

        public async Task<CommonResult<bool>> ResolveConflictAsync(
            SyncResolution resolution, CancellationToken ct)
        {
            if (AccountManager.Instance.IsLocalOnlySaveMode)
            {
                return CommonResult<bool>.Failure(
                    CommonErrorType.SAVEDATA_SYNC_RESOLVE_FAILED,
                    "Cloud sync conflict resolution is not available in Guest/Editor (Local-only).");
            }

            // Resolve requires cloud access (load/save). Ensure cloud client is initialized.
            {
                var init = await _initializeCloudAsync(ct);
                if (init.IsFailure)
                    return CommonResult<bool>.Failure(init.Error!);
            }

            try
            {
                switch (resolution)
                {
                    case SyncResolution.UseLocal:
                    {
                        var localR = await loadPrimaryLocalRecordAsync(ct);
                        if (localR.IsFailure)
                            return CommonResult<bool>.Failure(localR.Error!);
                        if (localR.Value == null)
                            return CommonResult<bool>.Failure(CommonErrorType.SAVEDATA_SYNC_RESOLVE_FAILED, "Local payload is null.");

                        var jsonR = decryptLocalPayloadToJson(localR.Value);
                        if (jsonR.IsFailure)
                            return CommonResult<bool>.Failure(jsonR.Error!);

                        var saveCloud = await savePrimaryCloudAsync(jsonR.Value, ct);
                        if (saveCloud.IsFailure)
                            return CommonResult<bool>.Failure(saveCloud.Error!);

                        _needsCloudSave = false;
                        _hasPrimarySaveContext = true;
                        LoadFromPayload(localR.Value.payload);
                        await postSyncEntitlementsAsync(ct);

                        return CommonResult<bool>.Success(true);
                    }

                    case SyncResolution.UseCloud:
                    {
                        var cloudR = await loadPrimaryCloudRecordAsync(ct);
                        if (cloudR.IsFailure)
                            return CommonResult<bool>.Failure(cloudR.Error!);
                        if (cloudR.Value == null)
                            return CommonResult<bool>.Failure(CommonErrorType.SAVEDATA_SYNC_RESOLVE_FAILED, "Cloud payload is null.");

                        var jsonR = decryptCloudPayloadToJson(cloudR.Value);
                        if (jsonR.IsFailure)
                            return CommonResult<bool>.Failure(jsonR.Error!);

                        _needsCloudSave = false;
                        var saveLocalR = await savePrimaryLocalAsync(jsonR.Value, ct);
                        if (saveLocalR.IsFailure)
                            return CommonResult<bool>.Failure(saveLocalR.Error!);

                        LoadFromJson(jsonR.Value);
                        _hasPrimarySaveContext = true;
                        await postSyncEntitlementsAsync(ct);

                        return CommonResult<bool>.Success(true);
                    }

                    default:
                        return CommonResult<bool>.Failure(CommonErrorType.SAVEDATA_SYNC_RESOLVE_FAILED, $"Unknown resolution: {resolution}");
                }
            }
            catch (OperationCanceledException ex)
            {
                return CommonResult<bool>.Failure(
                    new CommonError(CommonErrorType.SAVEDATA_SYNC_CANCELLED, "Resolve cancelled.", ex.Message));
            }
        }

        private async Task<CommonResult> postSyncEntitlementsAsync(CancellationToken ct)
        {
            var inventory = getInventoryStorageOrNull();
            if (inventory == null || (inventory.Rentals.Count <= 0 && inventory.SeasonPasses.Count <= 0))
                return CommonResult.Ok();

            var result = await PurchaseManager.Instance.SyncEntitlementsAsync(ct);
            if (result.IsFailure)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] PostSync SyncEntitlements failed (non-fatal): {result.Error}");
                return CommonResult.Failure(result.Error!);
            }

            return CommonResult.Ok();
        }

        // ──────────────────────────────────────────────
        //  Public: Save API
        // ──────────────────────────────────────────────

        /// <summary>
        /// 현재 Account/Inventory/Purchase 상태를 local + cloud에 저장한다.
        /// SyncAsync 성공 후 사용 가능. cloud 저장 실패 시 MarkNeedsCloudSave 처리.
        /// </summary>
        public async Task<CommonResult<bool>> SaveGameStateAsync(CancellationToken ct)
        {
            if (!_hasPrimarySaveContext)
                return CommonResult<bool>.Failure(
                    CommonErrorType.SAVEDATA_SYNC_REQUIRED, "Primary save is not initialized. Call SyncAsync first.");

            var json = ToJson();
            var local = await savePrimaryLocalAsync(json, ct);
            if (local.IsFailure)
                return local;

            if (!AccountManager.Instance.IsLocalOnlySaveMode)
            {
                try
                {
                    var init = await _initializeCloudAsync(ct);
                    if (init.IsSuccess)
                    {
                        var cloud = await savePrimaryCloudAsync(json, ct);
                        if (cloud.IsFailure)
                        {
                            MarkNeedsCloudSave();
                            UnityEngine.Debug.LogWarning(
                                $"[SaveDataManager] SaveGameStateAsync cloud save failed (non-fatal): {cloud.Error}");
                        }
                        else
                        {
                            ClearNeedsCloudSave();
                        }
                    }
                    else
                    {
                        MarkNeedsCloudSave();
                    }
                }
                catch (System.Exception ex)
                {
                    MarkNeedsCloudSave();
                    UnityEngine.Debug.LogWarning(
                        $"[SaveDataManager] SaveGameStateAsync cloud exception (non-fatal): {ex.Message}");
                }
            }

            return CommonResult<bool>.Success(true);
        }

        // ──────────────────────────────────────────────
        //  Public: Load API
        // ──────────────────────────────────────────────

        public CommonResult<bool> LoadLocalGameState()
        {
            var record = loadPrimaryLocalRecord();
            if (record.IsFailure) return CommonResult<bool>.Failure(record.Error!);

            var payload = record.Value;
            if (payload?.payload == null)
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_NOT_FOUND, "No local data found.");

            _hasPrimarySaveContext = true;
            LoadFromPayload(payload.payload);
            return CommonResult<bool>.Success(true);
        }

        // ──────────────────────────────────────────────
        //  Public: Clear Slot API
        // ──────────────────────────────────────────────

        public async Task<CommonResult<bool>> ClearSaveAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled.");

            // 1) Local delete (idempotent)
            var filenameR = getPrimaryLocalFilename();
            if (filenameR.IsFailure)
            {
                return CommonResult<bool>.Failure(filenameR.Error!);
            }
            var filename = filenameR.Value;

            try
            {
                var root = getRootPath();
                if (string.IsNullOrWhiteSpace(root))
                {
                    return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_PATH_EMPTY, "Root path is empty.");
                }

                var path = System.IO.Path.Combine(root, filename);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                // NOTE: dedicated LOCALSAVE_DELETE 가 없으므로 LOCALSAVE_WRITE 재사용(파일 I/O 실패)
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_WRITE, $"Local delete failed. {ex.Message}");
            }

            // 2) Cloud delete
            if (AccountManager.Instance.IsLocalOnlySaveMode)
            {
                // Guest/Editor: cloud is silently ignored
                _hasPrimarySaveContext = false;
                ClearGameState();
                return CommonResult<bool>.Success(true);
            }

            // Initialize cloud; if failed, silently skip (policy: local can proceed)
            var init = await _initializeCloudAsync(ct);
            if (init.IsFailure)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] ClearSaveAsync: cloud init failed, skipping cloud delete. err={init.Error}");
                _hasPrimarySaveContext = false;
                ClearGameState();
                return CommonResult<bool>.Success(true);
            }

            if (_cloudClient == null || !_cloudClient.IsAvailable)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] ClearSaveAsync: cloud client not available, skipping cloud delete.");
                _hasPrimarySaveContext = false;
                ClearGameState();
                return CommonResult<bool>.Success(true);
            }

            var cloudSlotR = getPrimaryCloudSlot();
            if (cloudSlotR.IsFailure)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] ClearSaveAsync: primary cloud slot missing, skipping cloud delete. err={cloudSlotR.Error}");
                _hasPrimarySaveContext = false;
                ClearGameState();
                return CommonResult<bool>.Success(true);
            }
            var cloudSlot = cloudSlotR.Value;

            var del = await _cloudClient.DeleteAsync(cloudSlot, ct);
            if (del != SaveCloudResult.Success)
            {
                // Cloud delete 실패는 "로컬은 이미 삭제됨" 정책상 실패로 올리지 않고 warn 처리(최소 변경).
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] ClearSaveAsync: cloud delete failed. cloudSlot='{cloudSlot}' result={del}");
            }

            _hasPrimarySaveContext = false;
            ClearGameState();

            return CommonResult<bool>.Success(true);
        }

        public string ToJson()
        {
            var inventory = getInventoryStorageOrNull();
            var purchase = getPurchaseStorageOrNull();
            var account = getAccountStorageOrNull();
            return SaveDataJsonCodec.Serialize(
                inventory ?? new InventoryStorage(),
                purchase ?? new PurchaseStorage(),
                account ?? new AccountStorage());
        }

        public void LoadFromPayload(string payload)
        {
            var json = ComplexUtil.Decrypt_Base64(payload);
            LoadFromJson(json);
        }

        public void LoadFromJson(string json)
        {
            var inventory = getInventoryStorageOrNull();
            var purchase = getPurchaseStorageOrNull();
            var account = getAccountStorageOrNull();
            if (inventory == null || purchase == null || account == null)
                return;

            SaveDataJsonCodec.DeserializeInto(json, inventory, purchase, account);
            applyLoadedAccountStorageToRuntime();
        }

        public void ClearGameState()
        {
            getInventoryStorageOrNull()?.Clear();
            getPurchaseStorageOrNull()?.ClearAll();
            getAccountStorageOrNull()?.Clear();
            applyLoadedAccountStorageToRuntime();
        }

        // ──────────────────────────────────────────────
        //  Internal: Deobfuscate payload to json (source-aware)
        // ──────────────────────────────────────────────

        private CommonResult<string> decryptLocalPayloadToJson(SaveLocalPayload payload)
        {
            if (payload == null)
                return CommonResult<string>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveLocalPayload is null.");

            var raw = payload.payload ?? string.Empty;
            if (string.IsNullOrEmpty(raw))
                return CommonResult<string>.Success(raw);

            try
            {
                var json = ComplexUtil.Decrypt_Base64(raw);
                return CommonResult<string>.Success(json);
            }
            catch (Exception ex)
            {
                return CommonResult<string>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, $"Local deobfuscate failed: {ex.Message}");
            }
        }

        private CommonResult<string> decryptCloudPayloadToJson(SaveCloudPayload payload)
        {
            if (payload == null)
                return CommonResult<string>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveCloudPayload is null.");

            var raw = payload.Payload ?? string.Empty;
            if (string.IsNullOrEmpty(raw))
                return CommonResult<string>.Success(raw);

            try
            {
                var json = ComplexUtil.Decrypt_Base64(raw);
                return CommonResult<string>.Success(json);
            }
            catch (Exception ex)
            {
                return CommonResult<string>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, $"Cloud deobfuscate failed: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        //  Public: Payload parsing
        // ──────────────────────────────────────────────

        public static CommonResult<T> ParsePayloadResult<T>(SaveLocalPayload payload)
        {
            var mgr = SaveDataManager.Instance;
            if (mgr == null)
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveDataManager.Instance is null.");

            var dec = mgr.decryptLocalPayloadToJson(payload);
            if (dec.IsFailure)
                return CommonResult<T>.Failure(dec.Error!);

            var json = dec.Value;
            if (string.IsNullOrWhiteSpace(json))
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "Decrypted json is empty.");

            try
            {
                var value = JsonUtility.FromJson<T>(json);
                return CommonResult<T>.Success(value);
            }
            catch (Exception ex)
            {
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, ex.Message);
            }
        }

        public static CommonResult<T> ParsePayloadResult<T>(SaveCloudPayload payload)
        {
            var mgr = SaveDataManager.Instance;
            if (mgr == null)
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveDataManager.Instance is null.");

            var dec = mgr.decryptCloudPayloadToJson(payload);
            if (dec.IsFailure)
                return CommonResult<T>.Failure(dec.Error!);

            var json = dec.Value;
            if (string.IsNullOrWhiteSpace(json))
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "Decrypted json is empty.");

            try
            {
                var value = JsonUtility.FromJson<T>(json);
                return CommonResult<T>.Success(value);
            }
            catch (Exception ex)
            {
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        //  Internal: Cloud initialization
        // ──────────────────────────────────────────────

        internal Task<CommonResult<SaveCloudResult>> _initializeCloudAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            return Task.FromResult(editorNoCloud<SaveCloudResult>());
#else
            if (_cloudClient == null)
            {
                _cloudClient = createDefaultClient();
            }

            return signInCloudInternal(ct);
#endif
        }

        internal bool _isCloudAvailable => _cloudClient != null && _cloudClient.IsAvailable;

        // ──────────────────────────────────────────────
        //  Private: Local save operations
        // ──────────────────────────────────────────────

        private CommonResult<string> getPrimaryLocalFilename()
        {
            var filename = _primaryLocalFilename?.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(filename))
            {
                return CommonResult<string>.Failure(
                    CommonErrorType.LOCALSAVE_FILENAME_INVALID,
                    "Primary local filename is empty.");
            }

            if (!IsValidJsonFilename(filename, out var fnError))
            {
                return CommonResult<string>.Failure(CommonErrorType.LOCALSAVE_FILENAME_INVALID, fnError);
            }

            return CommonResult<string>.Success(filename);
        }

        private CommonResult<string> getPrimaryCloudSlot()
        {
            var cloudSlot = _primaryCloudSlot?.Trim();
            if (string.IsNullOrWhiteSpace(cloudSlot))
            {
                return CommonResult<string>.Failure(
                    CommonErrorType.CLOUDSAVE_SLOT_MISSING,
                    "Primary cloud slot is not configured.");
            }

            return CommonResult<string>.Success(cloudSlot);
        }

        private CommonResult<SaveLocalPayload> loadPrimaryLocalRecord()
        {
            var filenameR = getPrimaryLocalFilename();
            if (filenameR.IsFailure)
                return CommonResult<SaveLocalPayload>.Failure(filenameR.Error!);

            var filename = filenameR.Value;

            var loaded = SaveLocalFileStore.Read(getRootPath(), filename);
            if (loaded.IsFailure)
            {
                return CommonResult<SaveLocalPayload>.Failure(loaded.Error!);
            }

            var save = loaded.Value;
            if (save == null)
            {
                return CommonResult<SaveLocalPayload>.Success(null);
            }

            // Payload Contract (Obfuscated-only):
            // - 반환 SaveLocalPayload.payload 는 저장 포맷 그대로(난독화 시 obfuscated)여야 한다.
            // - deobfuscate/parse는 별도 경로(SaveDataManager)를 통해 수행한다.
            return CommonResult<SaveLocalPayload>.Success(save);
        }

        private Task<CommonResult<SaveLocalPayload>> loadPrimaryLocalRecordAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(
                    CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            return Task.FromResult(loadPrimaryLocalRecord());
        }

        private CommonResult<bool> savePrimaryLocal(string data)
        {
            var filenameR = getPrimaryLocalFilename();
            if (filenameR.IsFailure)
                return CommonResult<bool>.Failure(filenameR.Error!);

            var filename = filenameR.Value;

            var plain = data ?? string.Empty;
            var obfuscated = ComplexUtil.Encrypt_Base64(plain);

            var save = new SaveLocalPayload(
                SchemaVersion,
                nowUpdateTime(),
                obfuscated,
                _getOrCreateDeviceId(),
                nextSaveSeq(),
                snapshotAccountMeta()
            );

            var write = SaveLocalFileStore.WriteAtomic(getRootPath(), filename, save);
            return write.IsSuccess
                ? CommonResult<bool>.Success(true)
                : CommonResult<bool>.Failure(write.Error!);
        }

        private Task<CommonResult<bool>> savePrimaryLocalAsync(string data, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            return Task.FromResult(savePrimaryLocal(data));
        }

        // ──────────────────────────────────────────────
        //  Private: Cloud save operations
        // ──────────────────────────────────────────────

        private Task<CommonResult<SaveCloudPayload>> loadPrimaryCloudRecordAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            return Task.FromResult(editorNoCloud<SaveCloudPayload>());
#else
            if (_cloudClient == null)
                return Task.FromResult(
                    CommonResult<SaveCloudPayload>.Failure(CommonErrorType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            var cloudSlotR = getPrimaryCloudSlot();
            if (cloudSlotR.IsFailure)
                return Task.FromResult(
                    CommonResult<SaveCloudPayload>.Failure(cloudSlotR.Error!));

            var cloudSlot = cloudSlotR.Value;
            return loadCloudRecordInternal(cloudSlot, ct);
#endif
        }

        private Task<CommonResult<bool>> savePrimaryCloudAsync(string data, CancellationToken ct)
        {
#if UNITY_EDITOR
            return Task.FromResult(editorNoCloud<bool>());
#else
            if (_cloudClient == null)
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            var cloudSlotR = getPrimaryCloudSlot();
            if (cloudSlotR.IsFailure)
                return Task.FromResult(
                    CommonResult<bool>.Failure(cloudSlotR.Error!));

            if (!isLikelyJson(data))
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_PAYLOAD_INVALID,
                        "Payload must be JSON (object or array)."));

            var cloudSlot = cloudSlotR.Value;
            return saveCloudInternal(cloudSlot, data, ct);
#endif
        }

        private async Task<CommonResult<SaveCloudResult>> signInCloudInternal(CancellationToken ct)
        {
            var r = await _cloudClient.SignInIfNeededAsync(ct);
            var clientName = _cloudClient != null ? _cloudClient.GetType().Name : "null";

            return r == SaveCloudResult.Success
                ? CommonResult<SaveCloudResult>.Success(r)
                : CommonResult<SaveCloudResult>.Failure(CommonErrorType.CLOUDSAVE_SIGNIN, $"Sign-in failed: {r} (client={clientName})");
        }

        private async Task<CommonResult<bool>> saveCloudInternal(
            string cloudSlot, string data, CancellationToken ct)
        {
            var plain = data ?? string.Empty;
            var obfuscated = ComplexUtil.Encrypt_Base64(plain);

            var csPayload = new SaveCloudPayload(
                SchemaVersion,
                nowUpdateTime(),
                obfuscated,
                _getOrCreateDeviceId(),
                nextSaveSeq(),
                snapshotAccountMeta()
            );

            var r = await _cloudClient.SaveAsync(cloudSlot, csPayload, ct);
            return r.IsFailure
                ? CommonResult<bool>.Failure(r.Error!)
                : CommonResult<bool>.Success(true);
        }

        private async Task<CommonResult<SaveCloudPayload>> loadCloudRecordInternal(
            string cloudSlot, CancellationToken ct)
        {
            var (result, loaded) = await _cloudClient.LoadAsync(cloudSlot, ct);

            if (result == SaveCloudResult.NotFound)
            {
                return CommonResult<SaveCloudPayload>.Success(null);
            }

            if (result != SaveCloudResult.Success)
            {
                var errorType = isCloudConnectionFailureResult(result)
                    ? CommonErrorType.CLOUDSAVE_CONNECTION_FAILED
                    : CommonErrorType.CLOUDSAVE_LOAD;
                return CommonResult<SaveCloudPayload>.Failure(errorType, $"Load failed: {result}");
            }

            if (loaded == null)
            {
                return CommonResult<SaveCloudPayload>.Success(null);
            }

            // Payload Contract (Obfuscated-only):
            // - 반환 SaveCloudPayload.Payload 는 저장 포맷 그대로(난독화 시 obfuscated)여야 한다.
            return CommonResult<SaveCloudPayload>.Success(loaded);
        }

        private static bool isCloudConnectionFailureResult(SaveCloudResult result)
        {
            switch (result)
            {
                case SaveCloudResult.NotAvailable:
                case SaveCloudResult.AuthRequired:
                case SaveCloudResult.TemporaryFailure:
                case SaveCloudResult.FatalFailure:
                    return true;
                default:
                    return false;
            }
        }

        private static ISaveCloudClient createDefaultClient()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new AppleSaveCloudClient();
#elif UNITY_ANDROID && !UNITY_EDITOR
            tryActivateGpgsSavedGames();
            return new SaveCloudClientGoogle();
#else
            return new SaveCloudClientGoogle();
#endif
        }

        private static void tryActivateGpgsSavedGames()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");

                if (platformType != null)
                {
                    platformType.GetMethod("Activate", BindingFlags.Public | BindingFlags.Static)
                        ?.Invoke(null, null);
                    return;
                }

                platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, GooglePlayGames");
                if (platformType == null) return;

                var builderType = Type.GetType(
                    "GooglePlayGames.BasicApi.PlayGamesClientConfiguration+Builder, GooglePlayGames");
                if (builderType == null) return;

                var builder = Activator.CreateInstance(builderType);
                if (builder == null) return;

                builderType.GetMethod("EnableSavedGames", Type.EmptyTypes)
                    ?.Invoke(builder, null);

                var config = builderType.GetMethod("Build", Type.EmptyTypes)
                    ?.Invoke(builder, null);
                if (config == null) return;

                MethodInfo init = null;
                var methods = platformType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (var i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (!string.Equals(m.Name, "InitializeInstance", StringComparison.Ordinal)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 1)
                    {
                        init = m;
                        break;
                    }
                }

                init?.Invoke(null, new[] { config });

                platformType.GetMethod("Activate", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, null);
            }
            catch
            {
                // Best-effort: no throw.
            }
#endif
        }

        // ──────────────────────────────────────────────
        //  Private: Shared helpers
        // ──────────────────────────────────────────────

        private static AccountStorage snapshotAccountMeta()
        {
            var accountStorage = getAccountStorageOrNull();
            if (accountStorage != null)
                return accountStorage.Clone();

            return null;
        }

        private static void applyLoadedAccountStorageToRuntime()
        {
            if (!AccountManager.TryGet(out var accountManager))
                return;

            accountManager.ApplyStorage(accountManager.Storage);
        }

        private static InventoryStorage getInventoryStorageOrNull()
        {
            try
            {
                var inventoryManager = InventoryManager.Instance;
                return inventoryManager != null ? inventoryManager.Storage : null;
            }
            catch
            {
                return null;
            }
        }

        private static PurchaseStorage getPurchaseStorageOrNull()
        {
            try
            {
                var purchaseManager = PurchaseManager.Instance;
                return purchaseManager != null ? purchaseManager.Storage : null;
            }
            catch
            {
                return null;
            }
        }

        private static AccountStorage getAccountStorageOrNull()
        {
            try
            {
                var accountManager = AccountManager.Instance;
                return accountManager != null ? accountManager.Storage : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool hasSameObfuscatedPayload(SaveLocalPayload local, SaveCloudPayload cloud)
        {
            if (local == null || cloud == null)
                return false;

            return string.Equals(local.payload ?? string.Empty, cloud.Payload ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool TryCompareSaveSeq(SaveLocalPayload local, SaveCloudPayload cloud, out int compare)
        {
            compare = 0;

            var localSeq = local != null ? local.saveSeq : 0L;
            var cloudSeq = cloud != null ? cloud.SaveSeq : 0L;
            if (localSeq <= 0 || cloudSeq <= 0 || localSeq == cloudSeq)
                return false;

            compare = localSeq > cloudSeq ? 1 : -1;
            return true;
        }

        private static long nextSaveSeq()
        {
            long seq = 0L;
            var raw = PlayerPrefs.GetString(SaveSeqPrefsKey, null);
            if (!string.IsNullOrEmpty(raw))
                long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seq);

            if (seq < 0)
                seq = 0;

            seq++;
            PlayerPrefs.SetString(SaveSeqPrefsKey, seq.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            return seq;
        }

        private static string _getOrCreateDeviceId()
        {
            var id = PlayerPrefs.GetString(DeviceIdPrefsKey, null);
            if (!string.IsNullOrEmpty(id)) return id;

            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceIdPrefsKey, id);
            PlayerPrefs.Save();
            return id;
        }

        private static string nowUpdateTime()
        {
            return DateTime.Now.ToString(UpdateTimeFormat, CultureInfo.InvariantCulture);
        }

        private string getRootPath()
        {
            return _localRoot == SaveLocalRoot.PersistentData
                ? Application.persistentDataPath
                : Application.temporaryCachePath;
        }

        internal static bool IsValidJsonFilename(string filename, out string error)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                error = "Filename is empty.";
                return false;
            }

            if (filename.Contains(".."))
            {
                error = "Filename must not contain '..'.";
                return false;
            }

            if (!filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                error = "Filename must end with .json";
                return false;
            }

            error = null;
            return true;
        }

        private static bool isLikelyJson(string s)
        {
            if (s == null) return true;
            s = s.Trim();
            if (s.Length == 0) return true;

            var first = s[0];
            var last = s[s.Length - 1];
            if (first == '{' && last == '}') return true;
            if (first == '[' && last == ']') return true;
            return false;
        }

        // ──────────────────────────────────────────────
        //  Editor
        // ──────────────────────────────────────────────

#if UNITY_EDITOR
        private static CommonResult<T> editorNoCloud<T>()
        {
            return CommonResult<T>.Failure(
                CommonErrorType.CLOUDSAVE_NOCLIENT,
                "SaveCloud is not supported in Unity Editor. Use SaveLocal only.");
        }
#endif
    }
}
