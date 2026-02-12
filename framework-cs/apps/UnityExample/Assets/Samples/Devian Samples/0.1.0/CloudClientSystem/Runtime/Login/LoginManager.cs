using System;
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
    /// Order: Firebase init -> sign-in (type-based) -> CloudSaveManager.InitializeAsync -> (Sync placeholder)
    /// - Editor/Guest: Firebase Anonymous
    /// - Google (Android): GPGS credential (internally acquired via Reflection) -> PlayGamesAuthProvider sign-in / link
    /// - Apple (iOS): credential sign-in / link
    /// </summary>
    public sealed class LoginManager : CompoSingleton<LoginManager>
    {
        private bool _initialized;
        private FirebaseAuth _auth;

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
                    return CoreResult<bool>.Failure(ErrorClientType.LOGIN_FIREBASE_DEPENDENCY, $"Firebase dependency error: {dep}");
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

            // 3. CloudSaveManager init
            var initResult = await CloudSaveManager.Instance.InitializeAsync(ct);
            if (initResult.IsFailure)
            {
                return CoreResult<bool>.Failure(initResult.Error!);
            }

            // 4. Sync (not implemented — entry point only)

            return CoreResult<bool>.Success(true);
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
                    return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_CREDENTIAL_UNSUPPORTED,
                        $"Internal credential acquisition is not supported for {loginType}. Use LoginAsync(LoginType, LoginCredential, CancellationToken) instead.");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Acquires Google GPGS server auth code via Reflection (compile-safe without GPGS plugin).
        /// Flow: PlayGamesPlatform.Authenticate -> RequestServerSideAccess -> ServerAuthCode
        /// </summary>
        private async Task<CoreResult<LoginCredential>> getGoogleGpgsCredentialAsync(CancellationToken ct)
        {
            try
            {
                var platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                if (platformType == null)
                {
                    return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_GPGS_NOT_FOUND,
                        "GooglePlayGames plugin is not installed.");
                }

                var instanceProp = platformType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                {
                    return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_GPGS_NOT_INITIALIZED,
                        "PlayGamesPlatform.Instance is null. Call PlayGamesPlatform.Activate() first.");
                }

                // Authenticate
                var authenticateMethod = platformType.GetMethod("Authenticate", new[] { typeof(Action<bool>) });
                if (authenticateMethod == null)
                {
                    return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_GPGS_NO_AUTHENTICATE,
                        "PlayGamesPlatform.Authenticate(Action<bool>) not found.");
                }

                var tcsAuth = new TaskCompletionSource<bool>();
                Action<bool> authCallback = success => tcsAuth.TrySetResult(success);
                authenticateMethod.Invoke(instance, new object[] { authCallback });

                var authenticated = await tcsAuth.Task;
                ct.ThrowIfCancellationRequested();

                if (!authenticated)
                {
                    return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_GPGS_AUTH_FAILED,
                        "Google Play Games authentication failed.");
                }

                // RequestServerSideAccess → server auth code
                var requestMethod = platformType.GetMethod("RequestServerSideAccess",
                    new[] { typeof(bool), typeof(Action<string>) });
                if (requestMethod == null)
                {
                    return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_GPGS_NO_SERVER_ACCESS,
                        "PlayGamesPlatform.RequestServerSideAccess not found.");
                }

                var tcsCode = new TaskCompletionSource<string>();
                Action<string> codeCallback = code => tcsCode.TrySetResult(code);
                requestMethod.Invoke(instance, new object[] { false, codeCallback });

                var serverAuthCode = await tcsCode.Task;
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(serverAuthCode))
                {
                    return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_GPGS_NO_AUTH_CODE,
                        "Failed to obtain server auth code from GPGS.");
                }

                return CoreResult<LoginCredential>.Success(
                    new LoginCredential(null, null, null, serverAuthCode));
            }
            catch (Exception ex)
            {
                return CoreResult<LoginCredential>.Failure(ErrorClientType.LOGIN_GPGS_EXCEPTION, ex.Message);
            }
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
                    return CoreResult<bool>.Failure(ErrorClientType.LOGIN_UNSUPPORTED, $"LoginType {loginType} is not supported on this platform.");
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
                    return CoreResult<bool>.Failure(ErrorClientType.LOGIN_ANONYMOUS_FAILED, "Anonymous sign-in returned no user.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(ErrorClientType.LOGIN_ANONYMOUS_EXCEPTION, ex.Message);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task<CoreResult<bool>> signInOrLinkGoogleAsync(LoginCredential credential, CancellationToken ct)
        {
            if (credential == null || string.IsNullOrEmpty(credential.ServerAuthCode))
            {
                return CoreResult<bool>.Failure(ErrorClientType.LOGIN_GOOGLE_MISSING_AUTH_CODE, "Google ServerAuthCode is required.");
            }

            try
            {
                var cred = PlayGamesAuthProvider.GetCredential(credential.ServerAuthCode);

                if (_auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous)
                {
                    var linked = await _auth.CurrentUser.LinkWithCredentialAsync(cred);
                    ct.ThrowIfCancellationRequested();
                    if (linked?.User == null)
                    {
                        return CoreResult<bool>.Failure(ErrorClientType.LOGIN_GOOGLE_LINK_FAILED, "Failed to link Google credential to anonymous user.");
                    }

                    return CoreResult<bool>.Success(true);
                }

                var signed = await _auth.SignInWithCredentialAsync(cred);
                ct.ThrowIfCancellationRequested();
                if (signed?.User == null)
                {
                    return CoreResult<bool>.Failure(ErrorClientType.LOGIN_GOOGLE_SIGNIN_FAILED, "Failed to sign in with Google credential.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(ErrorClientType.LOGIN_GOOGLE_EXCEPTION, ex.Message);
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private async Task<CoreResult<bool>> signInOrLinkAppleAsync(LoginCredential credential, CancellationToken ct)
        {
            if (credential == null || string.IsNullOrEmpty(credential.IdToken) || string.IsNullOrEmpty(credential.RawNonce))
            {
                return CoreResult<bool>.Failure(ErrorClientType.LOGIN_APPLE_MISSING_TOKEN, "Apple IdToken and RawNonce are required.");
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
                    if (linked?.User == null)
                    {
                        return CoreResult<bool>.Failure(ErrorClientType.LOGIN_APPLE_LINK_FAILED, "Failed to link Apple credential to anonymous user.");
                    }

                    return CoreResult<bool>.Success(true);
                }

                var signed = await _auth.SignInWithCredentialAsync(cred);
                ct.ThrowIfCancellationRequested();
                if (signed?.User == null)
                {
                    return CoreResult<bool>.Failure(ErrorClientType.LOGIN_APPLE_SIGNIN_FAILED, "Failed to sign in with Apple credential.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(ErrorClientType.LOGIN_APPLE_EXCEPTION, ex.Message);
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
