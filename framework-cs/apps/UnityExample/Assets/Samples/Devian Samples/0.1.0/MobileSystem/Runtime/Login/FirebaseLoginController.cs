#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Firebase;
using Firebase.Auth;
using UnityEngine;


namespace Devian
{
    /// <summary>
    /// Firebase login manager (Anonymous sign-in only).
    /// </summary>
    public sealed class FirebaseLoginController
    {
        private FirebaseApp? _app;
        private FirebaseAuth? _auth;
        private bool _isInitialized;


        public async Task<CoreResult<bool>> InitializeAsync(CancellationToken ct)
        {
            if (_isInitialized)
                return CoreResult<bool>.Success(true);


            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                ct.ThrowIfCancellationRequested();


                if (status != DependencyStatus.Available)
                {
                    return CoreResult<bool>.Failure(CommonErrorType.FIREBASE_DEPENDENCY,
                        $"Firebase dependencies not available: {status}");
                }


                _app = FirebaseApp.DefaultInstance;
                _auth = FirebaseAuth.DefaultInstance;
                _isInitialized = true;


                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirebaseLoginController] Initialize failed: {ex}");
                return CoreResult<bool>.Failure(CommonErrorType.FIREBASE_DEPENDENCY, ex.Message);
            }
        }


        public async Task<CoreResult<string>> SignInAnonymouslyAsync(CancellationToken ct)
        {
            var init = await InitializeAsync(ct);
            if (init.IsFailure)
                return CoreResult<string>.Failure(init.Error!);


            if (_auth == null)
                return CoreResult<string>.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseAuth is null.");


            try
            {
                var result = await _auth.SignInAnonymouslyAsync();
                ct.ThrowIfCancellationRequested();


                var user = result?.User;
                if (user == null)
                    return CoreResult<string>.Failure(CommonErrorType.FIREBASE_SIGNIN, "Sign-in succeeded but user is null.");


                return CoreResult<string>.Success(user.UserId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirebaseLoginController] SignInAnonymously failed: {ex}");
                return CoreResult<string>.Failure(CommonErrorType.FIREBASE_SIGNIN, ex.Message);
            }
        }


        public CoreResult<bool> SignOut()
        {
            if (!_isInitialized || _auth == null)
                return CoreResult<bool>.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, "Firebase is not initialized.");


            try
            {
                _auth.SignOut();
                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirebaseLoginController] SignOut failed: {ex}");
                return CoreResult<bool>.Failure(CommonErrorType.FIREBASE_SIGNIN, ex.Message);
            }
        }
    }
}
