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
    public sealed class AccountLoginGpgs
    {
        public bool IsAvailable
        {
            get
            {
                ensureSavedGameClient();
                return _isAvailable;
            }
        }

        private bool _reflectionResolved;
        private bool _savedGameResolved;
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

        /// <summary>
        /// Java SnapshotsClient — GPGS snapshot lifecycle 관리에 사용.
        /// AndroidSavedGameClient.mSnapshotsClient 필드를 reflection으로 추출.
        /// discardAndClose 호출에 필요하다 (LoadAsync 후 snapshot을 닫기 위해).
        /// </summary>
        private object _javaSnapshotsClient;

        /// <summary>
        /// Unity main thread SynchronizationContext.
        /// GPGS JNI 호출은 반드시 main thread에서 해야 하므로,
        /// ConfigureAwait(false)로 thread pool에 넘어간 후에도
        /// Invoke 시점에 main thread로 marshal하기 위해 캡처한다.
        /// </summary>
        private SynchronizationContext _unitySyncCtx;
#endif

        /// <summary>
        /// Phase 1: PlayGamesPlatform type + Instance (Activate if needed).
        /// 인증 전에도 호출 가능 — _platformType/_platformInstance 만 확보한다.
        /// </summary>
        private void ensureReflection()
        {
            if (_reflectionResolved) return;
            _reflectionResolved = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _unitySyncCtx = SynchronizationContext.Current;
                _platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                if (_platformType == null) return;

                _platformInstance = getStaticProperty(_platformType, "Instance");
                if (_platformInstance == null)
                {
                    try
                    {
                        var activate = _platformType.GetMethod("Activate", BindingFlags.Public | BindingFlags.Static);
                        activate?.Invoke(null, null);
                        _platformInstance = getStaticProperty(_platformType, "Instance");
                    }
                    catch { }
                }
            }
            catch { }
#endif
        }

        /// <summary>
        /// Phase 2: SavedGame client + Saved Games 타입 리플렉션.
        /// 인증 완료 후에 호출해야 SavedGame 프로퍼티가 null이 아니다.
        /// 여러 번 호출 가능 — 성공할 때까지 재시도한다.
        /// </summary>
        private void ensureSavedGameClient()
        {
            if (_savedGameResolved) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            ensureReflection();
            if (_platformType == null || _platformInstance == null)
            {
                _isAvailable = false;
                _savedGameResolved = true;
                return;
            }

            try
            {
                _savedGameClient = getInstanceProperty(_platformInstance, "SavedGame");
                if (_savedGameClient == null)
                {
                    // 인증 완료 전이면 null — 아직 resolved로 마킹하지 않고 다음 호출에서 재시도
                    _isAvailable = false;
                    return;
                }

                var asmName = _platformType.Assembly.GetName().Name;
                _statusType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus, {asmName}");
                _metadataType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.ISavedGameMetadata, {asmName}");
                // DataSource: GPGS v1 = GooglePlayGames.BasicApi.SavedGame.DataSource, v2 = GooglePlayGames.BasicApi.DataSource
                var dsType = Type.GetType($"GooglePlayGames.BasicApi.DataSource, {asmName}")
                          ?? Type.GetType($"GooglePlayGames.BasicApi.SavedGame.DataSource, {asmName}");
                var stType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.ConflictResolutionStrategy, {asmName}");
                _builderType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.SavedGameMetadataUpdate+Builder, {asmName}");

                // _statusType, _metadataType, stType are required. dsType is optional (GPGS v2 removed DataSource enum).
                if (_statusType == null || _metadataType == null || stType == null)
                {
                    Debug.LogWarning($"[AccountLoginGpgs] ensureSavedGameClient: required types not found. asm={asmName} " +
                        $"status={_statusType != null} metadata={_metadataType != null} st={stType != null}");
                    _isAvailable = false;
                    _savedGameResolved = true;
                    return;
                }

                _statusSuccess = Enum.Parse(_statusType, "Success");
                _dsReadNetworkFirst = resolveEnumValue(dsType, "ReadNetworkFirst", "ReadCacheOrNetwork");
                _strategyLongestPlaytime = Enum.Parse(stType, "UseLongestPlaytime");

                var clientType = Type.GetType($"GooglePlayGames.BasicApi.SavedGame.ISavedGameClient, {asmName}")
                              ?? _savedGameClient.GetType();

                // OpenWithAutomaticConflictResolution — find by name (signature varies by GPGS version)
                var openMethods = clientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < openMethods.Length; i++)
                {
                    if (openMethods[i].Name == "OpenWithAutomaticConflictResolution")
                    {
                        _openMethod = openMethods[i];
                        break;
                    }
                }
                _readMethod = clientType.GetMethod("ReadBinaryData");
                _commitMethod = clientType.GetMethod("CommitUpdate");
                _deleteMethod = clientType.GetMethod("Delete");

                _isAvailable = _openMethod != null && _readMethod != null && _commitMethod != null;

                // Java SnapshotsClient 추출 — discardAndClose 호출에 필요
                try
                {
                    var scField = _savedGameClient.GetType()
                        .GetField("mSnapshotsClient", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (scField != null)
                        _javaSnapshotsClient = scField.GetValue(_savedGameClient);
                }
                catch { }

                _savedGameResolved = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountLoginGpgs] ensureSavedGameClient failed: {e.Message}");
                _isAvailable = false;
                _savedGameResolved = true;
            }
#else
            _isAvailable = false;
            _savedGameResolved = true;
#endif
        }

        // ───── Sign-in ─────

        public async Task<SaveCloudResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // SavedGame 프로퍼티는 인증 완료 직후 바로 사용 가능하지 않을 수 있다.
            // 최대 10회 × 500ms = 5초 대기하며 재시도한다.
            const int maxRetries = 10;
            const int retryDelayMs = 500;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ensureSavedGameClient();
                if (_isAvailable) break;
                if (_savedGameResolved) return SaveCloudResult.NotAvailable; // 영구 실패 (타입 없음 등)
                if (attempt < maxRetries - 1)
                    await Task.Delay(retryDelayMs, ct);
            }

            if (!_isAvailable) return SaveCloudResult.NotAvailable;

            if (Social.localUser != null && Social.localUser.authenticated)
                return SaveCloudResult.Success;

            var tcs = new TaskCompletionSource<SaveCloudResult>();

            if (ct.CanBeCanceled)
                ct.Register(() => tcs.TrySetResult(SaveCloudResult.TemporaryFailure));

            try
            {
                Social.localUser.Authenticate(success =>
                {
                    tcs.TrySetResult(success ? SaveCloudResult.Success : SaveCloudResult.AuthRequired);
                });
            }
            catch
            {
                tcs.TrySetResult(SaveCloudResult.FatalFailure);
            }

            return await tcs.Task;
#else
            return SaveCloudResult.NotAvailable;
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
        public async Task<CommonResult<LoginCredential>> GetServerAuthCodeCredentialAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureReflection();

            try
            {
                if (_platformType == null || _platformInstance == null)
                {
                    return CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NOT_FOUND,
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
                    return CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTHENTICATE,
                        "GPGS SignInStatus type not found.");
                }

                // ── 인증 순서 ──
                // 1) Authenticate (silent: isAuthenticated 체크) — UI 없이 즉시 확인.
                //    기기에 이미 Play Games 로그인이 되어 있으면 바로 성공.
                // 2) ManuallyAuthenticate (interactive: signIn UI 표시) — silent 실패 시만.
                //    signIn() 후 isAuthenticated()=false 타이밍 이슈가 있을 수 있으므로
                //    가능하면 silent를 먼저 시도한다.
                // Authenticate 오버로드가 3개 있어 GetMethod(name, types)가
                // AmbiguousMatchException 또는 null을 반환할 수 있다.
                // GetMethods로 수동 검색하여 Action<SignInStatus> 1개만 받는 오버로드를 찾는다.
                var cbType = typeof(Action<>).MakeGenericType(signInStatusType);

                MethodInfo silentAuth = null;
                MethodInfo manualAuth = null;

                var allMethods = _platformType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < allMethods.Length; i++)
                {
                    var m = allMethods[i];
                    if (m.Name != "Authenticate" && m.Name != "ManuallyAuthenticate") continue;

                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    if (ps[0].ParameterType != cbType) continue;

                    if (m.Name == "Authenticate") silentAuth = m;
                    else if (m.Name == "ManuallyAuthenticate") manualAuth = m;
                }

                if (silentAuth == null && manualAuth == null)
                {
                    return CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTHENTICATE,
                        "PlayGamesPlatform.ManuallyAuthenticate/Authenticate not found.");
                }

                bool authenticated = false;

                // 1) Silent
                if (silentAuth != null)
                {
                    var tcs1 = new TaskCompletionSource<bool>();
                    var cb1 = createAction1(signInStatusType, (obj) =>
                    {
                        var status = obj?.ToString() ?? "null";
                        tcs1.TrySetResult(string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase));
                    });
                    silentAuth.Invoke(_platformInstance, new object[] { cb1 });
                    authenticated = await tcs1.Task;
                    ct.ThrowIfCancellationRequested();
                }

                // 2) Manual (UI) — silent 실패 시
                if (!authenticated && manualAuth != null)
                {
                    var tcs2 = new TaskCompletionSource<bool>();
                    var cb2 = createAction1(signInStatusType, (obj) =>
                    {
                        var status = obj?.ToString() ?? "null";
                        tcs2.TrySetResult(string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase));
                    });
                    manualAuth.Invoke(_platformInstance, new object[] { cb2 });
                    authenticated = await tcs2.Task;
                    ct.ThrowIfCancellationRequested();
                }

                if (!authenticated)
                {
                    return CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_AUTH_FAILED,
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

                // ── RequestServerSideAccess (optional — 실패해도 cloud save에는 영향 없음) ──
                // GPGS 인증 자체는 위에서 성공했으므로, server auth code 획득은 best-effort.
                // WebClientId 미설정, ApiException: 10 등의 오류 시 빈 credential로 계속 진행한다.
                string serverAuthCode = null;
                try
                {
                    serverAuthCode = await requestServerAuthCodeAsync(req);
                }
                catch (Exception reqEx)
                {
                    Debug.LogWarning($"[AccountLoginGpgs] RequestServerSideAccess threw: {reqEx.Message}");
                }

                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(serverAuthCode))
                {
                    Debug.LogWarning("[AccountLoginGpgs] RequestServerSideAccess failed, proceeding without server auth code.");
                    return CommonResult<LoginCredential>.Success(LoginCredential.Empty());
                }

                return CommonResult<LoginCredential>.Success(new LoginCredential(null, null, null, serverAuthCode));
            }
            catch (Exception ex)
            {
                return CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_EXCEPTION, ex.ToString());
            }
#else
            return CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NOT_FOUND,
                "GPGS is not available on this platform.");
#endif
        }

        // ───── Cloud Save (Saved Games) ─────

        public async Task<(SaveCloudResult result, SaveCloudPayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureSavedGameClient();
            if (!_isAvailable)
                return (SaveCloudResult.NotAvailable, null);

            try
            {
                Debug.Log($"[AccountLoginGpgs] LoadAsync: opening slot={slot}");
                var (openSt, meta) = await openSavedGame(slot).ConfigureAwait(false);
                Debug.Log($"[AccountLoginGpgs] LoadAsync: openSt={openSt}");
                if (!isSuccess(openSt)) return (mapStatus(openSt), null);

                Debug.Log("[AccountLoginGpgs] LoadAsync: reading binary data");
                var (readSt, data) = await readBinaryData(meta).ConfigureAwait(false);
                Debug.Log($"[AccountLoginGpgs] LoadAsync: readSt={readSt} dataLen={(data as byte[])?.Length ?? -1}");

                // Read 완료 후 snapshot을 즉시 닫는다.
                // GPGS는 동일 slot에 대해 동시에 하나의 open만 허용하므로,
                // 닫지 않으면 이후 Open 호출(SaveAsync 등)이 영원히 대기한다.
                await discardSnapshot(meta).ConfigureAwait(false);

                if (!isSuccess(readSt)) return (mapStatus(readSt), null);

                var bytes = data as byte[];
                if (bytes == null || bytes.Length == 0) return (SaveCloudResult.NotFound, null);

                string json = Encoding.UTF8.GetString(bytes);
                var payload = JsonUtility.FromJson<SaveCloudPayload>(json);
                return (SaveCloudResult.Success, payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountLoginGpgs] LoadAsync failed: {e.Message}");
                return (SaveCloudResult.FatalFailure, null);
            }
#else
            return (SaveCloudResult.NotAvailable, null);
#endif
        }

        public async Task<SaveCloudResult> SaveAsync(
            string slot, SaveCloudPayload payload, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureSavedGameClient();
            if (!_isAvailable) return SaveCloudResult.NotAvailable;

            try
            {
                var (openSt, meta) = await openSavedGame(slot).ConfigureAwait(false);
                if (!isSuccess(openSt)) return mapStatus(openSt);

                string json = JsonUtility.ToJson(payload);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                object metaUpdate = buildMetadataUpdate("cloudsave");

                var (commitSt, _) = await commitUpdate(meta, metaUpdate, bytes).ConfigureAwait(false);
                return isSuccess(commitSt) ? SaveCloudResult.Success : mapStatus(commitSt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountLoginGpgs] SaveAsync failed: {e.Message}");
                return SaveCloudResult.FatalFailure;
            }
#else
            return SaveCloudResult.NotAvailable;
#endif
        }

        public async Task<SaveCloudResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ensureSavedGameClient();
            if (!_isAvailable) return SaveCloudResult.NotAvailable;

            try
            {
                var (openSt, meta) = await openSavedGame(slot).ConfigureAwait(false);
                if (!isSuccess(openSt)) return mapStatus(openSt);

                // GPGS v2 Delete(ISavedGameMetadata) — 동기, 콜백 없음, void 반환.
                var tcs = new TaskCompletionSource<bool>();
                invokeOnMainThread(() =>
                {
                    try
                    {
                        _deleteMethod.Invoke(_savedGameClient, new object[] { meta });
                        tcs.TrySetResult(true);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AccountLoginGpgs] Delete invoke failed: {e.Message}");
                        tcs.TrySetResult(false);
                    }
                });

                var success = await tcs.Task.ConfigureAwait(false);
                return success ? SaveCloudResult.Success : SaveCloudResult.FatalFailure;
            }
            catch
            {
                return SaveCloudResult.FatalFailure;
            }
#else
            return SaveCloudResult.NotAvailable;
#endif
        }

        // ───── Reflection wrappers ─────

#if UNITY_ANDROID && !UNITY_EDITOR

        /// <summary>
        /// RequestServerSideAccess를 호출하여 server auth code를 획득한다.
        /// GPGS v2 콜백은 main thread에서 실행되므로 ConfigureAwait(false)를 사용하지 않는다.
        /// req가 null이면 즉시 null 반환.
        /// </summary>
        private async Task<string> requestServerAuthCodeAsync(MethodInfo req)
        {
            if (req == null) return null;

            var reqPs = req.GetParameters();
            var cbParamType = reqPs[reqPs.Length - 1].ParameterType;

            var authResponseType =
                cbParamType.IsGenericType && cbParamType.GetGenericArguments().Length == 1
                    ? cbParamType.GetGenericArguments()[0]
                    : cbParamType.GetMethod("Invoke").GetParameters()[0].ParameterType;

            var getAuthCode = authResponseType == typeof(string)
                ? null
                : authResponseType.GetMethod("GetAuthCode", BindingFlags.Public | BindingFlags.Instance);

            var tcsCode = new TaskCompletionSource<string>();

            var authResponseCb = createAction1(authResponseType, (obj) =>
            {
                if (obj == null)
                {
                    tcsCode.TrySetResult(string.Empty);
                    return;
                }

                if (obj is string directCode)
                {
                    tcsCode.TrySetResult(directCode);
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
                    args[i] = Activator.CreateInstance(t);
                else
                    args[i] = null;
            }

            args[reqPs.Length - 1] = authResponseCb;

            req.Invoke(_platformInstance, args);

            return await tcsCode.Task;
        }

        /// <summary>
        /// GPGS 비동기 호출의 최대 대기 시간 (초).
        /// 콜백이 오지 않으면 timeout 후 실패 처리하여 Unity main thread 블로킹을 방지한다.
        /// </summary>
        private const int GpgsTimeoutSec = 15;

        private async Task<(object status, object meta)> openSavedGame(string slot)
        {
            var tcs = new TaskCompletionSource<(object, object)>();

            var ps = _openMethod.GetParameters();

            // Build callback from the actual last parameter type
            var cbParamType = ps[ps.Length - 1].ParameterType;
            var cbGenericArgs = cbParamType.IsGenericType ? cbParamType.GetGenericArguments() : null;

            Delegate del;
            Action<object, object> cb = (st, meta) => tcs.TrySetResult((st, meta));
            if (cbGenericArgs != null && cbGenericArgs.Length == 2)
                del = createAction2(cbGenericArgs[0], cbGenericArgs[1], cb);
            else
                del = createAction2(_statusType, _metadataType, cb);

            // Build args array — match each parameter by type
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var pt = ps[i].ParameterType;

                if (pt == typeof(string))
                    args[i] = slot;
                else if (typeof(Delegate).IsAssignableFrom(pt))
                    args[i] = del;
                else if (_dsReadNetworkFirst != null && pt == _dsReadNetworkFirst.GetType())
                    args[i] = _dsReadNetworkFirst;
                else if (pt == _strategyLongestPlaytime.GetType())
                    args[i] = _strategyLongestPlaytime;
                else if (pt.IsEnum)
                    args[i] = Enum.ToObject(pt, 0);
                else if (pt == typeof(TimeSpan))
                    args[i] = TimeSpan.FromSeconds(10);
                else if (pt == typeof(int))
                    args[i] = 0;
                else if (pt == typeof(bool))
                    args[i] = false;
                else
                    args[i] = null;
            }

            // GPGS 호출은 반드시 main thread에서 해야 한다 (Android JNI 요구).
            // ConfigureAwait(false) 이후 thread pool에서 호출될 수 있으므로 main thread로 marshal.
            invokeOnMainThread(() =>
            {
                try
                {
                    _openMethod.Invoke(_savedGameClient, args);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AccountLoginGpgs] openSavedGame Invoke failed: {e}");
                    tcs.TrySetResult((null, null));
                }
            });

            return await withTimeout(tcs, "openSavedGame").ConfigureAwait(false);
        }

        private async Task<(object status, object data)> readBinaryData(object meta)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            Action<object, object> cb = (st, data) => tcs.TrySetResult((st, data));
            var rps = _readMethod.GetParameters();
            var rcb = rps[rps.Length - 1].ParameterType;
            var rga = rcb.IsGenericType ? rcb.GetGenericArguments() : null;
            var del = (rga != null && rga.Length == 2)
                ? createAction2(rga[0], rga[1], cb)
                : createAction2(_statusType, typeof(byte[]), cb);
            invokeOnMainThread(() => _readMethod.Invoke(_savedGameClient, new object[] { meta, del }));
            return await withTimeout(tcs, "readBinaryData").ConfigureAwait(false);
        }

        private async Task<(object status, object result)> commitUpdate(
            object metadata, object metaUpdate, byte[] data)
        {
            var tcs = new TaskCompletionSource<(object, object)>();
            Action<object, object> cb = (st, meta) => tcs.TrySetResult((st, meta));
            var cps = _commitMethod.GetParameters();
            var ccb = cps[cps.Length - 1].ParameterType;
            var cga = ccb.IsGenericType ? ccb.GetGenericArguments() : null;
            var del = (cga != null && cga.Length == 2)
                ? createAction2(cga[0], cga[1], cb)
                : createAction2(_statusType, _metadataType, cb);
            invokeOnMainThread(() => _commitMethod.Invoke(_savedGameClient, new object[] { metadata, metaUpdate, data, del }));
            return await withTimeout(tcs, "commitUpdate").ConfigureAwait(false);
        }

        /// <summary>
        /// GPGS 콜백 기반 호출에 timeout을 적용한다.
        /// 콜백이 GpgsTimeoutSec 내에 오지 않으면 (null, null) 반환하여 Unity 블로킹을 방지.
        /// ConfigureAwait(false) — continuation이 thread pool에서 실행되므로
        /// GPGS가 RunOnGameThread로 main thread에 콜백을 post할 때 deadlock이 발생하지 않는다.
        /// </summary>
        private async Task<(object, object)> withTimeout(
            TaskCompletionSource<(object, object)> tcs, string label)
        {
            var delay = Task.Delay(TimeSpan.FromSeconds(GpgsTimeoutSec));
            var completed = await Task.WhenAny(tcs.Task, delay).ConfigureAwait(false);
            if (completed == delay)
            {
                Debug.LogWarning($"[AccountLoginGpgs] {label}: GPGS callback timed out after {GpgsTimeoutSec}s");
                tcs.TrySetResult((null, null));
            }
            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Action을 Unity main thread에서 실행한다.
        /// ConfigureAwait(false) 이후 thread pool에 있을 때
        /// GPGS JNI 호출을 main thread로 marshal하기 위해 사용.
        /// </summary>
        private void invokeOnMainThread(Action action)
        {
            if (_unitySyncCtx != null && SynchronizationContext.Current != _unitySyncCtx)
            {
                _unitySyncCtx.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Open 후 ReadBinaryData만 수행하고 Save/Commit하지 않는 경우,
        /// snapshot을 명시적으로 닫아야 다음 Open 호출이 성공한다.
        /// GPGS Java SDK의 SnapshotsClient.discardAndClose(Snapshot)을 JNI로 호출한다.
        /// meta 객체에서 AndroidSnapshotMetadata.mJavaSnapshot 필드를 reflection으로 추출하여 사용.
        /// 비동기 — main thread에서 JNI 호출이 완료될 때까지 대기한다.
        /// 실패해도 silent — best-effort.
        /// </summary>
        private async Task discardSnapshot(object meta)
        {
            if (meta == null) return;

            try
            {
                var metaType = meta.GetType();

                // AndroidSnapshotMetadata.mJavaSnapshot — live Snapshot Java 객체
                var snapshotField = metaType.GetField("mJavaSnapshot", BindingFlags.NonPublic | BindingFlags.Instance);
                var javaSnapshot = snapshotField?.GetValue(meta) as UnityEngine.AndroidJavaObject;

                // AndroidSnapshotMetadata.mJavaContents — SnapshotContents Java 객체
                var contentsField = metaType.GetField("mJavaContents", BindingFlags.NonPublic | BindingFlags.Instance);
                var javaContents = contentsField?.GetValue(meta) as UnityEngine.AndroidJavaObject;

                var sc = _javaSnapshotsClient as UnityEngine.AndroidJavaObject;

                var tcs = new TaskCompletionSource<bool>();

                invokeOnMainThread(() =>
                {
                    try
                    {
                        bool closed = false;

                        // 1차: SnapshotsClient.discardAndClose(Snapshot) 시도
                        if (sc != null && javaSnapshot != null)
                        {
                            try
                            {
                                using (sc.Call<UnityEngine.AndroidJavaObject>("discardAndClose", javaSnapshot)) { }
                                Debug.Log("[AccountLoginGpgs] discardAndClose: snapshot released.");
                                closed = true;
                            }
                            catch (Exception e1)
                            {
                                Debug.LogWarning($"[AccountLoginGpgs] discardAndClose failed: {e1.Message}");
                            }
                        }

                        // 2차 fallback: SnapshotContents.close() 시도
                        if (!closed && javaContents != null)
                        {
                            try
                            {
                                javaContents.Call("close");
                                Debug.Log("[AccountLoginGpgs] contents.close(): snapshot contents closed.");
                            }
                            catch (Exception e2)
                            {
                                Debug.LogWarning($"[AccountLoginGpgs] contents.close() failed: {e2.Message}");
                            }
                        }
                    }
                    finally
                    {
                        tcs.TrySetResult(true);
                    }
                });

                await tcs.Task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountLoginGpgs] discardSnapshot failed: {e.Message}");
            }
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

        private SaveCloudResult mapStatus(object status)
        {
            if (status == null) return SaveCloudResult.FatalFailure;

            var s = status.ToString();
            if (string.Equals(s, "Success", StringComparison.OrdinalIgnoreCase)) return SaveCloudResult.Success;
            if (string.Equals(s, "AuthenticationError", StringComparison.OrdinalIgnoreCase)) return SaveCloudResult.AuthRequired;
            if (string.Equals(s, "TimeoutError", StringComparison.OrdinalIgnoreCase)) return SaveCloudResult.TemporaryFailure;
            if (string.Equals(s, "InternalError", StringComparison.OrdinalIgnoreCase)) return SaveCloudResult.FatalFailure;

            return SaveCloudResult.TemporaryFailure;
        }

        /// <summary>
        /// Enum 값을 이름 후보 목록에서 순서대로 찾는다. 모두 실패하면 default(0) 사용.
        /// dsType이 null이면 null 반환 (DataSource는 optional).
        /// </summary>
        private static object resolveEnumValue(Type enumType, params string[] candidates)
        {
            if (enumType == null) return null;
            foreach (var name in candidates)
            {
                try { return Enum.Parse(enumType, name); }
                catch { }
            }
            // All candidates failed — use default (first) value
            return Enum.ToObject(enumType, 0);
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
