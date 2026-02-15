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
    /// Order: sign-in (type-based via 3 managers) -> SaveDataManager._initializeCloudAsync
    /// - Editor/Guest: AccountLoginFirebase (Anonymous)
    /// - Google (Android): AccountLoginGpgs
    /// - Apple (iOS): AccountLoginApple
    /// Sync is handled by SaveDataManager (separate responsibility).
    /// </summary>
    public sealed class AccountManager : CompoSingleton<AccountManager>
    {
        private AccountLoginFirebase _firebaseLogin = new AccountLoginFirebase();
        private AccountLoginGpgs _gpgs = new AccountLoginGpgs();
        private AccountLoginApple _apple = new AccountLoginApple();
        private LoginType _currentLoginType;

        /// <summary>
        /// Convenience overload — internally acquires credential for the given LoginType.
        /// Google(Android) uses GPGS Reflection; Apple(iOS) is not supported (use the credential overload).
        /// </summary>
        public async Task<CommonResult<bool>> LoginAsync(LoginType loginType, CancellationToken ct)
        {
            var credResult = await getLoginCredentialAsync(loginType, ct);
            if (credResult.IsFailure)
            {
                return CommonResult<bool>.Failure(credResult.Error!);
            }

            return await LoginAsync(loginType, credResult.Value, ct);
        }

        public async Task<CommonResult<bool>> LoginAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            // 1. Sign-in
            var signInResult = await signInAsync(loginType, credential ?? LoginCredential.Empty(), ct);
            if (signInResult.IsFailure)
            {
                return signInResult;
            }

            _currentLoginType = loginType;

            // 2. SaveCloud init policy:
            // - Guest: never
            // - Editor: never (use SaveLocal only)
            if (loginType != LoginType.GuestLogin && loginType != LoginType.EditorLogin)
            {
#if !UNITY_EDITOR
                var initResult = await SaveDataManager.Instance._initializeCloudAsync(ct);
                if (initResult.IsFailure)
                {
                    return CommonResult<bool>.Failure(initResult.Error!);
                }
#endif
            }

            return CommonResult<bool>.Success(true);
        }

        public void Logout()
        {
            // Complete logout: try sign-out from all providers regardless of current login type.
            // Failures are ignored (Logout is void).

            // 1) Firebase (Guest/Editor included) - always try
            try { _firebaseLogin?.SignOut(); } catch { /* ignore */ }

            // 2) GPGS - Android only
#if UNITY_ANDROID && !UNITY_EDITOR
            try { _gpgs?.SignOut(); } catch { /* ignore */ }
#endif

            // 3) Apple - iOS only
#if UNITY_IOS && !UNITY_EDITOR
            try { _apple?.SignOut(); } catch { /* ignore */ }
#endif

            // 4) Reset state
            _currentLoginType = LoginType.EditorLogin;
        }

        public LoginType _getCurrentLoginType()
        {
            return _currentLoginType;
        }

        private async Task<CommonResult<LoginCredential>> getLoginCredentialAsync(LoginType loginType, CancellationToken ct)
        {
            switch (loginType)
            {
                case LoginType.EditorLogin:
                case LoginType.GuestLogin:
                    return CommonResult<LoginCredential>.Success(LoginCredential.Empty());

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GoogleLogin:
                    return await getGoogleGpgsCredentialAsync(ct);
#endif

                default:
                    return CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_CREDENTIAL_UNSUPPORTED,
                        $"Internal credential acquisition is not supported for {loginType}. Use LoginAsync(LoginType, LoginCredential, CancellationToken) instead.");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task<CommonResult<LoginCredential>> getGoogleGpgsCredentialAsync(CancellationToken ct)
        {
            return await _gpgs.GetServerAuthCodeCredentialAsync(ct);
        }
#endif

        internal AccountLoginGpgs _getAccountLoginGpgs() => _gpgs;

        internal AccountLoginApple _getAccountLoginApple() => _apple;

        private async Task<CommonResult<bool>> signInAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            switch (loginType)
            {
                case LoginType.EditorLogin:
                case LoginType.GuestLogin:
                {
                    var r = await _firebaseLogin.SignInAnonymouslyAsync(ct);
                    return r.IsSuccess
                        ? CommonResult<bool>.Success(true)
                        : CommonResult<bool>.Failure(r.Error!);
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GoogleLogin:
                {
                    // GetServerAuthCodeCredentialAsync already calls ManuallyAuthenticate,
                    // so if we have a valid ServerAuthCode the user is already authenticated.
                    // Only call SignInIfNeededAsync when no credential was provided (direct call).
                    if (!string.IsNullOrEmpty(credential.ServerAuthCode))
                    {
                        return CommonResult<bool>.Success(true);
                    }

                    var r = await _gpgs.SignInIfNeededAsync(ct);
                    return r == SaveCloudResult.Success
                        ? CommonResult<bool>.Success(true)
                        : CommonResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_SIGNIN_FAILED, $"GPGS sign-in failed: {r}");
                }
#endif

#if UNITY_IOS && !UNITY_EDITOR
                case LoginType.AppleLogin:
                {
                    var r = await _apple.SignInAsync(ct);
                    return r.IsSuccess ? CommonResult<bool>.Success(true) : CommonResult<bool>.Failure(r.Error!);
                }
#endif

                default:
                    return CommonResult<bool>.Failure(CommonErrorType.LOGIN_UNSUPPORTED, $"LoginType {loginType} is not supported on this platform.");
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
