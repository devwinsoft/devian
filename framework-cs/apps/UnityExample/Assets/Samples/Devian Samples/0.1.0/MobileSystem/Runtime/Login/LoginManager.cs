using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
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
    /// Order: sign-in (type-based via 3 managers) -> CloudSaveManager.InitializeAsync
    /// - Editor/Guest: FirebaseLoginController (Anonymous)
    /// - Google (Android): GpgsLoginController
    /// - Apple (iOS): AppleLoginController
    /// Sync is handled by SyncDataManager (separate responsibility).
    /// </summary>
    public sealed class LoginManager : CompoSingleton<LoginManager>
    {
        private FirebaseLoginController _firebaseLogin;
        private GpgsLoginController _gpgs;
        private AppleLoginController _apple;
        private LoginType _currentLoginType;

        public void Initialize()
        {
            _firebaseLogin = new FirebaseLoginController();
            _gpgs = new GpgsLoginController();
            _apple = new AppleLoginController();
        }

        /// <summary>
        /// Convenience overload — internally acquires credential for the given LoginType.
        /// Google(Android) uses GPGS Reflection; Apple(iOS) is not supported (use the credential overload).
        /// </summary>
        public async Task<CoreResult<bool>> LoginAsync(LoginType loginType, CancellationToken ct)
        {
            if (_firebaseLogin == null || _gpgs == null || _apple == null)
                Initialize();

            var credResult = await getLoginCredentialAsync(loginType, ct);
            if (credResult.IsFailure)
            {
                return CoreResult<bool>.Failure(credResult.Error!);
            }

            return await LoginAsync(loginType, credResult.Value, ct);
        }

        public async Task<CoreResult<bool>> LoginAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            // 1. Ensure managers
            if (_firebaseLogin == null || _gpgs == null || _apple == null)
                Initialize();

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
            if (_firebaseLogin == null || _gpgs == null || _apple == null)
                Initialize();

            switch (_currentLoginType)
            {
                case LoginType.EditorLogin:
                case LoginType.GuestLogin:
                    _firebaseLogin.SignOut();
                    break;
                case LoginType.GoogleLogin:
#if UNITY_ANDROID && !UNITY_EDITOR
                    _gpgs.SignOut();
#endif
                    break;
                case LoginType.AppleLogin:
#if UNITY_IOS && !UNITY_EDITOR
                    _apple.SignOut();
#endif
                    break;
            }

            _currentLoginType = LoginType.EditorLogin;
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
        private async Task<CoreResult<LoginCredential>> getGoogleGpgsCredentialAsync(CancellationToken ct)
        {
            if (_gpgs == null) Initialize();
            return await _gpgs.GetServerAuthCodeCredentialAsync(ct);
        }
#endif

        internal GpgsLoginController _getGpgsLoginController()
        {
            if (_firebaseLogin == null || _gpgs == null || _apple == null)
                Initialize();

            return _gpgs;
        }

        internal AppleLoginController _getAppleLoginController()
        {
            if (_firebaseLogin == null || _gpgs == null || _apple == null)
                Initialize();

            return _apple;
        }

        private async Task<CoreResult<bool>> signInAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            switch (loginType)
            {
                case LoginType.EditorLogin:
                case LoginType.GuestLogin:
                {
                    var r = await _firebaseLogin.SignInAnonymouslyAsync(ct);
                    return r.IsSuccess
                        ? CoreResult<bool>.Success(true)
                        : CoreResult<bool>.Failure(r.Error!);
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GoogleLogin:
                {
                    var r = await _gpgs.SignInIfNeededAsync(ct);
                    return r == CloudSaveResult.Success
                        ? CoreResult<bool>.Success(true)
                        : CoreResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_SIGNIN_FAILED, $"GPGS sign-in failed: {r}");
                }
#endif

#if UNITY_IOS && !UNITY_EDITOR
                case LoginType.AppleLogin:
                {
                    var r = await _apple.SignInAsync(ct);
                    return r.IsSuccess ? CoreResult<bool>.Success(true) : CoreResult<bool>.Failure(r.Error!);
                }
#endif

                default:
                    return CoreResult<bool>.Failure(CommonErrorType.LOGIN_UNSUPPORTED, $"LoginType {loginType} is not supported on this platform.");
            }
        }

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
