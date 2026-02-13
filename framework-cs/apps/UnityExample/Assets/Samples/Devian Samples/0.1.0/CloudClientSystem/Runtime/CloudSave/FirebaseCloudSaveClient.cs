using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Firebase;
using Firebase.Auth;
using Firebase.Firestore;


namespace Devian
{
    /// <summary>
    /// Firebase Firestore cloud save client.
    /// Uses Firebase Auth + Firestore for cloud save storage.
    /// Does NOT own the login flow — LoginManager must sign in before CloudSave.
    /// </summary>
    public sealed class FirebaseCloudSaveClient : ICloudSaveClient
    {
        private const string UsersCollection = "users";
        private const string CloudSaveSubCollection = "cloudSave";

        private bool _initialized;
        private FirebaseAuth _auth;
        private FirebaseFirestore _db;

        public bool IsAvailable => _initialized && _auth != null && _db != null;


        public async Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var init = await ensureInitializedAsync(ct);
            if (init != CloudSaveResult.Success)
            {
                UnityEngine.Debug.LogError($"[FirebaseCloudSaveClient] init failed: {init}");
                return init;
            }

            // CloudSave does NOT own the login flow.
            // LoginManager must have signed in already (anonymous or other).
            if (_auth.CurrentUser != null)
            {
                return CloudSaveResult.Success;
            }

            UnityEngine.Debug.LogError("[FirebaseCloudSaveClient] AuthRequired: FirebaseAuth.CurrentUser is null. Login flow must sign in before CloudSave.");
            return CloudSaveResult.AuthRequired;
        }


        public async Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var auth = await SignInIfNeededAsync(ct);
            if (auth != CloudSaveResult.Success)
            {
                return (auth, null);
            }

            var uid = _auth.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid))
            {
                return (CloudSaveResult.AuthRequired, null);
            }

            try
            {
                var doc = userSlotDoc(uid, slot);
                var snap = await doc.GetSnapshotAsync();
                ct.ThrowIfCancellationRequested();

                if (!snap.Exists)
                {
                    return (CloudSaveResult.NotFound, null);
                }

                var dict = snap.ToDictionary();

                var payload = new CloudSavePayload(
                    version: dict.ContainsKey("Version") ? Convert.ToInt32(dict["Version"]) : 0,
                    updateTime: dict.ContainsKey("UpdateTime") ? Convert.ToString(dict["UpdateTime"]) : null,
                    utcTime: dict.ContainsKey("UtcTime") ? Convert.ToInt64(dict["UtcTime"]) : 0L,
                    payload: dict.ContainsKey("Payload") ? Convert.ToString(dict["Payload"]) : null,
                    checksum: dict.ContainsKey("Checksum") ? Convert.ToString(dict["Checksum"]) : null
                );

                return (CloudSaveResult.Success, payload);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FirebaseCloudSaveClient] LoadAsync failed. slot='{slot}' ex={ex}");
                return (CloudSaveResult.TemporaryFailure, null);
            }
        }


        public async Task<CloudSaveResult> SaveAsync(
            string slot, CloudSavePayload payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var auth = await SignInIfNeededAsync(ct);
            if (auth != CloudSaveResult.Success)
            {
                return auth;
            }

            var uid = _auth.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid))
            {
                return CloudSaveResult.AuthRequired;
            }

            try
            {
                var doc = userSlotDoc(uid, slot);

                var data = new Dictionary<string, object>
                {
                    ["Version"] = payload.Version,
                    ["UpdateTime"] = payload.UpdateTime,
                    ["UtcTime"] = payload.UtcTime,
                    ["Payload"] = payload.Payload,
                    ["Checksum"] = payload.Checksum,
                };

                await doc.SetAsync(data, SetOptions.MergeAll);
                ct.ThrowIfCancellationRequested();

                return CloudSaveResult.Success;
            }
            catch
            {
                return CloudSaveResult.TemporaryFailure;
            }
        }


        public async Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var auth = await SignInIfNeededAsync(ct);
            if (auth != CloudSaveResult.Success)
            {
                return auth;
            }

            var uid = _auth.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid))
            {
                return CloudSaveResult.AuthRequired;
            }

            try
            {
                var doc = userSlotDoc(uid, slot);
                await doc.DeleteAsync();
                ct.ThrowIfCancellationRequested();

                return CloudSaveResult.Success;
            }
            catch
            {
                return CloudSaveResult.TemporaryFailure;
            }
        }


        private async Task<CloudSaveResult> ensureInitializedAsync(CancellationToken ct)
        {
            if (_initialized)
            {
                return CloudSaveResult.Success;
            }

            try
            {
                var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
                ct.ThrowIfCancellationRequested();

                if (dep != DependencyStatus.Available)
                {
                    _initialized = false;
                    return CloudSaveResult.NotAvailable;
                }

                _auth = FirebaseAuth.DefaultInstance;
                _db = FirebaseFirestore.DefaultInstance;

                _initialized = true;
                return CloudSaveResult.Success;
            }
            catch (Exception ex)
            {
                _initialized = false;
                UnityEngine.Debug.LogError($"[FirebaseCloudSaveClient] ensureInitializedAsync exception: {ex}");
                return CloudSaveResult.NotAvailable;
            }
        }


        private DocumentReference userSlotDoc(string uid, string slot)
        {
            return _db
                .Collection(UsersCollection).Document(uid)
                .Collection(CloudSaveSubCollection).Document(slot);
        }
    }
}
