#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Game;
using Firebase;
using Firebase.Auth;
using UnityEngine;


namespace Devian
{
    /// <summary>
    /// Firebase login manager (Anonymous sign-in only).
    /// </summary>
    public sealed class AccountLoginFirebase
    {
        private FirebaseApp? _app;
        private FirebaseAuth? _auth;
        private bool _isInitialized;


        public async Task<GameResult<bool>> InitializeAsync(CancellationToken ct)
        {
            if (_isInitialized)
                return GameResult<bool>.Success(true);


            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                ct.ThrowIfCancellationRequested();


                if (status != DependencyStatus.Available)
                {
                    return GameResult<bool>.Failure(GAME_ERROR_TYPE.FIREBASE_DEPENDENCY,
                        $"Firebase dependencies not available: {status}");
                }


                _app = FirebaseApp.DefaultInstance;
                _auth = FirebaseAuth.DefaultInstance;
                _isInitialized = true;


                return GameResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountLoginFirebase] Initialize failed: {ex}");
                return GameResult<bool>.Failure(GAME_ERROR_TYPE.FIREBASE_DEPENDENCY, ex.Message);
            }
        }


        public async Task<GameResult<string>> SignInAnonymouslyAsync(CancellationToken ct)
        {
            var init = await InitializeAsync(ct);
            if (init.IsFailure)
                return GameResult<string>.Failure(init.Error!);


            if (_auth == null)
                return GameResult<string>.Failure(GAME_ERROR_TYPE.FIREBASE_NOT_INITIALIZED, "FirebaseAuth is null.");


            // 기존 anonymous user가 있으면 재사용 (새 UID 생성 방지).
            // Firebase SDK auth state 복원이 비동기적이므로,
            // SignInAnonymouslyAsync()를 바로 호출하면 CurrentUser가 null 상태에서
            // 새 계정이 생성될 수 있다.
            var current = _auth.CurrentUser;
            if (current != null && current.IsAnonymous)
                return GameResult<string>.Success(current.UserId);


            try
            {
                var result = await _auth.SignInAnonymouslyAsync();
                ct.ThrowIfCancellationRequested();


                var user = result?.User;
                if (user == null)
                    return GameResult<string>.Failure(GAME_ERROR_TYPE.FIREBASE_SIGNIN, "Sign-in succeeded but user is null.");


                return GameResult<string>.Success(user.UserId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountLoginFirebase] SignInAnonymously failed: {ex}");
                return GameResult<string>.Failure(GAME_ERROR_TYPE.FIREBASE_SIGNIN, ex.Message);
            }
        }


        public GameResult<bool> SignOut()
        {
            if (!_isInitialized || _auth == null)
                return GameResult<bool>.Failure(GAME_ERROR_TYPE.FIREBASE_NOT_INITIALIZED, "Firebase is not initialized.");


            try
            {
                _auth.SignOut();
                return GameResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountLoginFirebase] SignOut failed: {ex}");
                return GameResult<bool>.Failure(GAME_ERROR_TYPE.FIREBASE_SIGNIN, ex.Message);
            }
        }
    }
}
