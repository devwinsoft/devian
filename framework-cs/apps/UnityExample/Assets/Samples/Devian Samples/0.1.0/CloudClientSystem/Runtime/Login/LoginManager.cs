using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Devian.Domain.Common;

namespace Devian
{
    public enum LoginType
    {
        EditorLogin = 0,
        GuestLogin = 1,
        GoogleLogin = 2,
        AppleLogin = 3,
    }

    /// <summary>
    /// Login flow orchestrator.
    /// Order: Firebase init -> sign-in (type-based) -> CloudSaveManager.InitializeAsync
    /// - Editor/Guest: Firebase Anonymous
    /// - Google (Android): GPGS credential (internally acquired via Reflection) -> PlayGamesAuthProvider sign-in / link
    /// - Apple (iOS): credential sign-in / link
    /// Sync is handled by SyncDataManager (separate responsibility).
    /// </summary>
    public sealed class LoginManager : CompoSingleton<LoginManager>
    {
        private bool _initialized;
        private FirebaseAuth _auth;
        private LoginType _currentLoginType;

        /// <summary>
        /// Convenience overload — internally acquires credential for the given LoginType.
        /// Google(Android) uses GPGS Reflection; Apple(iOS) is not supported (use the credential overload).
        /// </summary>
        public async Task<CoreResult<bool>> LoginAsync(LoginType loginType, CancellationToken ct)
        {
            var credResult = await getLoginCredentialAsync(loginType, ct);
            if (credResult.IsFailure)
            {
                return CoreResult<bool>.Failure(credResult.Error!);
            }

            return await LoginAsync(loginType, credResult.Value, ct);
        }

        public async Task<CoreResult<bool>> LoginAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            // 1. Firebase init
            if (!_initialized)
            {
                var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
                ct.ThrowIfCancellationRequested();
                if (dep != DependencyStatus.Available)
                {
                    return CoreResult<bool>.Failure(CommonErrorType.LOGIN_FIREBASE_DEPENDENCY, $"Firebase dependency error: {dep}");
                }

                _auth = FirebaseAuth.DefaultInstance;
                _initialized = true;
            }

            // 2. Sign-in
            var signInResult = await signInAsync(loginType, credential ?? LoginCredential.Empty(), ct);
            if (signInResult.IsFailure)
            {
                return signInResult;
            }

            _currentLoginType = loginType;

            // 3. CloudSaveManager init (NOT for Guest)
            if (loginType != LoginType.GuestLogin)
            {
                var initResult = await ClaudSaveInstaller.InitializeAsync(ct);
                if (initResult.IsFailure)
                {
                    return CoreResult<bool>.Failure(initResult.Error!);
                }
            }

            return CoreResult<bool>.Success(true);
        }

        public void Logout()
        {
            if (_auth == null)
            {
                return;
            }

            _auth.SignOut();
        }

        public LoginType _getCurrentLoginType()
        {
            return _currentLoginType;
        }

        private async Task<CoreResult<LoginCredential>> getLoginCredentialAsync(LoginType loginType, CancellationToken ct)
        {
            switch (loginType)
            {
                case LoginType.EditorLogin:
                case LoginType.GuestLogin:
                    return CoreResult<LoginCredential>.Success(LoginCredential.Empty());

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GoogleLogin:
                    return await getGoogleGpgsCredentialAsync(ct);
#endif

                default:
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_CREDENTIAL_UNSUPPORTED,
                        $"Internal credential acquisition is not supported for {loginType}. Use LoginAsync(LoginType, LoginCredential, CancellationToken) instead.");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Acquires Google GPGS server auth code via Reflection (compile-safe without GPGS plugin).
        /// Supports both GPGS v1 (Action&lt;string&gt;) and v2 (Action&lt;AuthResponse&gt;) overloads.
        /// Flow: PlayGamesPlatform.Authenticate -> RequestServerSideAccess -> ServerAuthCode
        /// </summary>
        private async Task<CoreResult<LoginCredential>> getGoogleGpgsCredentialAsync(CancellationToken ct)
        {
            try
            {
                var platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                if (platformType == null)
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NOT_FOUND,
                        "GooglePlayGames plugin is not installed.");
                }

                var instanceProp = platformType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NOT_INITIALIZED,
                        "PlayGamesPlatform.Instance is null. Call PlayGamesPlatform.Activate() first.");
                }

                // Authenticate (supports GPGS v1 & v2)
                var authenticateV1 = platformType.GetMethod("Authenticate", new[] { typeof(Action<bool>) });
                bool authenticated;

                if (authenticateV1 != null)
                {
                    // v1: Authenticate(Action<bool>)
                    var tcsAuth = new TaskCompletionSource<bool>();
                    Action<bool> authCallback = success => tcsAuth.TrySetResult(success);
                    authenticateV1.Invoke(instance, new object[] { authCallback });

                    authenticated = await tcsAuth.Task;
                    ct.ThrowIfCancellationRequested();

                    if (!authenticated)
                    {
                        return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_AUTH_FAILED,
                            "Google Play Games authentication failed (v1).");
                    }
                }
                else
                {
                    // v2: Authenticate(Action<SignInStatus>) OR Authenticate(SignInInteractivity, Action<SignInStatus>)
                    var signInStatusType =
                        Type.GetType("GooglePlayGames.BasicApi.SignInStatus, Google.Play.Games") ??
                        Type.GetType("GooglePlayGames.BasicApi.SignInStatus, GooglePlayGames");

                    if (signInStatusType == null)
                    {
                        return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTHENTICATE,
                            "GPGS SignInStatus type not found (v2).");
                    }

                    var tcsAuth = new TaskCompletionSource<bool>();

                    // Action<SignInStatus> callback: Success => true
                    var statusCb = createAction1(signInStatusType, (obj) =>
                    {
                        var s = obj?.ToString();
                        tcsAuth.TrySetResult(string.Equals(s, "Success", StringComparison.OrdinalIgnoreCase));
                    });

                    MethodInfo authV2 = null;

                    // Prefer interactivity overload if present (allows prompt in some setups)
                    var interactivityType =
                        Type.GetType("GooglePlayGames.BasicApi.SignInInteractivity, Google.Play.Games") ??
                        Type.GetType("GooglePlayGames.BasicApi.SignInInteractivity, GooglePlayGames");

                    if (interactivityType != null)
                    {
                        authV2 = platformType.GetMethod("Authenticate", new[] { interactivityType, statusCb.GetType() });
                        if (authV2 != null)
                        {
                            object interactivityValue = null;

                            // Try enum value: CanPromptOnce
                            var names = Enum.GetNames(interactivityType);
                            for (int i = 0; i < names.Length; i++)
                            {
                                if (string.Equals(names[i], "CanPromptOnce", StringComparison.Ordinal))
                                {
                                    interactivityValue = Enum.Parse(interactivityType, names[i]);
                                    break;
                                }
                            }

                            // Fallback: first enum value
                            if (interactivityValue == null)
                            {
                                var values = Enum.GetValues(interactivityType);
                                interactivityValue = values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(interactivityType);
                            }

                            authV2.Invoke(instance, new object[] { interactivityValue, statusCb });

                            authenticated = await tcsAuth.Task;
                            ct.ThrowIfCancellationRequested();

                            if (!authenticated)
                            {
                                return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_AUTH_FAILED,
                                    "Google Play Games authentication failed (v2 interactivity).");
                            }
                        }
                    }

                    // Fallback: Authenticate(Action<SignInStatus>)
                    if (authV2 == null)
                    {
                        authV2 = platformType.GetMethod("Authenticate", new[] { statusCb.GetType() });
                        if (authV2 == null)
                        {
                            return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTHENTICATE,
                                "PlayGamesPlatform.Authenticate overload not found for v1 or v2.");
                        }

                        authV2.Invoke(instance, new object[] { statusCb });

                        authenticated = await tcsAuth.Task;
                        ct.ThrowIfCancellationRequested();

                        if (!authenticated)
                        {
                            return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_AUTH_FAILED,
                                "Google Play Games authentication failed (v2).");
                        }
                    }
                }

                // RequestServerSideAccess → server auth code
                string serverAuthCode;

                // v1: RequestServerSideAccess(bool, Action<string>)
                var v1Method = platformType.GetMethod("RequestServerSideAccess",
                    new[] { typeof(bool), typeof(Action<string>) });

                if (v1Method != null)
                {
                    // GPGS v1 path
                    var tcsCode = new TaskCompletionSource<string>();
                    Action<string> codeCallback = code => tcsCode.TrySetResult(code);
                    v1Method.Invoke(instance, new object[] { false, codeCallback });

                    serverAuthCode = await tcsCode.Task;
                    ct.ThrowIfCancellationRequested();
                }
                else
                {
                    // GPGS v2 path: discover RequestServerSideAccess overload dynamically
                    MethodInfo req = null;
                    var methods = platformType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        var m = methods[i];
                        if (m.Name != "RequestServerSideAccess") continue;

                        var mps = m.GetParameters();
                        if (mps.Length < 2) continue;

                        // last parameter should be a delegate (often Action<AuthResponse>)
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

                    // Determine AuthResponse type from Action<AuthResponse>
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

                    // Build args for the discovered overload:
                    // first param is usually bool forceRefreshToken
                    var args = new object[reqPs.Length];
                    args[0] = false;

                    // middle params: try to supply empty List<> if required, else null
                    for (int i = 1; i < reqPs.Length - 1; i++)
                    {
                        var t = reqPs[i].ParameterType;
                        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            args[i] = Activator.CreateInstance(t); // empty list
                        }
                        else
                        {
                            args[i] = null;
                        }
                    }

                    // callback
                    args[reqPs.Length - 1] = authResponseCb;

                    req.Invoke(instance, args);

                    serverAuthCode = await tcsCode.Task;
                    ct.ThrowIfCancellationRequested();
                }

                if (string.IsNullOrEmpty(serverAuthCode))
                {
                    return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_NO_AUTH_CODE,
                        "Failed to obtain server auth code from GPGS.");
                }

                return CoreResult<LoginCredential>.Success(new LoginCredential(null, null, null, serverAuthCode));
            }
            catch (Exception ex)
            {
                // Keep error detail for debugging (message alone is often insufficient for reflection issues).
                return CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_GPGS_EXCEPTION, ex.ToString());
            }
        }

        /// <summary>
        /// Creates a typed Action&lt;T&gt; delegate at runtime from Action&lt;object&gt;.
        /// Used for GPGS v2 reflection where callback types are only known at runtime.
        /// </summary>
        private static Delegate createAction1(Type argType, Action<object> handler)
        {
            var param = Expression.Parameter(argType, "x");
            var converted = Expression.Convert(param, typeof(object));
            var call = Expression.Call(
                Expression.Constant(handler),
                typeof(Action<object>).GetMethod("Invoke"),
                converted);
            var lambda = Expression.Lambda(typeof(Action<>).MakeGenericType(argType), call, param);
            return lambda.Compile();
        }
#endif

        private async Task<CoreResult<bool>> signInAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            switch (loginType)
            {
                case LoginType.EditorLogin:
                case LoginType.GuestLogin:
                    return await signInAnonymousAsync(ct);

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GoogleLogin:
                    return await signInOrLinkGoogleAsync(credential, ct);
#endif

#if UNITY_IOS && !UNITY_EDITOR
                case LoginType.AppleLogin:
                    return await signInOrLinkAppleAsync(credential, ct);
#endif

                default:
                    return CoreResult<bool>.Failure(CommonErrorType.LOGIN_UNSUPPORTED, $"LoginType {loginType} is not supported on this platform.");
            }
        }

        private async Task<CoreResult<bool>> signInAnonymousAsync(CancellationToken ct)
        {
            try
            {
                await _auth.SignInAnonymouslyAsync();
                ct.ThrowIfCancellationRequested();
                if (_auth.CurrentUser == null)
                {
                    return CoreResult<bool>.Failure(CommonErrorType.LOGIN_ANONYMOUS_FAILED, "Anonymous sign-in returned no user.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOGIN_ANONYMOUS_EXCEPTION, ex.Message);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task<CoreResult<bool>> signInOrLinkGoogleAsync(LoginCredential credential, CancellationToken ct)
        {
            if (credential == null || string.IsNullOrEmpty(credential.ServerAuthCode))
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_MISSING_AUTH_CODE, "Google ServerAuthCode is required.");
            }

            try
            {
                var cred = PlayGamesAuthProvider.GetCredential(credential.ServerAuthCode);

                if (_auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous)
                {
                    var linked = await _auth.CurrentUser.LinkWithCredentialAsync(cred);
                    ct.ThrowIfCancellationRequested();
                    if (linked == null)
                    {
                        return CoreResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_LINK_FAILED, "Failed to link Google credential to anonymous user.");
                    }

                    return CoreResult<bool>.Success(true);
                }

                var signed = await _auth.SignInWithCredentialAsync(cred);
                ct.ThrowIfCancellationRequested();
                if (signed == null)
                {
                    return CoreResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_SIGNIN_FAILED, "Failed to sign in with Google credential.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_EXCEPTION, ex.Message);
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private async Task<CoreResult<bool>> signInOrLinkAppleAsync(LoginCredential credential, CancellationToken ct)
        {
            if (credential == null || string.IsNullOrEmpty(credential.IdToken) || string.IsNullOrEmpty(credential.RawNonce))
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOGIN_APPLE_MISSING_TOKEN, "Apple IdToken and RawNonce are required.");
            }

            try
            {
                // Apple uses OAuth provider in Firebase Auth.
                var provider = new OAuthProvider("apple.com");
                var cred = provider.GetCredential(credential.IdToken, credential.RawNonce, null);

                if (_auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous)
                {
                    var linked = await _auth.CurrentUser.LinkWithCredentialAsync(cred);
                    ct.ThrowIfCancellationRequested();
                    if (linked == null)
                    {
                        return CoreResult<bool>.Failure(CommonErrorType.LOGIN_APPLE_LINK_FAILED, "Failed to link Apple credential to anonymous user.");
                    }

                    return CoreResult<bool>.Success(true);
                }

                var signed = await _auth.SignInWithCredentialAsync(cred);
                ct.ThrowIfCancellationRequested();
                if (signed == null)
                {
                    return CoreResult<bool>.Failure(CommonErrorType.LOGIN_APPLE_SIGNIN_FAILED, "Failed to sign in with Apple credential.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOGIN_APPLE_EXCEPTION, ex.Message);
            }
        }
#endif

    }

    /// <summary>
    /// Credential container.
    /// - Guest/Editor: not used (LoginCredential.Empty())
    /// - Google(Android): ServerAuthCode required (internally acquired via GPGS Reflection)
    /// - Apple(iOS): IdToken + RawNonce required (caller-provided)
    /// </summary>
    public sealed class LoginCredential
    {
        public string IdToken { get; }
        public string AccessToken { get; }
        public string RawNonce { get; }
        public string ServerAuthCode { get; }

        public LoginCredential(string idToken, string accessToken, string rawNonce, string serverAuthCode = null)
        {
            IdToken = idToken;
            AccessToken = accessToken;
            RawNonce = rawNonce;
            ServerAuthCode = serverAuthCode;
        }

        public static LoginCredential Empty() => new LoginCredential(null, null, null);
    }
}
