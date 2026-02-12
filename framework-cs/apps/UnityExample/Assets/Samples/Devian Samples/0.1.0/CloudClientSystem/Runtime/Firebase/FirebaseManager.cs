using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using Firebase;
using Firebase.Auth;
using Devian.Domain.Common;

// Apple 플러그인: 프로젝트에서 사용하는 AppleAuth 계열 네임스페이스를 실제에 맞게 사용

namespace Devian
{
    public sealed class FirebaseManager : CompoSingleton<FirebaseManager>
    {
        private bool _initialized;
        private FirebaseAuth _auth;

        public enum AuthLoginType
        {
            None = 0,
            Anonymous = 1,
            Google = 2,
            Apple = 3,
            Facebook = 4,
        }

        private AuthLoginType _currentLoginType = AuthLoginType.None;

        public async Task<CoreResult<bool>> InitializeAsync(CancellationToken ct)
        {
            var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dep != DependencyStatus.Available)
            {
                _initialized = false;
                return CoreResult<bool>.Failure(firebaseError(CommonErrorType.FIREBASE_DEPENDENCY, $"Firebase dependency error: {dep}"));
            }

            _auth = FirebaseAuth.DefaultInstance;
            _initialized = true;

            // Best-effort infer login type if user already exists (app restart scenario).
            if (_auth.CurrentUser != null)
            {
                _currentLoginType = inferLoginType(_auth.CurrentUser);
            }

            return CoreResult<bool>.Success(true);
        }

        public async Task<CoreResult<string>> SignInAnonymouslyAsync(CancellationToken ct)
        {
            if (!_initialized || _auth == null)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseManager is not initialized."));
            }

            try
            {
                var result = await _auth.SignInAnonymouslyAsync();

                var userId = tryGetUserId(result);
                if (string.IsNullOrEmpty(userId))
                {
                    return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_SIGNIN, "Anonymous sign-in returned no user id."));
                }

                _currentLoginType = AuthLoginType.Anonymous;
                return CoreResult<string>.Success(userId);
            }
            catch (Exception ex)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_SIGNIN, ex.Message));
            }
        }

        public async Task<CoreResult<string>> SignInWithGoogleAsync(string idToken, CancellationToken ct)
        {
            if (!_initialized || _auth == null)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseManager is not initialized."));
            }

            if (string.IsNullOrWhiteSpace(idToken))
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_GOOGLE_TOKEN, "Google idToken is empty."));
            }

            try
            {
                var credential = GoogleAuthProvider.GetCredential(idToken, accessToken: null);

                var result = await _auth.SignInWithCredentialAsync(credential);

                var userId = tryGetUserId(result);
                if (string.IsNullOrEmpty(userId))
                {
                    return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_GOOGLE_SIGNIN, "Firebase sign-in returned no user id."));
                }

                _currentLoginType = AuthLoginType.Google;
                return CoreResult<string>.Success(userId);
            }
            catch (Exception ex)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_GOOGLE_SIGNIN, ex.Message));
            }
        }

        public async Task<CoreResult<string>> SignInWithAppleAsync(CancellationToken ct)
        {
            if (!_initialized || _auth == null)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseManager is not initialized."));
            }

            try
            {
                var rawNonce = createNonce();
                var nonceSha256 = sha256(rawNonce);

                var appleTokens = await AppleSignInBridge.SignInAsync(nonceSha256, ct);
                if (appleTokens.IsFailure)
                {
                    return CoreResult<string>.Failure(appleTokens.Error!);
                }

                var identityToken = appleTokens.Value.IdentityToken;
                if (string.IsNullOrEmpty(identityToken))
                {
                    return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_APPLE_TOKEN, "Apple identity token is empty."));
                }

                var credential = OAuthProvider.GetCredential("apple.com", identityToken, rawNonce);

                var result = await _auth.SignInWithCredentialAsync(credential);

                var userId = tryGetUserId(result);
                if (string.IsNullOrEmpty(userId))
                {
                    return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_APPLE_SIGNIN, "Firebase sign-in returned no user id."));
                }

                _currentLoginType = AuthLoginType.Apple;
                return CoreResult<string>.Success(userId);
            }
            catch (Exception ex)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_APPLE_SIGNIN, ex.Message));
            }
        }

        public async Task<CoreResult<string>> SignInWithFacebookAsync(CancellationToken ct)
        {
            // Design only (not implemented)
            await Task.CompletedTask;
            return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_FACEBOOK_NOT_IMPLEMENTED,
                "Facebook re-login is designed but not implemented yet."));
        }

        public async Task<CoreResult<string>> GetIdTokenAsync(bool forceRefresh, CancellationToken ct)
        {
            if (!_initialized || _auth == null)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseManager is not initialized."));
            }

            var user = _auth.CurrentUser;
            if (user == null)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_NO_USER, "No current user. Sign in first."));
            }

            try
            {
                var token = await user.TokenAsync(forceRefresh);
                return CoreResult<string>.Success(token);
            }
            catch (Exception ex)
            {
                return CoreResult<string>.Failure(firebaseError(CommonErrorType.FIREBASE_TOKEN, ex.Message));
            }
        }

        public async Task<CoreResult<string>> GetIdToken(CancellationToken ct)
        {
            // 1) Try normal token fetch
            var token = await GetIdTokenAsync(forceRefresh: false, ct);
            if (token.IsSuccess)
            {
                return token;
            }

            // 2) Try forced refresh once
            var tokenForced = await GetIdTokenAsync(forceRefresh: true, ct);
            if (tokenForced.IsSuccess)
            {
                return tokenForced;
            }

            // 3) If there is no current user, attempt re-login based on current login type
            if (_auth == null)
            {
                return tokenForced;
            }

            if (_auth.CurrentUser != null)
            {
                // User exists but token refresh failed -> do not auto re-login loop; return failure.
                return tokenForced;
            }

            var reauth = await reLoginByCurrentType(ct);
            if (reauth.IsFailure)
            {
                return CoreResult<string>.Failure(reauth.Error!);
            }

            // 4) Retry token fetch after re-login
            return await GetIdTokenAsync(forceRefresh: false, ct);
        }

        public CoreResult<bool> SignOut()
        {
            if (_auth == null)
            {
                return CoreResult<bool>.Failure(firebaseError(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseManager is not initialized."));
            }

            _auth.SignOut();
            _currentLoginType = AuthLoginType.None;
            return CoreResult<bool>.Success(true);
        }

        // --- Private helpers ---

        private static CoreError firebaseError(CommonErrorType code, string message)
        {
            return new CoreError(code, message);
        }

        private static string tryGetUserId(object signInResult)
        {
            if (signInResult == null) return null;

            // Case A: SDK returns FirebaseUser directly
            if (signInResult is FirebaseUser firebaseUser)
            {
                return firebaseUser.UserId;
            }

            var t = signInResult.GetType();

            // Case B: SDK returns AuthResult-like object that has User (FirebaseUser)
            var userProp = t.GetProperty("User");
            if (userProp != null)
            {
                var u = userProp.GetValue(signInResult) as FirebaseUser;
                if (u != null) return u.UserId;
            }

            // Case C: some SDKs may expose UserId directly
            var userIdProp = t.GetProperty("UserId");
            if (userIdProp != null)
            {
                return userIdProp.GetValue(signInResult) as string;
            }

            return null;
        }

        private static AuthLoginType inferLoginType(FirebaseUser user)
        {
            foreach (var info in user.ProviderData)
            {
                switch (info.ProviderId)
                {
                    case "google.com":
                        return AuthLoginType.Google;
                    case "apple.com":
                        return AuthLoginType.Apple;
                    case "facebook.com":
                        return AuthLoginType.Facebook;
                }
            }

            if (user.IsAnonymous)
            {
                return AuthLoginType.Anonymous;
            }

            return AuthLoginType.None;
        }

        private async Task<CoreResult<bool>> reLoginByCurrentType(CancellationToken ct)
        {
            switch (_currentLoginType)
            {
                case AuthLoginType.Anonymous:
                {
                    var r = await SignInAnonymouslyAsync(ct);
                    return r.IsSuccess ? CoreResult<bool>.Success(true) : CoreResult<bool>.Failure(r.Error!);
                }

                case AuthLoginType.Google:
                {
                    await Task.CompletedTask;
                    return CoreResult<bool>.Failure(firebaseError(CommonErrorType.FIREBASE_GOOGLE_REAUTH_REQUIRED,
                        "Google re-login requires an idToken from the app layer. Call SignInWithGoogleAsync(idToken, ...)."));
                }

                case AuthLoginType.Apple:
                {
                    var r = await SignInWithAppleAsync(ct);
                    return r.IsSuccess ? CoreResult<bool>.Success(true) : CoreResult<bool>.Failure(r.Error!);
                }

                case AuthLoginType.Facebook:
                {
                    // Design only
                    await Task.CompletedTask;
                    return CoreResult<bool>.Failure(firebaseError(CommonErrorType.FIREBASE_FACEBOOK_NOT_IMPLEMENTED,
                        "Facebook re-login is designed but not implemented yet."));
                }

                default:
                    await Task.CompletedTask;
                    return CoreResult<bool>.Failure(firebaseError(CommonErrorType.FIREBASE_REAUTH_UNKNOWN, "No known login type to re-authenticate."));
            }
        }

        private readonly struct AppleTokens
        {
            public AppleTokens(string identityToken)
            {
                IdentityToken = identityToken;
            }

            public string IdentityToken { get; }
        }

        private static class AppleSignInBridge
        {
            public static async Task<CoreResult<AppleTokens>> SignInAsync(string nonceSha256, CancellationToken ct)
            {
                // TODO: 프로젝트에서 사용하는 Apple Sign-In 플러그인 API로 실제 구현할 것.
                // 원래 설계 방침: FirebaseManager에 옵션(#if)로 "있는 척"하지 않는다.
                // 따라서 여기서도 "미연결 스텁 성공" 같은 옵션 동작을 만들지 말고,
                // 실제 플러그인 호출로 채우기 전까지는 명확히 실패 반환을 유지한다.
                await Task.CompletedTask;
                return CoreResult<AppleTokens>.Failure(firebaseError(CommonErrorType.FIREBASE_APPLE_BRIDGE, "AppleSignInBridge is not wired to a plugin implementation."));
            }
        }

        private static string createNonce()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string sha256(string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
