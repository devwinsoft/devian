using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;


namespace Devian
{
    /// <summary>
    /// Google Play Games Saved Games (cloud save) client.
    /// - Reflection based: compiles even when GooglePlayGames plugin is not present.
    /// - Works only on Android runtime with plugin installed &amp; configured.
    /// </summary>
    public sealed class GoogleCloudSaveClient : ICloudSaveClient
    {
        private const string _gpgsAsmName = "GooglePlayGames";
        private const string _playGamesPlatformTypeName = "GooglePlayGames.PlayGamesPlatform, GooglePlayGames";
        private const string _dataSourceTypeName = "GooglePlayGames.BasicApi.SavedGame.DataSource, GooglePlayGames";
        private const string _conflictStrategyTypeName = "GooglePlayGames.BasicApi.SavedGame.ConflictResolutionStrategy, GooglePlayGames";
        private const string _savedGameRequestStatusTypeName = "GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus, GooglePlayGames";
        private const string _savedGameClientTypeName = "GooglePlayGames.BasicApi.SavedGame.ISavedGameClient, GooglePlayGames";
        private const string _savedGameMetadataTypeName = "GooglePlayGames.BasicApi.SavedGame.ISavedGameMetadata, GooglePlayGames";
        private const string _savedGameMetadataUpdateBuilderTypeName = "GooglePlayGames.BasicApi.SavedGame.SavedGameMetadataUpdate+Builder, GooglePlayGames";

        public bool IsAvailable => _isAvailable;

        private readonly bool _isAvailable;
        private readonly Type _playGamesPlatformType;
        private readonly object _playGamesPlatformInstance; // PlayGamesPlatform.Instance
        private readonly object _savedGameClient;           // PlayGamesPlatform.Instance.SavedGame

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly Type _statusType;
        private readonly Type _metadataType;
        private readonly object _statusSuccess;
        private readonly object _dsReadNetworkFirst;
        private readonly object _strategyLongestPlaytime;
        private readonly MethodInfo _openMethod;
        private readonly MethodInfo _readMethod;
        private readonly MethodInfo _commitMethod;
        private readonly MethodInfo _deleteMethod;
        private readonly Type _builderType;
#endif

        public GoogleCloudSaveClient()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _playGamesPlatformType = Type.GetType(_playGamesPlatformTypeName);
                if (_playGamesPlatformType == null)
                {
                    _isAvailable = false;
                    return;
                }

                _playGamesPlatformInstance = _getStaticProperty(_playGamesPlatformType, "Instance");
                if (_playGamesPlatformInstance == null)
                {
                    _isAvailable = false;
                    return;
                }

                _savedGameClient = _getInstanceProperty(_playGamesPlatformInstance, "SavedGame");
                if (_savedGameClient == null)
                {
                    _isAvailable = false;
                    return;
                }

                // Resolve types
                _statusType = Type.GetType(_savedGameRequestStatusTypeName);
                _metadataType = Type.GetType(_savedGameMetadataTypeName);
                var dsType = Type.GetType(_dataSourceTypeName);
                var stType = Type.GetType(_conflictStrategyTypeName);
                _builderType = Type.GetType(_savedGameMetadataUpdateBuilderTypeName);

                if (_statusType == null || _metadataType == null || dsType == null || stType == null)
                {
                    _isAvailable = false;
                    return;
                }

                // Enum values
                _statusSuccess = Enum.Parse(_statusType, "Success");
                _dsReadNetworkFirst = Enum.Parse(dsType, "ReadNetworkFirst");
                _strategyLongestPlaytime = Enum.Parse(stType, "UseLongestPlaytime");

                // Method resolution
                var clientType = Type.GetType(_savedGameClientTypeName) ?? _savedGameClient.GetType();
                _openMethod = clientType.GetMethod("OpenWithAutomaticConflictResolution");
                _readMethod = clientType.GetMethod("ReadBinaryData");
                _commitMethod = clientType.GetMethod("CommitUpdate");
                _deleteMethod = clientType.GetMethod("Delete");

                _isAvailable = _openMethod != null && _readMethod != null && _commitMethod != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GoogleCloudSaveClient] init failed: {e.Message}");
                _isAvailable = false;
            }
#else
            _isAvailable = false;
#endif
        }

        // ───── ICloudSaveClient ─────

        public Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_isAvailable) return Task.FromResult(CloudSaveResult.NotAvailable);

            // Use Unity Social API (GPGS overrides Social when activated in project).
            if (Social.localUser != null && Social.localUser.authenticated)
                return Task.FromResult(CloudSaveResult.Success);

            var tcs = new TaskCompletionSource<CloudSaveResult>();

            if (ct.CanBeCanceled)
                ct.Register(() => tcs.TrySetResult(CloudSaveResult.TemporaryFailure));

            try
            {
                Social.localUser.Authenticate(success =>
                {
                    tcs.TrySetResult(success ? CloudSaveResult.Success : CloudSaveResult.AuthRequired);
                });
            }
            catch
            {
                tcs.TrySetResult(CloudSaveResult.FatalFailure);
            }

            return tcs.Task;
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }

        public Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_isAvailable)
                return Task.FromResult((CloudSaveResult.NotAvailable, (CloudSavePayload)null));
            return _loadInternal(slot, ct);
#else
            return Task.FromResult((CloudSaveResult.NotAvailable, (CloudSavePayload)null));
#endif
        }

        public Task<CloudSaveResult> SaveAsync(
            string slot, CloudSavePayload payload, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_isAvailable) return Task.FromResult(CloudSaveResult.NotAvailable);
            return _saveInternal(slot, payload, ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }

        public Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_isAvailable) return Task.FromResult(CloudSaveResult.NotAvailable);
            return _deleteInternal(slot, ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }

        // ───── Internal async (Android only) ─────

#if UNITY_ANDROID && !UNITY_EDITOR

        private async Task<(CloudSaveResult, CloudSavePayload)> _loadInternal(
            string slot, CancellationToken ct)
        {
            try
            {
                // 1) OpenWithAutomaticConflictResolution
                var (openSt, meta) = await _openSavedGame(slot);
                if (!_isSuccess(openSt)) return (_mapStatus(openSt), null);

                // 2) ReadBinaryData
                var (readSt, data) = await _readBinaryData(meta);
                if (!_isSuccess(readSt)) return (_mapStatus(readSt), null);

                var bytes = data as byte[];
                if (bytes == null || bytes.Length == 0) return (CloudSaveResult.NotFound, null);

                // 3) Deserialize
                string json = Encoding.UTF8.GetString(bytes);
                var payload = JsonUtility.FromJson<CloudSavePayload>(json);
                return (CloudSaveResult.Success, payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GPGS] LoadAsync failed: {e.Message}");
                return (CloudSaveResult.FatalFailure, null);
            }
        }

        private async Task<CloudSaveResult> _saveInternal(
            string slot, CloudSavePayload payload, CancellationToken ct)
        {
            try
            {
                // 1) OpenWithAutomaticConflictResolution
                var (openSt, meta) = await _openSavedGame(slot);
                if (!_isSuccess(openSt)) return _mapStatus(openSt);

                // 2) Serialize
                string json = JsonUtility.ToJson(payload);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                // 3) Build metadata update
                object metaUpdate = _buildMetadataUpdate("cloudsave");

                // 4) CommitUpdate
                var (commitSt, _) = await _commitUpdate(meta, metaUpdate, bytes);
                return _isSuccess(commitSt) ? CloudSaveResult.Success : _mapStatus(commitSt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GPGS] SaveAsync failed: {e.Message}");
                return CloudSaveResult.FatalFailure;
            }
        }

        private async Task<CloudSaveResult> _deleteInternal(string slot, CancellationToken ct)
        {
            try
            {
                var (openSt, meta) = await _openSavedGame(slot);
                if (!_isSuccess(openSt)) return _mapStatus(openSt);

                // Delete is synchronous in GPGS
                _deleteMethod?.Invoke(_savedGameClient, new[] { meta });
                return CloudSaveResult.Success;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GPGS] DeleteAsync failed: {e.Message}");
                return CloudSaveResult.FatalFailure;
            }
        }

        // ───── GPGS Reflection wrappers ─────

        private Task<(object status, object result)> _openSavedGame(string filename)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            var callback = _makeCallback(_statusType, _metadataType, tcs);
            _openMethod.Invoke(_savedGameClient, new[]
            {
                (object)filename, _dsReadNetworkFirst, _strategyLongestPlaytime, callback
            });
            return tcs.Task;
        }

        private Task<(object status, object data)> _readBinaryData(object metadata)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            var callback = _makeCallback(_statusType, typeof(byte[]), tcs);
            _readMethod.Invoke(_savedGameClient, new[] { metadata, callback });
            return tcs.Task;
        }

        private Task<(object status, object result)> _commitUpdate(
            object metadata, object metaUpdate, byte[] data)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            var callback = _makeCallback(_statusType, _metadataType, tcs);
            _commitMethod.Invoke(_savedGameClient,
                new[] { metadata, metaUpdate, (object)data, callback });
            return tcs.Task;
        }

        private object _buildMetadataUpdate(string description)
        {
            if (_builderType == null) return null;
            var builder = Activator.CreateInstance(_builderType);
            var withDesc = _builderType.GetMethod("WithUpdatedDescription");
            if (withDesc != null)
                builder = withDesc.Invoke(builder, new object[] { description });
            var buildMethod = _builderType.GetMethod("Build");
            return buildMethod?.Invoke(builder, null);
        }

        // ───── Callback relay (generic → Delegate for GPGS callbacks) ─────

        private sealed class _Relay<T1, T2>
        {
            public TaskCompletionSource<(object, object)> Tcs;
            public void OnResult(T1 a, T2 b) => Tcs?.TrySetResult(((object)a, (object)b));
        }

        private static Delegate _makeCallback(
            Type t1, Type t2, TaskCompletionSource<(object, object)> tcs)
        {
            Type relayType = typeof(_Relay<,>).MakeGenericType(t1, t2);
            object relay = Activator.CreateInstance(relayType);
            relayType.GetField("Tcs").SetValue(relay, tcs);
            MethodInfo method = relayType.GetMethod("OnResult");
            Type actionType = typeof(Action<,>).MakeGenericType(t1, t2);
            return Delegate.CreateDelegate(actionType, relay, method);
        }

        // ───── Status mapping ─────

        private bool _isSuccess(object status)
        {
            return status != null && status.Equals(_statusSuccess);
        }

        private static CloudSaveResult _mapStatus(object status)
        {
            if (status == null) return CloudSaveResult.FatalFailure;

            int val;
            try
            {
                // status is a boxed enum (SavedGameRequestStatus). Convert handles boxed-enum safely.
                val = Convert.ToInt32(status);
            }
            catch
            {
                return CloudSaveResult.FatalFailure;
            }

            // GPGS SavedGameRequestStatus:
            //   Success = 1, TimeoutError = 2, InternalError = 3,
            //   BadInputError = 4, AuthenticationError = 5
            switch (val)
            {
                case 1:  return CloudSaveResult.Success;
                case 2:  return CloudSaveResult.TemporaryFailure;
                case 5:  return CloudSaveResult.AuthRequired;
                default: return CloudSaveResult.FatalFailure;
            }
        }

#endif

        // ───── Property helpers ─────

        private static object _getStaticProperty(Type type, string name)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null);
        }

        private static object _getInstanceProperty(object obj, string name)
        {
            var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(obj);
        }
    }
}
