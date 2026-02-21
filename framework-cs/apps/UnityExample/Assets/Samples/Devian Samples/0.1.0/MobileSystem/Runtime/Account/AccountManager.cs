using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;
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
        /// Google(Android) uses GPGS Reflection.
        /// Apple(iOS) requires Apple provider implementation; otherwise use the credential overload.
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
            // Cloud init 실패는 login 실패가 아님 — cloud save만 비활성화되고 login은 성공 처리.
            if (loginType != LoginType.GuestLogin && loginType != LoginType.EditorLogin)
            {
#if !UNITY_EDITOR
                var initResult = await SaveDataManager.Instance._initializeCloudAsync(ct);
                if (initResult.IsFailure)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[AccountManager] Cloud init failed (login proceeds): {initResult.Error}");
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

        /// <summary>
        /// Purchase 인증 여부는 AccountManager 로그인 상태를 기준으로 판단한다.
        /// EditorLogin(기본/로그아웃 상태)만 미인증으로 본다.
        /// </summary>
        public bool IsPurchaseLoginReady()
        {
            return _currentLoginType != LoginType.EditorLogin;
        }

        /// <summary>
        /// Purchase 진입 시 인증 보정:
        /// - 이미 로그인 상태면 즉시 성공
        /// - Android에서는 GPGS silent auth 기반으로 Google login을 자동 시도(UI 없음)
        /// </summary>
        public async Task<CommonResult<bool>> EnsurePurchaseLoginReadyAsync(CancellationToken ct)
        {
            if (IsPurchaseLoginReady())
                return CommonResult<bool>.Success(true);

#if UNITY_ANDROID && !UNITY_EDITOR
            var silentCredential = await _gpgs.GetServerAuthCodeCredentialSilentAsync(ct);
            if (silentCredential.IsFailure)
            {
                Debug.Log($"[AccountManager] Purchase auto-login skipped (silent GPGS unavailable): {silentCredential.Error}");
                return CommonResult<bool>.Success(false);
            }

            var signIn = await signInWithGoogleCredentialAsync(silentCredential.Value, ct);
            if (signIn.IsFailure)
                return CommonResult<bool>.Failure(signIn.Error!);

            _currentLoginType = LoginType.GoogleLogin;
            return CommonResult<bool>.Success(true);
#else
            return CommonResult<bool>.Success(false);
#endif
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

#if UNITY_IOS && !UNITY_EDITOR
                case LoginType.AppleLogin:
                    return await _apple.SignInAsync(ct);
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
                    return await signInWithGoogleCredentialAsync(credential, ct);
                }
#endif

#if UNITY_IOS && !UNITY_EDITOR
                case LoginType.AppleLogin:
                {
                    return await signInWithAppleCredentialAsync(credential, ct);
                }
#endif

                default:
                    return CommonResult<bool>.Failure(CommonErrorType.LOGIN_UNSUPPORTED, $"LoginType {loginType} is not supported on this platform.");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        async Task<CommonResult<bool>> signInWithGoogleCredentialAsync(LoginCredential credential, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credential?.ServerAuthCode))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_MISSING_AUTH_CODE,
                    "Google server auth code is missing. Configure GPGS server-side access and Web client ID.");
            }

            var init = await _firebaseLogin.InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult<bool>.Failure(init.Error!);

            Credential firebaseCredential;
            try
            {
                firebaseCredential = PlayGamesAuthProvider.GetCredential(credential.ServerAuthCode);
            }
            catch (Exception ex)
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOGIN_GOOGLE_SIGNIN_FAILED, ex.Message);
            }

            return await signInOrLinkFirebaseCredentialAsync(
                firebaseCredential,
                CommonErrorType.LOGIN_GOOGLE_LINK_FAILED,
                CommonErrorType.LOGIN_GOOGLE_SIGNIN_FAILED,
                ct);
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        async Task<CommonResult<bool>> signInWithAppleCredentialAsync(LoginCredential credential, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credential?.IdToken) || string.IsNullOrWhiteSpace(credential?.RawNonce))
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOGIN_APPLE_MISSING_TOKEN,
                    "Apple IdToken and RawNonce are required.");
            }

            var init = await _firebaseLogin.InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult<bool>.Failure(init.Error!);

            Credential firebaseCredential;
            try
            {
                firebaseCredential = OAuthProvider.GetCredential(
                    "apple.com",
                    credential.IdToken,
                    credential.RawNonce,
                    credential.AccessToken);
            }
            catch (Exception ex)
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOGIN_APPLE_SIGNIN_FAILED, ex.Message);
            }

            return await signInOrLinkFirebaseCredentialAsync(
                firebaseCredential,
                CommonErrorType.LOGIN_APPLE_LINK_FAILED,
                CommonErrorType.LOGIN_APPLE_SIGNIN_FAILED,
                ct);
        }
#endif

        async Task<CommonResult<bool>> signInOrLinkFirebaseCredentialAsync(
            Credential credential,
            CommonErrorType linkErrorType,
            CommonErrorType signInErrorType,
            CancellationToken ct)
        {
            var init = await _firebaseLogin.InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult<bool>.Failure(init.Error!);

            FirebaseAuth auth;
            try
            {
                auth = FirebaseAuth.DefaultInstance;
            }
            catch (Exception ex)
            {
                return CommonResult<bool>.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, ex.Message);
            }

            if (auth == null)
                return CommonResult<bool>.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseAuth is null.");

            var currentUser = auth.CurrentUser;
            if (currentUser != null && currentUser.IsAnonymous)
            {
                try
                {
                    var linked = await currentUser.LinkWithCredentialAsync(credential);
                    ct.ThrowIfCancellationRequested();
                    if (linked?.User == null)
                        return CommonResult<bool>.Failure(linkErrorType, "Firebase link succeeded but user is null.");
                    return CommonResult<bool>.Success(true);
                }
                catch (Exception linkEx)
                {
                    Debug.LogWarning($"[AccountManager] Firebase link failed; fallback to sign-in. {linkEx.Message}");
                }
            }

            try
            {
                var user = await auth.SignInWithCredentialAsync(credential);
                ct.ThrowIfCancellationRequested();
                if (user == null)
                    return CommonResult<bool>.Failure(signInErrorType, "Firebase sign-in succeeded but user is null.");
                return CommonResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CommonResult<bool>.Failure(signInErrorType, ex.Message);
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
