using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Devian.Domain.Common;


namespace Devian
{
    /// <summary>
    /// Google Play Games Services (GPGS) v2 manager.
    /// Consolidates GPGS Sign-in and Saved Games (Cloud Save) via Reflection.
    /// Targets GPGS v2 (assembly: Google.Play.Games) only.
    /// Compiles even without GooglePlayGames plugin (Reflection + platform guards).
    /// </summary>
    public sealed class GpgsLoginController
    {
        public bool IsAvailable
        {
            get
            {
                ensureReflection();
                return _isAvailable;
            }
        }

        private bool _reflectionResolved;
        private bool _isAvailable;

#if UNITY_ANDROID && !UNITY_EDITOR
        private Type _platformType;
        private object _platformInstance;
        private object _savedGameClient;
        private Type _statusType;
        private Type _metadataType;
        private object _statusSuccess;
        private object _dsReadNetworkFirst;
        private object _strategyLongestPlaytime;
        private MethodInfo _openMethod;
        private MethodInfo _readMethod;
        private MethodInfo _commitMethod;
        private MethodInfo _deleteMethod;
        private Type _builderType;
#endif

        private void ensureReflection()
        {
            if (_reflectionResolved) return;
            _reflectionResolved = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                if (_platformType == null) { _isAvailable = false; return; }

                _platformInstance = getStaticProperty(_platformType, "Instance");
                if (_platformInstance == null) { _isAvailable = false; return; }

                _savedGameClient = getInstanceProperty(_platformInstance, "SavedGame");
                if (_savedGameClient == null) { _isAvailable = false; return; }

                var asmName = _platformType.Assembly.GetName().Name;
                _statusType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus, {asmName}");
                _metadataType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.ISavedGameMetadata, {asmName}");
                var dsType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.DataSource, {asmName}");
                var stType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.ConflictResolutionStrategy, {asmName}");
                _builderType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.SavedGameMetadataUpdate+Builder, {asmName}");

                if (_statusType == null || _metadataType == null || dsType == null || stType == null)
                { _isAvailable = false; return; }

                _statusSuccess = Enum.Parse(_statusType, "Success");
                _dsReadNetworkFirst = Enum.Parse(dsType, "ReadNetworkFirst");
                _strategyLongestPlaytime = Enum.Parse(stType, "UseLongestPlaytime");

                var clientType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.ISavedGameClient, {asmName}")
                              ?? _savedGameClient.GetType();
                _openMethod = clientType.GetMethod("OpenWithAutomaticConflictResolution");
                _readMethod = clientType.GetMethod("ReadBinaryData");
                _commitMethod = clientType.GetMethod("CommitUpdate");
                _deleteMethod = clientType.GetMethod("Delete");

                _isAvailable = _openMethod != null && _readMethod != null && _commitMethod != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GpgsLoginController] Reflection init failed: {e.Message}");
                _isAvailable = false;
            }
#else
            _isAvailable = false;
#endif
        }

        // ───── Sign-in ─────

        public Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureReflection();
            if (!_isAvailable) return Task.FromResult(CloudSaveResult.NotAvailable);

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

        public void SignOut()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_isAvailable) return;
            try
            {
                var m = _platformType?.GetMethod("SignOut", BindingFlags.Public | BindingFlags.Instance);
                m?.Invoke(_platformInstance, null);
            }
            catch { }
#endif
        }

        /// <summary>
        /// Acquires Google GPGS server auth code via Reflection (for backend sign-in / account linking).
        /// GPGS v2 only (Action&lt;SignInStatus&gt; + Action&lt;AuthResponse&gt;).
        /// </summary>
        public async Task<CoreResult<LoginCredential>> GetServerAuthCodeCredentialAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureReflection();

            try
            {
                if (_platformType == null || _platformInstance == null)
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NOT_FOUND,
                        "GooglePlayGames v2 plugin is not installed.");
                }

                // ── Authenticate (v2 API) ──
                // GPGS v2 has two distinct methods:
                //   Authenticate(Action<SignInStatus>)         → silent (automatic) sign-in only
                //   ManuallyAuthenticate(Action<SignInStatus>) → shows sign-in UI dialog
                // We try ManuallyAuthenticate first so the user sees the sign-in prompt.
                // If it doesn't exist, fall back to Authenticate (silent).
                var asmName = _platformType.Assembly.GetName().Name;
                var signInStatusType = Type.GetType($"GooglePlayGames.BasicApi.SignInStatus, {asmName}");

                if (signInStatusType == null)
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTHENTICATE,
                        "GPGS SignInStatus type not found.");
                }

                var tcsAuth = new TaskCompletionSource<bool>();

                var statusCb = createAction1(signInStatusType, (obj) =>
                {
                    var s = obj?.ToString();
                    tcsAuth.TrySetResult(string.Equals(s, "Success", StringComparison.OrdinalIgnoreCase));
                });

                // 1) Try ManuallyAuthenticate (interactive, shows UI)
                var authMethod = _platformType.GetMethod("ManuallyAuthenticate", new[] { statusCb.GetType() });

                // 2) Fallback: Authenticate (silent only)
                if (authMethod == null)
                    authMethod = _platformType.GetMethod("Authenticate", new[] { statusCb.GetType() });

                if (authMethod == null)
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTHENTICATE,
                        "PlayGamesPlatform.ManuallyAuthenticate/Authenticate not found.");
                }

                authMethod.Invoke(_platformInstance, new object[] { statusCb });

                bool authenticated = await tcsAuth.Task;
                ct.ThrowIfCancellationRequested();

                if (!authenticated)
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_AUTH_FAILED,
                        "Google Play Games authentication failed.");
                }

                // ── RequestServerSideAccess (v2: dynamic discovery) ──
                MethodInfo req = null;
                var methods = _platformType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m.Name != "RequestServerSideAccess") continue;

                    var mps = m.GetParameters();
                    if (mps.Length < 2) continue;

                    if (!typeof(Delegate).IsAssignableFrom(mps[mps.Length - 1].ParameterType)) continue;

                    req = m;
                    break;
                }

                if (req == null)
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_SERVER_ACCESS,
                        "PlayGamesPlatform.RequestServerSideAccess not found.");
                }

                var reqPs = req.GetParameters();
                var cbParamType = reqPs[reqPs.Length - 1].ParameterType;

                var authResponseType =
                    cbParamType.IsGenericType && cbParamType.GetGenericArguments().Length == 1
                        ? cbParamType.GetGenericArguments()[0]
                        : cbParamType.GetMethod("Invoke").GetParameters()[0].ParameterType;

                var getAuthCode = authResponseType.GetMethod("GetAuthCode", BindingFlags.Public | BindingFlags.Instance);

                var tcsCode = new TaskCompletionSource<string>();

                var authResponseCb = createAction1(authResponseType, (obj) =>
                {
                    if (obj == null)
                    {
                        tcsCode.TrySetResult(string.Empty);
                        return;
                    }

                    var code = getAuthCode != null ? (string)getAuthCode.Invoke(obj, null) : null;
                    tcsCode.TrySetResult(code ?? string.Empty);
                });

                var args = new object[reqPs.Length];
                args[0] = false;

                for (int i = 1; i < reqPs.Length - 1; i++)
                {
                    var t = reqPs[i].ParameterType;
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        args[i] = Activator.CreateInstance(t);
                    }
                    else
                    {
                        args[i] = null;
                    }
                }

                args[reqPs.Length - 1] = authResponseCb;

                req.Invoke(_platformInstance, args);

                string serverAuthCode = await tcsCode.Task;
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(serverAuthCode))
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTH_CODE,
                        "Failed to obtain server auth code from GPGS.");
                }

                return CoreResult<LoginCredential>.Success(new LoginCredential(null, null, null, serverAuthCode));
            }
            catch (Exception ex)
            {
                return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_EXCEPTION, ex.ToString());
            }
#else
            return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NOT_FOUND,
                "GPGS is not available on this platform.");
#endif
        }

        // ───── Cloud Save (Saved Games) ─────

        public async Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureReflection();
            if (!_isAvailable)
                return (CloudSaveResult.NotAvailable, null);

            try
            {
                var (openSt, meta) = await openSavedGame(slot);
                if (!isSuccess(openSt)) return (mapStatus(openSt), null);

                var (readSt, data) = await readBinaryData(meta);
                if (!isSuccess(readSt)) return (mapStatus(readSt), null);

                var bytes = data as byte[];
                if (bytes == null || bytes.Length == 0) return (CloudSaveResult.NotFound, null);

                string json = Encoding.UTF8.GetString(bytes);
                var payload = JsonUtility.FromJson<CloudSavePayload>(json);
                return (CloudSaveResult.Success, payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GpgsLoginController] LoadAsync failed: {e.Message}");
                return (CloudSaveResult.FatalFailure, null);
            }
#else
            return (CloudSaveResult.NotAvailable, null);
#endif
        }

        public async Task<CloudSaveResult> SaveAsync(
            string slot, CloudSavePayload payload, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureReflection();
            if (!_isAvailable) return CloudSaveResult.NotAvailable;

            try
            {
                var (openSt, meta) = await openSavedGame(slot);
                if (!isSuccess(openSt)) return mapStatus(openSt);

                string json = JsonUtility.ToJson(payload);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                object metaUpdate = buildMetadataUpdate("cloudsave");

                var (commitSt, _) = await commitUpdate(meta, metaUpdate, bytes);
                return isSuccess(commitSt) ? CloudSaveResult.Success : mapStatus(commitSt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GpgsLoginController] SaveAsync failed: {e.Message}");
                return CloudSaveResult.FatalFailure;
            }
#else
            return CloudSaveResult.NotAvailable;
#endif
        }

        public async Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureReflection();
            if (!_isAvailable) return CloudSaveResult.NotAvailable;

            try
            {
                var (openSt, meta) = await openSavedGame(slot);
                if (!isSuccess(openSt)) return mapStatus(openSt);

                var tcs = new TaskCompletionSource<object>();
                Action<object> cb = (st) => tcs.TrySetResult(st);
                var del = createAction1(_statusType, cb);

                _deleteMethod.Invoke(_savedGameClient, new object[] { meta, del });

                var stObj = await tcs.Task;
                return isSuccess(stObj) ? CloudSaveResult.Success : mapStatus(stObj);
            }
            catch
            {
                return CloudSaveResult.FatalFailure;
            }
#else
            return CloudSaveResult.NotAvailable;
#endif
        }

        // ───── Reflection wrappers ─────

#if UNITY_ANDROID && !UNITY_EDITOR

        private Task<(object status, object meta)> openSavedGame(string slot)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            Action<object, object> cb = (st, meta) => tcs.TrySetResult((st, meta));
            var del = createAction2(_statusType, _metadataType, cb);
            _openMethod.Invoke(_savedGameClient, new object[] { slot, _dsReadNetworkFirst, _strategyLongestPlaytime, del });
            return tcs.Task;
        }

        private Task<(object status, object data)> readBinaryData(object meta)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            Action<object, object> cb = (st, data) => tcs.TrySetResult((st, data));
            var del = createAction2(_statusType, typeof(byte[]), cb);
            _readMethod.Invoke(_savedGameClient, new object[] { meta, del });
            return tcs.Task;
        }

        private Task<(object status, object result)> commitUpdate(
            object metadata, object metaUpdate, byte[] data)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            Action<object, object> cb = (st, meta) => tcs.TrySetResult((st, meta));
            var del = createAction2(_statusType, _metadataType, cb);
            _commitMethod.Invoke(_savedGameClient, new object[] { metadata, metaUpdate, data, del });
            return tcs.Task;
        }

        private object buildMetadataUpdate(string description)
        {
            if (_builderType == null) return null;
            var builder = Activator.CreateInstance(_builderType);
            var withDesc = _builderType.GetMethod("WithUpdatedDescription");
            if (withDesc != null)
                builder = withDesc.Invoke(builder, new object[] { description });
            var buildMethod = _builderType.GetMethod("Build");
            return buildMethod?.Invoke(builder, null);
        }

        private bool isSuccess(object status)
        {
            if (status == null) return false;
            return status.Equals(_statusSuccess);
        }

        private CloudSaveResult mapStatus(object status)
        {
            if (status == null) return CloudSaveResult.FatalFailure;

            var s = status.ToString();
            if (string.Equals(s, "Success", StringComparison.OrdinalIgnoreCase)) return CloudSaveResult.Success;
            if (string.Equals(s, "AuthenticationError", StringComparison.OrdinalIgnoreCase)) return CloudSaveResult.AuthRequired;
            if (string.Equals(s, "TimeoutError", StringComparison.OrdinalIgnoreCase)) return CloudSaveResult.TemporaryFailure;
            if (string.Equals(s, "InternalError", StringComparison.OrdinalIgnoreCase)) return CloudSaveResult.FatalFailure;

            return CloudSaveResult.TemporaryFailure;
        }

#endif

        private static object getStaticProperty(Type type, string name)
        {
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static object getInstanceProperty(object instance, string name)
        {
            return instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        }

        /// <summary>
        /// IL2CPP-safe: builds Action&lt;T&gt; that boxes the argument and forwards to Action&lt;object&gt;.
        /// Delegate.CreateDelegate(Action&lt;T&gt;, Action&lt;object&gt;.Target, Action&lt;object&gt;.Method)
        /// crashes on IL2CPP when T is a value type (enum, struct) because the calling convention
        /// differs between object and value types in the native layer (SIGSEGV fault addr 0x2).
        /// Expression.Lambda compiles a proper boxing wrapper that IL2CPP can handle.
        /// </summary>
        private static Delegate createAction1(Type argType, Action<object> onCall)
        {
            var actionType = typeof(Action<>).MakeGenericType(argType);
            var param = Expression.Parameter(argType, "arg");
            var boxed = argType.IsValueType
                ? (Expression)Expression.Convert(param, typeof(object))
                : param;
            var call = Expression.Invoke(Expression.Constant(onCall), boxed);
            return Expression.Lambda(actionType, call, param).Compile();
        }

        /// <summary>
        /// IL2CPP-safe two-argument variant: Action&lt;T1,T2&gt; → Action&lt;object,object&gt;.
        /// </summary>
        private static Delegate createAction2(Type arg1Type, Type arg2Type, Action<object, object> onCall)
        {
            var actionType = typeof(Action<,>).MakeGenericType(arg1Type, arg2Type);
            var p1 = Expression.Parameter(arg1Type, "a1");
            var p2 = Expression.Parameter(arg2Type, "a2");
            var b1 = arg1Type.IsValueType
                ? (Expression)Expression.Convert(p1, typeof(object))
                : p1;
            var b2 = arg2Type.IsValueType
                ? (Expression)Expression.Convert(p2, typeof(object))
                : p2;
            var call = Expression.Invoke(Expression.Constant(onCall), b1, b2);
            return Expression.Lambda(actionType, call, p1, p2).Compile();
        }
    }
}
