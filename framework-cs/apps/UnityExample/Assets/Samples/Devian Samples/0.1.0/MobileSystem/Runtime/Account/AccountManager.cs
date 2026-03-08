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
        NONE = 0,
        EDITOR = 1,
        GUEST = 2,
        GOOGLE = 3,
        APPLE = 4,
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
        private const string LastLoginTypePrefsKey = "Devian.Account.LastLoginType";
        private AccountLoginFirebase _firebaseLogin = new AccountLoginFirebase();
        private AccountLoginGpgs _gpgs = new AccountLoginGpgs();
        private AccountLoginApple _apple = new AccountLoginApple();
        private readonly AccountStorage _storage = new();

        public AccountStorage Storage => _storage;
        public LoginType CurrentLoginType => sanitizeLoginType(_storage.loginType);
        public bool HasAuthenticatedSession => CurrentLoginType != LoginType.NONE && tryHasFirebaseSession();
        public bool CanAttemptCloudSave => canAttemptCloudSave(CurrentLoginType);
        public bool IsLocalOnlySaveMode => !CanAttemptCloudSave;

        protected override void onInitAwake()
        {
            restoreCachedLoginType();
            ApplyStorage(_storage);
        }

        /// <summary>
        /// Convenience overload — internally acquires credential for the given LoginType.
        /// Google(Android) uses GPGS Reflection.
        /// Apple(iOS) requires Apple provider implementation; otherwise use the credential overload.
        /// </summary>
        public async Task<CommonResult<SessionInitSnapshot?>> LoginAsync(LoginType loginType, CancellationToken ct)
        {
            var credResult = await getLoginCredentialAsync(loginType, ct);
            if (credResult.IsFailure)
            {
                return CommonResult<SessionInitSnapshot?>.Failure(credResult.Error!);
            }

            return await LoginAsync(loginType, credResult.Value, ct);
        }

        public async Task<CommonResult<SessionInitSnapshot?>> LoginAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            // 1. Sign-in
            var signInResult = await signInAsync(loginType, credential ?? LoginCredential.Empty(), ct);
            if (signInResult.IsFailure)
            {
                return CommonResult<SessionInitSnapshot?>.Failure(signInResult.Error!);
            }

            writeAccountState(loginType);

            // 2. SaveCloud init policy:
            // - Guest: never
            // - Editor: never (use SaveLocal only)
            // Cloud init 실패는 login 실패가 아님 — cloud save만 비활성화되고 login은 성공 처리.
            if (canAttemptCloudSave(loginType))
            {
#if !UNITY_EDITOR
                var cloudInit = await SaveDataManager.Instance._initializeCloudAsync(ct);
                if (cloudInit.IsFailure)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[AccountManager] Cloud init failed (login proceeds): {cloudInit.Error}");
                }
#endif
            }

            // 3. InitSession (Firebase Functions — remoteConfig + entitlements + purchaseAdjustments)
#if !UNITY_EDITOR
            var initSession = await FirebaseManager.Instance.InitSessionAsync(null, ct);
            if (initSession.IsFailure)
            {
                return CommonResult<SessionInitSnapshot?>.Failure(initSession.Error!);
            }
            return CommonResult<SessionInitSnapshot?>.Success(initSession.Value);
#else
            return CommonResult<SessionInitSnapshot?>.Success(null);
#endif
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
            writeAccountState(LoginType.NONE);
        }

        /// <summary>
        /// 게임 진입 전에 현재 계정 메타를 기준으로 런타임 인증 세션을 복구한다.
        /// - Guest/Editor: anonymous sign-in으로 복구
        /// - Google(Android): GPGS silent auth 기반으로 복구
        /// - Apple(iOS): 현재는 caller-provided credential이 없으므로 자동 복구하지 않음
        /// </summary>
        public async Task<CommonResult<SessionInitSnapshot?>> EnsureRuntimeSessionAsync(CancellationToken ct)
        {
            bool sessionRestored;

            if (HasAuthenticatedSession)
            {
                sessionRestored = true;
            }
            else
            {
                switch (CurrentLoginType)
                {
                    case LoginType.NONE:
                        return CommonResult<SessionInitSnapshot?>.Success(null);

                    case LoginType.EDITOR:
                    case LoginType.GUEST:
                    {
                        var login = await LoginAsync(CurrentLoginType, ct);
                        if (login.IsFailure)
                            return CommonResult<SessionInitSnapshot?>.Failure(login.Error!);
                        // LoginAsync already called InitSession internally
                        return CommonResult<SessionInitSnapshot?>.Success(login.Value);
                    }

#if UNITY_ANDROID && !UNITY_EDITOR
                    case LoginType.GOOGLE:
                    {
                        var silentCredential = await _gpgs.GetServerAuthCodeCredentialSilentAsync(ct);
                        if (silentCredential.IsFailure)
                        {
                            Debug.Log($"[AccountManager] Runtime session restore skipped (silent GPGS unavailable): {silentCredential.Error}");
                            return CommonResult<SessionInitSnapshot?>.Success(null);
                        }

                        var signIn = await signInWithGoogleCredentialAsync(silentCredential.Value, ct);
                        if (signIn.IsFailure)
                            return CommonResult<SessionInitSnapshot?>.Failure(signIn.Error!);

                        writeAccountState(LoginType.GOOGLE);
                        sessionRestored = true;
                        break;
                    }
#endif

                    case LoginType.APPLE:
                    default:
                        return CommonResult<SessionInitSnapshot?>.Success(null);
                }
            }

            // Session exists or was restored (not via LoginAsync) — call InitSession
            if (sessionRestored)
            {
#if !UNITY_EDITOR
                var initSession = await FirebaseManager.Instance.InitSessionAsync(null, ct);
                if (initSession.IsFailure)
                    return CommonResult<SessionInitSnapshot?>.Failure(initSession.Error!);
                return CommonResult<SessionInitSnapshot?>.Success(initSession.Value);
#else
                return CommonResult<SessionInitSnapshot?>.Success(null);
#endif
            }

            return CommonResult<SessionInitSnapshot?>.Success(null);
        }

        /// <summary>
        /// Purchase 인증 여부는 저장된 loginType이 아니라 현재 Firebase 세션 기준으로 판단한다.
        /// </summary>
        public bool IsPurchaseLoginReady()
        {
            return HasAuthenticatedSession;
        }

        /// <summary>
        /// Purchase 진입 시 인증 보정:
        /// - 이미 Firebase 세션이 있으면 즉시 성공
        /// - Guest/Editor는 anonymous sign-in으로 세션을 복구
        /// - Android에서는 GPGS silent auth 기반으로 Google login을 자동 시도(UI 없음)
        /// </summary>
        public async Task<CommonResult<bool>> EnsurePurchaseLoginReadyAsync(CancellationToken ct)
        {
            var runtimeSession = await EnsureRuntimeSessionAsync(ct);
            if (runtimeSession.IsFailure)
                return CommonResult<bool>.Failure(runtimeSession.Error!);

            if (runtimeSession.Value != null)
                return CommonResult<bool>.Success(true);

            if (CurrentLoginType != LoginType.NONE)
                return CommonResult<bool>.Success(false);

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

            writeAccountState(LoginType.GOOGLE);
            return CommonResult<bool>.Success(true);
#else
            return CommonResult<bool>.Success(false);
#endif
        }

        private Task<CommonResult<LoginCredential>> getLoginCredentialAsync(LoginType loginType, CancellationToken ct)
        {
            switch (loginType)
            {
                case LoginType.EDITOR:
                case LoginType.GUEST:
                    return Task.FromResult(CommonResult<LoginCredential>.Success(LoginCredential.Empty()));

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GOOGLE:
                    return getGoogleGpgsCredentialAsync(ct);
#endif

#if UNITY_IOS && !UNITY_EDITOR
                case LoginType.APPLE:
                    return _apple.SignInAsync(ct);
#endif

                default:
                    return Task.FromResult(CommonResult<LoginCredential>.Failure(
                        CommonErrorType.LOGIN_CREDENTIAL_UNSUPPORTED,
                        $"Internal credential acquisition is not supported for {loginType}. Use LoginAsync(LoginType, LoginCredential, CancellationToken) instead."));
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

        private async Task<CommonResult> signInAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            switch (loginType)
            {
                case LoginType.EDITOR:
                case LoginType.GUEST:
                {
                    var r = await _firebaseLogin.SignInAnonymouslyAsync(ct);
                    return r.IsSuccess
                        ? CommonResult.Ok()
                        : CommonResult.Failure(r.Error!);
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GOOGLE:
                {
                    return await signInWithGoogleCredentialAsync(credential, ct);
                }
#endif

#if UNITY_IOS && !UNITY_EDITOR
                case LoginType.APPLE:
                {
                    return await signInWithAppleCredentialAsync(credential, ct);
                }
#endif

                default:
                    return CommonResult.Failure(CommonErrorType.LOGIN_UNSUPPORTED, $"LoginType {loginType} is not supported on this platform.");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        async Task<CommonResult> signInWithGoogleCredentialAsync(LoginCredential credential, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credential?.ServerAuthCode))
            {
                return CommonResult.Failure(CommonErrorType.LOGIN_GOOGLE_MISSING_AUTH_CODE,
                    "Google server auth code is missing. Configure GPGS server-side access and Web client ID.");
            }

            var init = await _firebaseLogin.InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult.Failure(init.Error!);

            Credential firebaseCredential;
            try
            {
                firebaseCredential = PlayGamesAuthProvider.GetCredential(credential.ServerAuthCode);
            }
            catch (Exception ex)
            {
                return CommonResult.Failure(CommonErrorType.LOGIN_GOOGLE_SIGNIN_FAILED, ex.Message);
            }

            return await signInOrLinkFirebaseCredentialAsync(
                firebaseCredential,
                CommonErrorType.LOGIN_GOOGLE_LINK_FAILED,
                CommonErrorType.LOGIN_GOOGLE_SIGNIN_FAILED,
                ct);
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        async Task<CommonResult> signInWithAppleCredentialAsync(LoginCredential credential, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credential?.IdToken) || string.IsNullOrWhiteSpace(credential?.RawNonce))
            {
                return CommonResult.Failure(CommonErrorType.LOGIN_APPLE_MISSING_TOKEN,
                    "Apple IdToken and RawNonce are required.");
            }

            var init = await _firebaseLogin.InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult.Failure(init.Error!);

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
                return CommonResult.Failure(CommonErrorType.LOGIN_APPLE_SIGNIN_FAILED, ex.Message);
            }

            return await signInOrLinkFirebaseCredentialAsync(
                firebaseCredential,
                CommonErrorType.LOGIN_APPLE_LINK_FAILED,
                CommonErrorType.LOGIN_APPLE_SIGNIN_FAILED,
                ct);
        }
#endif

        async Task<CommonResult> signInOrLinkFirebaseCredentialAsync(
            Credential credential,
            CommonErrorType linkErrorType,
            CommonErrorType signInErrorType,
            CancellationToken ct)
        {
            var init = await _firebaseLogin.InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult.Failure(init.Error!);

            FirebaseAuth auth;
            try
            {
                auth = FirebaseAuth.DefaultInstance;
            }
            catch (Exception ex)
            {
                return CommonResult.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, ex.Message);
            }

            if (auth == null)
                return CommonResult.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseAuth is null.");

            var currentUser = auth.CurrentUser;
            if (currentUser != null && currentUser.IsAnonymous)
            {
                try
                {
                    var linked = await currentUser.LinkWithCredentialAsync(credential);
                    ct.ThrowIfCancellationRequested();
                    if (linked?.User == null)
                        return CommonResult.Failure(linkErrorType, "Firebase link succeeded but user is null.");
                    return CommonResult.Ok();
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
                    return CommonResult.Failure(signInErrorType, "Firebase sign-in succeeded but user is null.");
                return CommonResult.Ok();
            }
            catch (Exception ex)
            {
                return CommonResult.Failure(signInErrorType, ex.Message);
            }
        }

        internal void ApplyStorage(AccountStorage storage)
        {
            if (storage == null)
            {
                _storage.Clear();
                cacheLoginType(LoginType.NONE);
                return;
            }

            var loginType = sanitizeLoginType(storage.loginType);
            _storage.Set(
                loginType,
                storage.socialUserId,
                storage.lastUpdatedAtUtcMs);
            cacheLoginType(loginType);
        }

        private void writeAccountState(LoginType loginType)
        {
            var safeLoginType = sanitizeLoginType(loginType);
            _storage.Set(
                safeLoginType,
                resolveSocialUserId(safeLoginType),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cacheLoginType(safeLoginType);
        }

        private void restoreCachedLoginType()
        {
            var raw = PlayerPrefs.GetInt(LastLoginTypePrefsKey, (int)LoginType.NONE);
            var cached = sanitizeLoginType((LoginType)raw);
            if (cached == LoginType.NONE)
                return;

            _storage.Set(cached, _storage.socialUserId, _storage.lastUpdatedAtUtcMs);
        }

        private static void cacheLoginType(LoginType loginType)
        {
            PlayerPrefs.SetInt(LastLoginTypePrefsKey, (int)sanitizeLoginType(loginType));
            PlayerPrefs.Save();
        }

        private static LoginType sanitizeLoginType(LoginType loginType)
        {
            var raw = (int)loginType;
            return Enum.IsDefined(typeof(LoginType), raw)
                ? loginType
                : LoginType.NONE;
        }

        private static bool canAttemptCloudSave(LoginType loginType)
        {
#if UNITY_EDITOR
            return false;
#else
            switch (sanitizeLoginType(loginType))
            {
                case LoginType.GOOGLE:
                case LoginType.APPLE:
                    return true;
                default:
                    return false;
            }
#endif
        }

        private static bool tryHasFirebaseSession()
        {
            try
            {
                return FirebaseAuth.DefaultInstance?.CurrentUser != null;
            }
            catch
            {
                return false;
            }
        }

        private static string resolveSocialUserId(LoginType loginType)
        {
            if (loginType != LoginType.GOOGLE && loginType != LoginType.APPLE)
                return null;

            try
            {
                var user = FirebaseAuth.DefaultInstance?.CurrentUser;
                var uid = user?.UserId;
                return string.IsNullOrWhiteSpace(uid) ? null : uid;
            }
            catch
            {
                return null;
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
