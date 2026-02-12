using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;

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
    /// - Google (Android): credential sign-in / link
    /// - Apple (iOS): credential sign-in / link
    /// </summary>
    public sealed class LoginManager : CompoSingleton<LoginManager>
    {
        private bool _initialized;
        private FirebaseAuth _auth;

        public async Task<CoreResult<bool>> LoginAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            // 1. Firebase init
            if (!_initialized)
            {
                var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
                ct.ThrowIfCancellationRequested();
                if (dep != DependencyStatus.Available)
                {
                    return CoreResult<bool>.Failure("login.firebase_dependency", $"Firebase dependency error: {dep}");
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
                    return CoreResult<bool>.Failure("login.unsupported", $"LoginType {loginType} is not supported on this platform.");
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
                    return CoreResult<bool>.Failure("login.anonymous.failed", "Anonymous sign-in returned no user.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure("login.anonymous.exception", ex.Message);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task<CoreResult<bool>> signInOrLinkGoogleAsync(LoginCredential credential, CancellationToken ct)
        {
            if (credential == null || string.IsNullOrEmpty(credential.IdToken))
            {
                return CoreResult<bool>.Failure("login.google.missing_token", "Google IdToken is required.");
            }

            try
            {
                var cred = GoogleAuthProvider.GetCredential(credential.IdToken, credential.AccessToken);

                if (_auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous)
                {
                    var linked = await _auth.CurrentUser.LinkWithCredentialAsync(cred);
                    ct.ThrowIfCancellationRequested();
                    if (linked?.User == null)
                    {
                        return CoreResult<bool>.Failure("login.google.link_failed", "Failed to link Google credential to anonymous user.");
                    }

                    return CoreResult<bool>.Success(true);
                }

                var signed = await _auth.SignInWithCredentialAsync(cred);
                ct.ThrowIfCancellationRequested();
                if (signed?.User == null)
                {
                    return CoreResult<bool>.Failure("login.google.signin_failed", "Failed to sign in with Google credential.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure("login.google.exception", ex.Message);
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private async Task<CoreResult<bool>> signInOrLinkAppleAsync(LoginCredential credential, CancellationToken ct)
        {
            if (credential == null || string.IsNullOrEmpty(credential.IdToken) || string.IsNullOrEmpty(credential.RawNonce))
            {
                return CoreResult<bool>.Failure("login.apple.missing_token", "Apple IdToken and RawNonce are required.");
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
                        return CoreResult<bool>.Failure("login.apple.link_failed", "Failed to link Apple credential to anonymous user.");
                    }

                    return CoreResult<bool>.Success(true);
                }

                var signed = await _auth.SignInWithCredentialAsync(cred);
                ct.ThrowIfCancellationRequested();
                if (signed?.User == null)
                {
                    return CoreResult<bool>.Failure("login.apple.signin_failed", "Failed to sign in with Apple credential.");
                }

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure("login.apple.exception", ex.Message);
            }
        }
#endif
    }

    /// <summary>
    /// Credential container supplied by the caller (UI / native sign-in integration).
    /// - Guest/Editor: not used
    /// - Google(Android): IdToken required, AccessToken optional
    /// - Apple(iOS): IdToken + RawNonce required
    /// </summary>
    public sealed class LoginCredential
    {
        public string IdToken { get; }
        public string AccessToken { get; }
        public string RawNonce { get; }

        public LoginCredential(string idToken, string accessToken, string rawNonce)
        {
            IdToken = idToken;
            AccessToken = accessToken;
            RawNonce = rawNonce;
        }

        public static LoginCredential Empty() => new LoginCredential(null, null, null);
    }
}
