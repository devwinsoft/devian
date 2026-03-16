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
    /// Account authentication manager.
    /// - Editor/Guest: AccountLoginFirebase (Anonymous)
    /// - Google (Android): AccountLoginGpgs
    /// - Apple (iOS): AccountLoginApple
    /// Data sync / init-session orchestration is handled by LoginManager.
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
        public async Task<CommonResult> LoginAsync(LoginType loginType, CancellationToken ct)
        {
            var credResult = await getLoginCredentialAsync(loginType, ct);
            if (credResult.IsFailure)
            {
                return CommonResult.Failure(credResult.Error!);
            }

            return await LoginAsync(loginType, credResult.Value, ct);
        }

        public async Task<CommonResult> LoginAsync(LoginType loginType, LoginCredential credential, CancellationToken ct)
        {
            var signInResult = await signInAsync(loginType, credential ?? LoginCredential.Empty(), ct);
            if (signInResult.IsFailure)
                return CommonResult.Failure(signInResult.Error!);

            writeAccountState(loginType);
            return CommonResult.Ok();
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

        public async Task<CommonResult<bool>> EnsureRuntimeAuthSessionAsync(CancellationToken ct)
        {
            if (HasAuthenticatedSession)
                return CommonResult<bool>.Success(true);

            switch (CurrentLoginType)
            {
                case LoginType.NONE:
                    return CommonResult<bool>.Success(false);

                case LoginType.EDITOR:
                case LoginType.GUEST:
                {
                    var login = await LoginAsync(CurrentLoginType, ct);
                    if (login.IsFailure)
                        return CommonResult<bool>.Failure(login.Error!);
                    return CommonResult<bool>.Success(true);
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                case LoginType.GOOGLE:
                    return await TryRestoreGoogleAuthAsync(ct);
#endif

                case LoginType.APPLE:
                default:
                    return CommonResult<bool>.Success(false);
            }
        }

        public async Task<CommonResult<bool>> TryRestoreGoogleAuthAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var silentCredential = await _gpgs.GetServerAuthCodeCredentialSilentAsync(ct);
            if (silentCredential.IsFailure)
            {
                Debug.Log($"[AccountManager] Google silent auth unavailable: {silentCredential.Error}");
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
                        COMMON_ERROR_TYPE.LOGIN_CREDENTIAL_UNSUPPORTED,
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
                    return CommonResult.Failure(COMMON_ERROR_TYPE.LOGIN_UNSUPPORTED, $"LoginType {loginType} is not supported on this platform.");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        async Task<CommonResult> signInWithGoogleCredentialAsync(LoginCredential credential, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credential?.ServerAuthCode))
            {
                return CommonResult.Failure(COMMON_ERROR_TYPE.LOGIN_GOOGLE_MISSING_AUTH_CODE,
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
                return CommonResult.Failure(COMMON_ERROR_TYPE.LOGIN_GOOGLE_SIGNIN_FAILED, ex.Message);
            }

            return await signInOrLinkFirebaseCredentialAsync(
                firebaseCredential,
                COMMON_ERROR_TYPE.LOGIN_GOOGLE_LINK_FAILED,
                COMMON_ERROR_TYPE.LOGIN_GOOGLE_SIGNIN_FAILED,
                ct);
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        async Task<CommonResult> signInWithAppleCredentialAsync(LoginCredential credential, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credential?.IdToken) || string.IsNullOrWhiteSpace(credential?.RawNonce))
            {
                return CommonResult.Failure(COMMON_ERROR_TYPE.LOGIN_APPLE_MISSING_TOKEN,
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
                return CommonResult.Failure(COMMON_ERROR_TYPE.LOGIN_APPLE_SIGNIN_FAILED, ex.Message);
            }

            return await signInOrLinkFirebaseCredentialAsync(
                firebaseCredential,
                COMMON_ERROR_TYPE.LOGIN_APPLE_LINK_FAILED,
                COMMON_ERROR_TYPE.LOGIN_APPLE_SIGNIN_FAILED,
                ct);
        }
#endif

        async Task<CommonResult> signInOrLinkFirebaseCredentialAsync(
            Credential credential,
            COMMON_ERROR_TYPE linkErrorType,
            COMMON_ERROR_TYPE signInErrorType,
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
                return CommonResult.Failure(COMMON_ERROR_TYPE.FIREBASE_NOT_INITIALIZED, ex.Message);
            }

            if (auth == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.FIREBASE_NOT_INITIALIZED, "FirebaseAuth is null.");

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
