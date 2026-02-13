using System;
using System.Collections.Generic;
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
        public LocalSavePayload LocalPayload { get; }
        public CloudSavePayload CloudPayload { get; }
        public string LocalDeviceId { get; }
        public string CloudDeviceId { get; }

        public SyncResult(SyncState state, string slot = null,
            LocalSavePayload localPayload = null, CloudSavePayload cloudPayload = null,
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

    public sealed class SyncDataManager : CompoSingleton<SyncDataManager>
    {
        private const string DeviceIdPrefsKey = "Devian.DeviceId";

        private static string _getOrCreateDeviceId()
        {
            var id = PlayerPrefs.GetString(DeviceIdPrefsKey, null);
            if (!string.IsNullOrEmpty(id)) return id;

            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceIdPrefsKey, id);
            PlayerPrefs.Save();
            return id;
        }

        public async Task<CoreResult<SyncResult>> SyncAsync(CancellationToken ct)
        {
            if (LoginManager.Instance._getCurrentLoginType() == LoginType.GuestLogin)
            {
                var localKeys = LocalSaveManager.Instance.GetSlotKeys();
                var hasAnyLocal = false;
                for (var i = 0; i < localKeys.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(localKeys[i])) continue;
                    var r = await LocalSaveManager.Instance.LoadRecordAsync(localKeys[i], ct);
                    if (r.IsSuccess && r.Value != null) { hasAnyLocal = true; break; }
                }
                var guestState = hasAnyLocal ? SyncState.Success : SyncState.Initial;
                return CoreResult<SyncResult>.Success(new SyncResult(guestState));
            }

            return await syncAsync(ct);
        }

        public async Task<CoreResult<bool>> ResolveConflictAsync(
            string slot, SyncResolution resolution, CancellationToken ct)
        {
            if (LoginManager.Instance._getCurrentLoginType() == LoginType.GuestLogin)
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED, "Guest cannot resolve cloud sync conflicts.");
            }

            try
            {
                switch (resolution)
                {
                    case SyncResolution.UseLocal:
                    {
                        var localR = await LocalSaveManager.Instance.LoadRecordAsync(slot, ct);
                        if (localR.IsFailure)
                            return CoreResult<bool>.Failure(localR.Error!);
                        if (localR.Value == null)
                            return CoreResult<bool>.Failure(CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED, "Local payload is null.");

                        var saveCloud = await CloudSaveManager.Instance.SaveAsync(slot, localR.Value.payload, ct);
                        if (saveCloud.IsFailure)
                            return CoreResult<bool>.Failure(saveCloud.Error!);

                        return CoreResult<bool>.Success(true);
                    }

                    case SyncResolution.UseCloud:
                    {
                        var cloudR = await CloudSaveManager.Instance.LoadRecordAsync(slot, ct);
                        if (cloudR.IsFailure)
                            return CoreResult<bool>.Failure(cloudR.Error!);
                        if (cloudR.Value == null)
                            return CoreResult<bool>.Failure(CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED, "Cloud payload is null.");

                        var saveLocal = await LocalSaveManager.Instance.SaveAsync(slot, cloudR.Value.Payload, ct);
                        if (saveLocal.IsFailure)
                            return CoreResult<bool>.Failure(saveLocal.Error!);

                        return CoreResult<bool>.Success(true);
                    }

                    default:
                        return CoreResult<bool>.Failure(CommonErrorType.LOGIN_SYNC_RESOLVE_FAILED, $"Unknown resolution: {resolution}");
                }
            }
            catch (OperationCanceledException ex)
            {
                return CoreResult<bool>.Failure(
                    new CoreError(CommonErrorType.LOGIN_SYNC_CANCELLED, "Resolve cancelled.", ex.Message));
            }
        }

        private async Task<CoreResult<SyncResult>> syncAsync(CancellationToken ct)
        {
            try
            {
                var slotSet = new HashSet<string>(StringComparer.Ordinal);

                var localKeys = LocalSaveManager.Instance.GetSlotKeys();
                for (var i = 0; i < localKeys.Count; i++)
                {
                    var k = localKeys[i];
                    if (!string.IsNullOrWhiteSpace(k)) slotSet.Add(k);
                }

                var cloudKeys = CloudSaveManager.Instance.GetSlotKeys();
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

                    var localR = await LocalSaveManager.Instance.LoadRecordAsync(slot, ct);
                    if (localR.IsFailure)
                    {
                        return CoreResult<SyncResult>.Failure(
                            new CoreError(CommonErrorType.LOGIN_SYNC_LOAD_LOCAL_FAILED, $"Sync load local failed. slot='{slot}'", localR.Error!.ToString()));
                    }

                    var cloudWritable = true;

                    var cloudR = await CloudSaveManager.Instance.LoadRecordAsync(slot, ct);
                    if (cloudR.IsFailure)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[SyncDataManager] Sync load cloud failed. slot='{slot}'. " +
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
                        var saveLocal = await LocalSaveManager.Instance.SaveAsync(slot, cloud.Payload, ct);
                        if (saveLocal.IsFailure)
                        {
                            return CoreResult<SyncResult>.Failure(
                                new CoreError(CommonErrorType.LOGIN_SYNC_SAVE_LOCAL_FAILED, $"Sync save local failed. slot='{slot}'", saveLocal.Error!.ToString()));
                        }
                        continue;
                    }

                    if (local != null && cloud == null)
                    {
                        if (!cloudWritable)
                        {
                            continue;
                        }

                        var saveCloud = await CloudSaveManager.Instance.SaveAsync(slot, local.payload, ct);
                        if (saveCloud.IsFailure)
                        {
                            return CoreResult<SyncResult>.Failure(
                                new CoreError(CommonErrorType.LOGIN_SYNC_SAVE_CLOUD_FAILED, $"Sync save cloud failed. slot='{slot}'", saveCloud.Error!.ToString()));
                        }
                        continue;
                    }

                    // Both exist: check deviceId
                    var localDeviceId = local.deviceId ?? string.Empty;
                    var cloudDeviceId = cloud.DeviceId ?? string.Empty;

                    if (!string.Equals(localDeviceId, cloudDeviceId, StringComparison.Ordinal))
                    {
                        return CoreResult<SyncResult>.Success(new SyncResult(
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
                    return CoreResult<SyncResult>.Success(new SyncResult(SyncState.Initial));
                }

                return CoreResult<SyncResult>.Success(new SyncResult(SyncState.Success));
            }
            catch (OperationCanceledException ex)
            {
                return CoreResult<SyncResult>.Failure(
                    new CoreError(CommonErrorType.LOGIN_SYNC_CANCELLED, "Sync cancelled.", ex.Message));
            }
        }
    }
}
