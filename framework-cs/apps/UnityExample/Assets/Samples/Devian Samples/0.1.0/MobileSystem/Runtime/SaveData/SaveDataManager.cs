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

        public async Task<CommonResult<SyncResult>> SyncAsync(CancellationToken ct)
        {
            var loginType = AccountManager.Instance._getCurrentLoginType();
#if UNITY_EDITOR
            var isEditorNoCloud = true;
#else
            var isEditorNoCloud = (loginType == LoginType.EditorLogin);
#endif

            if (loginType == LoginType.GuestLogin || isEditorNoCloud)
            {
                var localKeys = getLocalSlotKeys();
                var hasAnyLocal = false;
                for (var i = 0; i < localKeys.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(localKeys[i])) continue;
                    var r = await loadLocalRecordAsync(localKeys[i], ct);
                    if (r.IsSuccess && r.Value != null) { hasAnyLocal = true; break; }
                }
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
                }
            }

            return await syncAsync(ct);
        }

        public async Task<CommonResult<bool>> ResolveConflictAsync(
            string slot, SyncResolution resolution, CancellationToken ct)
        {
            var loginType = AccountManager.Instance._getCurrentLoginType();
#if UNITY_EDITOR
            var isEditorNoCloud = true;
#else
            var isEditorNoCloud = (loginType == LoginType.EditorLogin);
#endif

            if (loginType == LoginType.GuestLogin || isEditorNoCloud)
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

                        var saveCloud = await saveCloudAsync(slot, localR.Value.payload, ct);
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

                        var saveLocalR = await saveLocalAsync(slot, cloudR.Value.Payload, ct);
                        if (saveLocalR.IsFailure)
                            return CommonResult<bool>.Failure(saveLocalR.Error!);

                        // Re-save same payload to cloud to update deviceId and prevent next Sync conflict.
                        var cloudSave = await saveCloudAsync(slot, cloudR.Value.Payload, ct);
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

        public Task<CommonResult<bool>> SaveDataLocalAsync(string slot, string payload, CancellationToken ct)
        {
            return saveLocalAsync(slot, payload, ct);
        }

        public async Task<CommonResult<bool>> SaveDataLocalAndCloudAsync(
            string slot, string payload, CancellationToken ct)
        {
            var local = await saveLocalAsync(slot, payload, ct);
            if (local.IsFailure) return local;

            var init = await _initializeCloudAsync(ct);
            if (init.IsFailure)
                return CommonResult<bool>.Failure(init.Error!);

            var cloud = await saveCloudAsync(slot, payload, ct);
            if (cloud.IsFailure) return cloud;

            return CommonResult<bool>.Success(true);
        }

        // ──────────────────────────────────────────────
        //  Public: Payload parsing
        // ──────────────────────────────────────────────

        public static CommonResult<T> ParsePayloadResult<T>(SaveLocalPayload payload)
        {
            if (payload == null)
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveLocalPayload is null.");

            var json = payload.payload;
            if (string.IsNullOrWhiteSpace(json))
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveLocalPayload json is empty.");

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
            if (payload == null)
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveCloudPayload is null.");

            var json = payload.Payload;
            if (string.IsNullOrWhiteSpace(json))
                return CommonResult<T>.Failure(CommonErrorType.SAVEDATA_PAYLOAD_PARSE_FAILED, "SaveCloudPayload json is empty.");

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
        //  Public: Key/IV management
        // ──────────────────────────────────────────────

        public void GetKeyIvBase64(out string keyBase64, out string ivBase64)
        {
            _slotConfig.GetKeyIvBase64(out keyBase64, out ivBase64);
        }

        public CommonResult<bool> SetKeyIvBase64(string keyBase64, string ivBase64)
        {
            return _slotConfig.SetKeyIvBase64(keyBase64, ivBase64);
        }

        public void ClearKeyIv()
        {
            _slotConfig.ClearKeyIv();
        }

        // ──────────────────────────────────────────────
        //  Private: Local save operations
        // ──────────────────────────────────────────────

        private List<string> getLocalSlotKeys()
        {
            return _slotConfig.GetLocalSlotKeys();
        }

        private CommonResult<SaveLocalPayload> loadLocalRecord(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");
            }

            if (!tryResolveLocalFilename(slot, out var filename))
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

            byte[] key = null;
            byte[] iv = null;

            if (_slotConfig.useEncryption && !tryGetKeyIv(out key, out iv, out var keyError))
            {
                return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_KEYIV, keyError);
            }

            var plain = _slotConfig.useEncryption
                ? Crypto.DecryptAes(save.payload, key, iv)
                : save.payload;

            return CommonResult<SaveLocalPayload>.Success(
                new SaveLocalPayload(save.version, save.updateTime, plain, _getOrCreateDeviceId()));
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

        private CommonResult<bool> saveLocal(string slot, string payload)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");
            }

            if (!tryResolveLocalFilename(slot, out var filename))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_SLOT_MISSING, $"Slot '{slot}' not configured.");
            }

            if (!IsValidJsonFilename(filename, out var fnError))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_FILENAME_INVALID, fnError);
            }

            var plain = payload ?? string.Empty;

            byte[] key = null;
            byte[] iv = null;

            if (_slotConfig.useEncryption && !tryGetKeyIv(out key, out iv, out var keyError))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, keyError);
            }

            var cipher = _slotConfig.useEncryption
                ? Crypto.EncryptAes(plain, key, iv)
                : plain;

            var save = new SaveLocalPayload(
                SchemaVersion,
                nowUpdateTime(),
                cipher,
                _getOrCreateDeviceId()
            );

            var write = SaveLocalFileStore.WriteAtomic(getRootPath(), filename, save);
            return write.IsSuccess
                ? CommonResult<bool>.Success(true)
                : CommonResult<bool>.Failure(write.Error!);
        }

        private Task<CommonResult<bool>> saveLocalAsync(string slot, string payload, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            return Task.FromResult(saveLocal(slot, payload));
        }

        // ──────────────────────────────────────────────
        //  Private: Cloud save operations
        // ──────────────────────────────────────────────

        private List<string> getCloudSlotKeys()
        {
            return _slotConfig.GetCloudSlotKeys();
        }

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

            if (!tryResolveCloudSlot(slot, out var cloudSlot))
                return Task.FromResult(
                    CommonResult<SaveCloudPayload>.Failure(CommonErrorType.CLOUDSAVE_SLOT_MISSING, $"Slot '{slot}' not configured."));

            return loadCloudRecordInternal(cloudSlot, ct);
        }

        private Task<CommonResult<bool>> saveCloudAsync(string slot, string payload, CancellationToken ct)
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

            if (!tryResolveCloudSlot(slot, out var cloudSlot))
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_SLOT_MISSING, $"Slot '{slot}' not configured."));

            if (!isLikelyJson(payload))
                return Task.FromResult(
                    CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_PAYLOAD_INVALID,
                        "Payload must be JSON (object or array)."));

            return saveCloudInternal(cloudSlot, payload, ct);
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
            string cloudSlot, string payload, CancellationToken ct)
        {
            var plain = payload ?? string.Empty;

            byte[] key = null;
            byte[] iv = null;

            if (_slotConfig.useEncryption && !tryGetKeyIv(out key, out iv, out var keyError))
            {
                return CommonResult<bool>.Failure(CommonErrorType.CLOUDSAVE_KEYIV, keyError);
            }

            var cipher = _slotConfig.useEncryption
                ? Crypto.EncryptAes(plain, key, iv)
                : plain;

            var csPayload = new SaveCloudPayload(
                SchemaVersion,
                nowUpdateTime(),
                cipher,
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

            byte[] key = null;
            byte[] iv = null;

            if (_slotConfig.useEncryption && !tryGetKeyIv(out key, out iv, out var keyError))
            {
                return CommonResult<SaveCloudPayload>.Failure(CommonErrorType.CLOUDSAVE_KEYIV, keyError);
            }

            var plain = _slotConfig.useEncryption
                ? Crypto.DecryptAes(loaded.Payload, key, iv)
                : loaded.Payload;

            return CommonResult<SaveCloudPayload>.Success(
                new SaveCloudPayload(loaded.Version, loaded.UpdateTime, plain, loaded.DeviceId));
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
                var slotSet = new HashSet<string>(StringComparer.Ordinal);

                var localKeys = getLocalSlotKeys();
                for (var i = 0; i < localKeys.Count; i++)
                {
                    var k = localKeys[i];
                    if (!string.IsNullOrWhiteSpace(k)) slotSet.Add(k);
                }

                var cloudKeys = getCloudSlotKeys();
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
                        var saveLocalR = await saveLocalAsync(slot, cloud.Payload, ct);
                        if (saveLocalR.IsFailure)
                        {
                            return CommonResult<SyncResult>.Failure(
                                new CommonError(CommonErrorType.LOGIN_SYNC_SAVE_LOCAL_FAILED, $"Sync save local failed. slot='{slot}'", saveLocalR.Error!.ToString()));
                        }

                        // Re-save to cloud to update deviceId and prevent next Sync conflict.
                        var cloudResaveR = await saveCloudAsync(slot, cloud.Payload, ct);
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

                        var saveCloudR = await saveCloudAsync(slot, local.payload, ct);
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
                    return CommonResult<SyncResult>.Success(new SyncResult(SyncState.Initial));
                }

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

        private bool tryResolveLocalFilename(string slot, out string filename)
        {
            return _slotConfig.TryResolveLocalFilename(slot, out filename);
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

        private bool tryResolveCloudSlot(string slot, out string cloudSlot)
        {
            return _slotConfig.TryResolveCloudSlot(slot, out cloudSlot);
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

        private bool tryGetKeyIv(out byte[] key, out byte[] iv, out string error)
        {
            return _slotConfig.TryGetKeyIv(out key, out iv, out error);
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

        [ContextMenu("Generate Key/IV (AES-256, Base64)")]
        public void GenerateKeyIv()
        {
            _slotConfig.keyBase64.Value = Convert.ToBase64String(Crypto.GenerateKey());
            _slotConfig.ivBase64.Value = Convert.ToBase64String(Crypto.GenerateIv());
        }

        private void OnValidate()
        {
            if (!_slotConfig.useEncryption) return;

            var keyB64 = _slotConfig.keyBase64.Value;
            var ivB64 = _slotConfig.ivBase64.Value;

            if (string.IsNullOrWhiteSpace(keyB64) || string.IsNullOrWhiteSpace(ivB64))
            {
                GenerateKeyIv();
            }
        }
#endif
    }
}
