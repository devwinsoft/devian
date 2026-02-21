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
        ConnectionFailed,
    }

    public sealed class SyncResult
    {
        public SyncState State { get; }
        public string Slot { get; }
        public SaveLocalPayload LocalPayload { get; }
        public SaveCloudPayload CloudPayload { get; }
        public string LocalDeviceId { get; }
        public string CloudDeviceId { get; }

        public SyncResult(SyncState state, string slot = null,
            SaveLocalPayload localPayload = null, SaveCloudPayload cloudPayload = null,
            string localDeviceId = null, string cloudDeviceId = null)
        {
            State = state;
            Slot = slot;
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

        [Header("Local Storage")]
        [SerializeField] private SaveLocalRoot _localRoot = SaveLocalRoot.PersistentData;

        [Header("Slots (Shared)")]
        [SerializeField] private SaveSlotConfig _slotConfig = new();

        private ISaveCloudClient _cloudClient;

        // ──────────────────────────────────────────────
        //  Public: Sync API
        // ──────────────────────────────────────────────

        private bool isLocalOnly(LoginType loginType)
        {
#if UNITY_EDITOR
            return true;
#else
            return loginType == LoginType.GuestLogin || loginType == LoginType.EditorLogin;
#endif
        }

        private async Task<bool> hasAnyLocalAsync(CancellationToken ct)
        {
            var localKeys = _slotConfig.GetLocalSlotKeys();
            for (var i = 0; i < localKeys.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(localKeys[i])) continue;
                var r = await loadLocalRecordAsync(localKeys[i], ct);
                if (r.IsSuccess && r.Value != null) return true;
            }
            return false;
        }

        public async Task<CommonResult<SyncResult>> SyncAsync(CancellationToken ct)
        {
            var loginType = AccountManager.Instance._getCurrentLoginType();

            if (isLocalOnly(loginType))
            {
                var hasAnyLocal = await hasAnyLocalAsync(ct);
                var state = hasAnyLocal ? SyncState.Success : SyncState.Initial;
                return CommonResult<SyncResult>.Success(new SyncResult(state));
            }

            // Try to initialize cloud so Sync can actually read cloud records.
            // If it fails, continue as local-only (same effective behavior as before).
            {
                var init = await _initializeCloudAsync(ct);
                if (init.IsFailure)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[SaveDataManager] SyncAsync: cloud init failed, proceeding local-only. error={init.Error}");

                    // If no local payload exists, caller cannot proceed -> explicit ConnectionFailed state.
                    // If local payload exists, caller can still handle using local data, so do NOT return ConnectionFailed.
                    var hasAnyLocal = await hasAnyLocalAsync(ct);
                    if (!hasAnyLocal)
                    {
                        return CommonResult<SyncResult>.Success(new SyncResult(SyncState.ConnectionFailed));
                    }
                }
            }

            return await syncAsync(ct);
        }

        public async Task<CommonResult<SyncResult>> SyncAsync(string slot, CancellationToken ct)
        {
            var loginType = AccountManager.Instance._getCurrentLoginType();

            // Guest/Editor: local-only. 반드시 slot 단일을 로드하여 payload를 채워 반환.
            if (isLocalOnly(loginType))
            {
                if (string.IsNullOrWhiteSpace(slot))
                    return CommonResult<SyncResult>.Failure(
                        CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");

                var localR = await loadLocalRecordAsync(slot, ct);
                if (localR.IsFailure)
                    return CommonResult<SyncResult>.Failure(localR.Error!);

                var local = localR.Value;
                if (local == null)
                    return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Initial, slot));

                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Success,
                    slot,
                    local,
                    null,
                    local.deviceId,
                    null));
            }

            // Cloud init 시도. 실패하면 local-only로 진행하되 slot 기준으로만 판정.
            {
                var init = await _initializeCloudAsync(ct);
                if (init.IsFailure)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[SaveDataManager] SyncAsync(slot): cloud init failed, proceeding local-only. error={init.Error}");

                    if (string.IsNullOrWhiteSpace(slot))
                        return CommonResult<SyncResult>.Success(new SyncResult(SyncState.ConnectionFailed));

                    var localR = await loadLocalRecordAsync(slot, ct);
                    if (localR.IsFailure)
                        return CommonResult<SyncResult>.Failure(localR.Error!);

                    var local = localR.Value;
                    if (local == null)
                        return CommonResult<SyncResult>.Success(new SyncResult(SyncState.ConnectionFailed, slot));

                    return CommonResult<SyncResult>.Success(new SyncResult(
                        SyncState.Success,
                        slot,
                        local,
                        null,
                        local.deviceId,
                        null));
                }
            }

            // Cloud 사용 가능: slot 1개만 sync 처리하고, 가능한 경우 payload를 채워 반환.
            if (string.IsNullOrWhiteSpace(slot))
                return CommonResult<SyncResult>.Failure(
                    CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");

            var localR2 = await loadLocalRecordAsync(slot, ct);
            if (localR2.IsFailure)
            {
                return CommonResult<SyncResult>.Failure(
                    new CommonError(CommonErrorType.LOGIN_SYNC_LOAD_LOCAL_FAILED, $"Sync load local failed. slot='{slot}'", localR2.Error!.ToString()));
            }

            var cloudWritable = true;
            var cloudR2 = await loadCloudRecordAsync(slot, ct);
            if (cloudR2.IsFailure)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] SyncAsync(slot) load cloud failed. slot='{slot}'. " +
                    $"Proceeding with local-only. error={cloudR2.Error}");
                cloudWritable = false;
            }

            var local2 = localR2.Value;
            var cloud2 = cloudR2.IsSuccess ? cloudR2.Value : null;

            // both missing
            if (local2 == null && cloud2 == null)
            {
                var st = cloudWritable ? SyncState.Initial : SyncState.ConnectionFailed;
                return CommonResult<SyncResult>.Success(new SyncResult(st, slot));
            }

            // cloud -> local (+ cloud resave)
            if (local2 == null && cloud2 != null)
            {
                var jsonR = decryptCloudPayloadToJson(cloud2);
                if (jsonR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_LOCAL_FAILED, $"Sync decrypt cloud failed. slot='{slot}'", jsonR.Error!.ToString()));
                }

                var saveLocalR = await saveLocalAsync(slot, jsonR.Value, ct);
                if (saveLocalR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_LOCAL_FAILED, $"Sync save local failed. slot='{slot}'", saveLocalR.Error!.ToString()));
                }

                var cloudResaveR = await saveCloudAsync(slot, jsonR.Value, ct);
                if (cloudResaveR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_CLOUD_FAILED, $"Sync re-save cloud failed. slot='{slot}'", cloudResaveR.Error!.ToString()));
                }

                // reload to return payloads
                var reLocal = await loadLocalRecordAsync(slot, ct);
                var reCloud = await loadCloudRecordAsync(slot, ct);
                var lp = reLocal.IsSuccess ? reLocal.Value : null;
                var cp = reCloud.IsSuccess ? reCloud.Value : null;
                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Success, slot, lp, cp, lp?.deviceId, cp?.DeviceId));
            }

            // local -> cloud
            if (local2 != null && cloud2 == null)
            {
                if (!cloudWritable)
                {
                    return CommonResult<SyncResult>.Success(new SyncResult(
                        SyncState.Success, slot, local2, null, local2.deviceId, null));
                }

                var jsonR = decryptLocalPayloadToJson(local2);
                if (jsonR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_CLOUD_FAILED, $"Sync decrypt local failed. slot='{slot}'", jsonR.Error!.ToString()));
                }

                var saveCloudR = await saveCloudAsync(slot, jsonR.Value, ct);
                if (saveCloudR.IsFailure)
                {
                    return CommonResult<SyncResult>.Failure(
                        new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_CLOUD_FAILED, $"Sync save cloud failed. slot='{slot}'", saveCloudR.Error!.ToString()));
                }

                var reCloud = await loadCloudRecordAsync(slot, ct);
                var cp = reCloud.IsSuccess ? reCloud.Value : null;
                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Success, slot, local2, cp, local2.deviceId, cp?.DeviceId));
            }

            // both exist
            if (local2 != null && cloud2 != null)
            {
                var localDeviceId = local2.deviceId ?? string.Empty;
                var cloudDeviceId = cloud2.DeviceId ?? string.Empty;

                if (!string.Equals(localDeviceId, cloudDeviceId, StringComparison.Ordinal))
                {
                    return CommonResult<SyncResult>.Success(new SyncResult(
                        SyncState.Conflict, slot, local2, cloud2, localDeviceId, cloudDeviceId));
                }

                return CommonResult<SyncResult>.Success(new SyncResult(
                    SyncState.Success, slot, local2, cloud2, localDeviceId, cloudDeviceId));
            }

            // fallback (should not reach)
            return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Success, slot));
        }

        public async Task<CommonResult<bool>> ResolveConflictAsync(
            string slot, SyncResolution resolution, CancellationToken ct)
        {
            var loginType = AccountManager.Instance._getCurrentLoginType();

            if (isLocalOnly(loginType))
            {
                return CommonResult<bool>.Failure(
                    CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED,
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
                        var localR = await loadLocalRecordAsync(slot, ct);
                        if (localR.IsFailure)
                            return CommonResult<bool>.Failure(localR.Error!);
                        if (localR.Value == null)
                            return CommonResult<bool>.Failure(CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED, "Local payload is null.");

                        var jsonR = decryptLocalPayloadToJson(localR.Value);
                        if (jsonR.IsFailure)
                            return CommonResult<bool>.Failure(jsonR.Error!);

                        var saveCloud = await saveCloudAsync(slot, jsonR.Value, ct);
                        if (saveCloud.IsFailure)
                            return CommonResult<bool>.Failure(saveCloud.Error!);

                        return CommonResult<bool>.Success(true);
                    }

                    case SyncResolution.UseCloud:
                    {
                        var cloudR = await loadCloudRecordAsync(slot, ct);
                        if (cloudR.IsFailure)
                            return CommonResult<bool>.Failure(cloudR.Error!);
                        if (cloudR.Value == null)
                            return CommonResult<bool>.Failure(CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED, "Cloud payload is null.");

                        var jsonR = decryptCloudPayloadToJson(cloudR.Value);
                        if (jsonR.IsFailure)
                            return CommonResult<bool>.Failure(jsonR.Error!);

                        var saveLocalR = await saveLocalAsync(slot, jsonR.Value, ct);
                        if (saveLocalR.IsFailure)
                            return CommonResult<bool>.Failure(saveLocalR.Error!);

                        // Re-save same payload to cloud to update deviceId and prevent next Sync conflict.
                        var cloudSave = await saveCloudAsync(slot, jsonR.Value, ct);
                        if (cloudSave.IsFailure)
                            return CommonResult<bool>.Failure(cloudSave.Error!);

                        return CommonResult<bool>.Success(true);
                    }

                    default:
                        return CommonResult<bool>.Failure(CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED, $"Unknown resolution: {resolution}");
                }
            }
            catch (OperationCanceledException ex)
            {
                return CommonResult<bool>.Failure(
                    new CommonError(CommonErrorType.LOGIN_SYNC_CANCELLED, "Resolve cancelled.", ex.Message));
            }
        }

        // ──────────────────────────────────────────────
        //  Public: Save API
        // ──────────────────────────────────────────────

        public Task<CommonResult<bool>> SaveDataAsync(string slot, string data, CancellationToken ct)
        {
            return SaveDataAsync(slot, data, includeCloud: false, ct);
        }

        public async Task<CommonResult<bool>> SaveDataAsync(
            string slot, string data, bool includeCloud, CancellationToken ct)
        {
            var local = await saveLocalAsync(slot, data, ct);
            if (local.IsFailure) return local;

            if (!includeCloud)
                return CommonResult<bool>.Success(true);

            // In Editor / Guest, silently ignore cloud save and return success.
            var loginType = AccountManager.Instance._getCurrentLoginType();
            if (isLocalOnly(loginType))
            {
                return CommonResult<bool>.Success(true);
            }

            var init = await _initializeCloudAsync(ct);
            if (init.IsFailure)
                return CommonResult<bool>.Failure(init.Error!);

            var cloud = await saveCloudAsync(slot, data, ct);
            if (cloud.IsFailure) return cloud;

            return CommonResult<bool>.Success(true);
        }

        public Task<CommonResult<bool>> SaveDataAsync<T>(string slot, T data, CancellationToken ct)
        {
            return SaveDataAsync<T>(slot, data, includeCloud: false, ct);
        }

        public Task<CommonResult<bool>> SaveDataAsync<T>(
            string slot, T data, bool includeCloud, CancellationToken ct)
        {
            var json = JsonUtility.ToJson(data);
            return SaveDataAsync(slot, json, includeCloud, ct);
        }

        // ──────────────────────────────────────────────
        //  Public: Clear Slot API
        // ──────────────────────────────────────────────

        public async Task<CommonResult<bool>> ClearSlotAsync(string slot, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled.");

            if (string.IsNullOrWhiteSpace(slot))
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");

            // 1) Local delete (idempotent)
            if (!_slotConfig.TryResolveLocalFilename(slot, out var filename))
            {
                return CommonResult<bool>.Failure(
                    CommonErrorType.LOCALSAVE_FILENAME_INVALID,
                    $"Filename resolve failed. slot='{slot}'.");
            }

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
            var loginType = AccountManager.Instance._getCurrentLoginType();
            if (isLocalOnly(loginType))
            {
                // Guest/Editor: cloud is silently ignored
                return CommonResult<bool>.Success(true);
            }

            // Initialize cloud; if failed, silently skip (policy: local can proceed)
            var init = await _initializeCloudAsync(ct);
            if (init.IsFailure)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] ClearSlotAsync: cloud init failed, skipping cloud delete. slot='{slot}' err={init.Error}");
                return CommonResult<bool>.Success(true);
            }

            if (_cloudClient == null || !_cloudClient.IsAvailable)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] ClearSlotAsync: cloud client not available, skipping cloud delete. slot='{slot}'");
                return CommonResult<bool>.Success(true);
            }

            if (!_slotConfig.TryResolveCloudSlot(slot, out var cloudSlot))
            {
                return CommonResult<bool>.Failure(
                    CommonErrorType.CLOUDSAVE_SLOT_MISSING,
                    $"Cloud slot resolve failed. slot='{slot}'.");
            }

            var del = await _cloudClient.DeleteAsync(cloudSlot, ct);
            if (del != SaveCloudResult.Success)
            {
                // Cloud delete 실패는 "로컬은 이미 삭제됨" 정책상 실패로 올리지 않고 warn 처리(최소 변경).
                UnityEngine.Debug.LogWarning(
                    $"[SaveDataManager] ClearSlotAsync: cloud delete failed. slot='{slot}' cloudSlot='{cloudSlot}' result={del}");
            }

            return CommonResult<bool>.Success(true);
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
#endif
            if (_cloudClient == null)
            {
                _cloudClient = createDefaultClient();
            }

            return signInCloudInternal(ct);
        }

        internal bool _isCloudAvailable => _cloudClient != null && _cloudClient.IsAvailable;

        // ──────────────────────────────────────────────
        //  Private: Local save operations
        // ──────────────────────────────────────────────

        private CommonResult<SaveLocalPayload> loadLocalRecord(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");
            }

            if (!_slotConfig.TryResolveLocalFilename(slot, out var filename))
            {
                return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_SLOT_MISSING, $"Slot '{slot}' not configured.");
            }

            if (!IsValidJsonFilename(filename, out var fnError))
            {
                return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_FILENAME_INVALID, fnError);
            }

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

        private Task<CommonResult<SaveLocalPayload>> loadLocalRecordAsync(string slot, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(
                    CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            return Task.FromResult(loadLocalRecord(slot));
        }

        private CommonResult<bool> saveLocal(string slot, string data)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");
            }

            if (!_slotConfig.TryResolveLocalFilename(slot, out var filename))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_SLOT_MISSING, $"Slot '{slot}' not configured.");
            }

            if (!IsValidJsonFilename(filename, out var fnError))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_FILENAME_INVALID, fnError);
            }

            var plain = data ?? string.Empty;
            var obfuscated = ComplexUtil.Encrypt_Base64(plain);

            var save = new SaveLocalPayload(
                SchemaVersion,
                nowUpdateTime(),
                obfuscated,
                _getOrCreateDeviceId()
            );

            var write = SaveLocalFileStore.WriteAtomic(getRootPath(), filename, save);
            return write.IsSuccess
                ? CommonResult<bool>.Success(true)
                : CommonResult<bool>.Failure(write.Error!);
        }

        private Task<CommonResult<bool>> saveLocalAsync(string slot, string data, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            return Task.FromResult(saveLocal(slot, data));
        }

        // ──────────────────────────────────────────────
        //  Private: Cloud save operations
        // ──────────────────────────────────────────────

        private Task<CommonResult<SaveCloudPayload>> loadCloudRecordAsync(string slot, CancellationToken ct)
        {
#if UNITY_EDITOR
            return Task.FromResult(editorNoCloud<SaveCloudPayload>());
#endif
            if (string.IsNullOrWhiteSpace(slot))
                return Task.FromResult(
                    CommonResult<SaveCloudPayload>.Failure(CommonErrorType.CLOUDSAVE_SLOT_EMPTY, "Slot is empty."));

            if (_cloudClient == null)
                return Task.FromResult(
                    CommonResult<SaveCloudPayload>.Failure(CommonErrorType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            if (!_slotConfig.TryResolveCloudSlot(slot, out var cloudSlot))
                return Task.FromResult(
                    CommonResult<SaveCloudPayload>.Failure(CommonErrorType.CLOUDSAVE_SLOT_MISSING, $"Slot '{slot}' not configured."));

            return loadCloudRecordInternal(cloudSlot, ct);
        }

        private Task<CommonResult<bool>> saveCloudAsync(string slot, string data, CancellationToken ct)
        {
#if UNITY_EDITOR
            return Task.FromResult(editorNoCloud<bool>());
#endif
            if (string.IsNullOrWhiteSpace(slot))
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_SLOT_EMPTY, "Slot is empty."));

            if (_cloudClient == null)
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            if (!_slotConfig.TryResolveCloudSlot(slot, out var cloudSlot))
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_SLOT_MISSING, $"Slot '{slot}' not configured."));

            if (!isLikelyJson(data))
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_PAYLOAD_INVALID,
                        "Payload must be JSON (object or array)."));

            return saveCloudInternal(cloudSlot, data, ct);
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
                _getOrCreateDeviceId()
            );

            var r = await _cloudClient.SaveAsync(cloudSlot, csPayload, ct);
            return r == SaveCloudResult.Success
                ? CommonResult<bool>.Success(true)
                : CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_SAVE, $"Save failed: {r}");
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
                return CommonResult<SaveCloudPayload>.Failure(CommonErrorType.CLOUDSAVE_LOAD, $"Load failed: {result}");
            }

            if (loaded == null)
            {
                return CommonResult<SaveCloudPayload>.Success(null);
            }

            // Payload Contract (Obfuscated-only):
            // - 반환 SaveCloudPayload.Payload 는 저장 포맷 그대로(난독화 시 obfuscated)여야 한다.
            return CommonResult<SaveCloudPayload>.Success(loaded);
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
        //  Private: Sync implementation
        // ──────────────────────────────────────────────

        private async Task<CommonResult<SyncResult>> syncAsync(CancellationToken ct)
        {
            try
            {
                var hasCloudConnectionFailure = false;
                var slotSet = new HashSet<string>(StringComparer.Ordinal);

                var localKeys = _slotConfig.GetLocalSlotKeys();
                for (var i = 0; i < localKeys.Count; i++)
                {
                    var k = localKeys[i];
                    if (!string.IsNullOrWhiteSpace(k)) slotSet.Add(k);
                }

                var cloudKeys = _slotConfig.GetCloudSlotKeys();
                for (var i = 0; i < cloudKeys.Count; i++)
                {
                    var k = cloudKeys[i];
                    if (!string.IsNullOrWhiteSpace(k)) slotSet.Add(k);
                }

                var hasAnyLocal = false;
                var hasAnyCloud = false;

                foreach (var slot in slotSet)
                {
                    ct.ThrowIfCancellationRequested();

                    var localR = await loadLocalRecordAsync(slot, ct);
                    if (localR.IsFailure)
                    {
                        return CommonResult<SyncResult>.Failure(
                            new CommonError(CommonErrorType.LOGIN_SYNC_LOAD_LOCAL_FAILED, $"Sync load local failed. slot='{slot}'", localR.Error!.ToString()));
                    }

                    var cloudWritable = true;

                    var cloudR = await loadCloudRecordAsync(slot, ct);
                    if (cloudR.IsFailure)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[SaveDataManager] Sync load cloud failed. slot='{slot}'. " +
                            $"Proceeding with local-only. error={cloudR.Error}");

                        hasCloudConnectionFailure = true;
                        cloudWritable = false;
                    }

                    var local = localR.Value;
                    var cloud = cloudR.IsSuccess ? cloudR.Value : null;

                    if (local != null) hasAnyLocal = true;
                    if (cloud != null) hasAnyCloud = true;

                    if (local == null && cloud == null)
                    {
                        continue;
                    }

                    if (local == null && cloud != null)
                    {
                        var jsonR = decryptCloudPayloadToJson(cloud);
                        if (jsonR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_LOCAL_FAILED, $"Sync decrypt cloud failed. slot='{slot}'", jsonR.Error!.ToString()));
                        }

                        var saveLocalR = await saveLocalAsync(slot, jsonR.Value, ct);
                        if (saveLocalR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_LOCAL_FAILED, $"Sync save local failed. slot='{slot}'", saveLocalR.Error!.ToString()));
                        }

                        // Re-save to cloud to update deviceId and prevent next Sync conflict.
                        var cloudResaveR = await saveCloudAsync(slot, jsonR.Value, ct);
                        if (cloudResaveR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_CLOUD_FAILED, $"Sync re-save cloud failed. slot='{slot}'", cloudResaveR.Error!.ToString()));
                        }
                        continue;
                    }

                    if (local != null && cloud == null)
                    {
                        if (!cloudWritable)
                        {
                            continue;
                        }

                        var jsonR = decryptLocalPayloadToJson(local);
                        if (jsonR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_CLOUD_FAILED, $"Sync decrypt local failed. slot='{slot}'", jsonR.Error!.ToString()));
                        }

                        var saveCloudR = await saveCloudAsync(slot, jsonR.Value, ct);
                        if (saveCloudR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_CLOUD_FAILED, $"Sync save cloud failed. slot='{slot}'", saveCloudR.Error!.ToString()));
                        }
                        continue;
                    }

                    // Both exist: check deviceId
                    var localDeviceId = local.deviceId ?? string.Empty;
                    var cloudDeviceId = cloud.DeviceId ?? string.Empty;

                    if (!string.Equals(localDeviceId, cloudDeviceId, StringComparison.Ordinal))
                    {
                        return CommonResult<SyncResult>.Success(new SyncResult(
                            SyncState.Conflict,
                            slot,
                            local,
                            cloud,
                            localDeviceId,
                            cloudDeviceId));
                    }
                }

                if (!hasAnyLocal && !hasAnyCloud)
                {
                    if (hasCloudConnectionFailure)
                        return CommonResult<SyncResult>.Success(new SyncResult(SyncState.ConnectionFailed));
                    return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Initial));
                }

                if (hasCloudConnectionFailure && !hasAnyLocal)
                    return CommonResult<SyncResult>.Success(new SyncResult(SyncState.ConnectionFailed));
                return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Success));
            }
            catch (OperationCanceledException ex)
            {
                return CommonResult<SyncResult>.Failure(
                    new CommonError(CommonErrorType.LOGIN_SYNC_CANCELLED, "Sync cancelled.", ex.Message));
            }
        }

        // ──────────────────────────────────────────────
        //  Private: Shared helpers
        // ──────────────────────────────────────────────

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
