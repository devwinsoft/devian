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
    public sealed class AccountLoginFirebase
    {
        private FirebaseApp? _app;
        private FirebaseAuth? _auth;
        private bool _isInitialized;


        public async Task<CommonResult<bool>> InitializeAsync(CancellationToken ct)
        {
            if (_isInitialized)
                return CommonResult<bool>.Success(true);


            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                ct.ThrowIfCancellationRequested();


                if (status != DependencyStatus.Available)
                {
                    return CommonResult<bool>.Failure(CommonErrorType.FIREBASE_DEPENDENCY,
                        $"Firebase dependencies not available: {status}");
                }


                _app = FirebaseApp.DefaultInstance;
                _auth = FirebaseAuth.DefaultInstance;
                _isInitialized = true;


                return CommonResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountLoginFirebase] Initialize failed: {ex}");
                return CommonResult<bool>.Failure(CommonErrorType.FIREBASE_DEPENDENCY, ex.Message);
            }
        }


        public async Task<CommonResult<string>> SignInAnonymouslyAsync(CancellationToken ct)
        {
            var init = await InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult<string>.Failure(init.Error!);


            if (_auth == null)
                return CommonResult<string>.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, "FirebaseAuth is null.");


            try
            {
                var result = await _auth.SignInAnonymouslyAsync();
                ct.ThrowIfCancellationRequested();


                var user = result?.User;
                if (user == null)
                    return CommonResult<string>.Failure(CommonErrorType.FIREBASE_SIGNIN, "Sign-in succeeded but user is null.");


                return CommonResult<string>.Success(user.UserId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountLoginFirebase] SignInAnonymously failed: {ex}");
                return CommonResult<string>.Failure(CommonErrorType.FIREBASE_SIGNIN, ex.Message);
            }
        }


        public CommonResult<bool> SignOut()
        {
            if (!_isInitialized || _auth == null)
                return CommonResult<bool>.Failure(CommonErrorType.FIREBASE_NOT_INITIALIZED, "Firebase is not initialized.");


            try
            {
                _auth.SignOut();
                return CommonResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountLoginFirebase] SignOut failed: {ex}");
                return CommonResult<bool>.Failure(CommonErrorType.FIREBASE_SIGNIN, ex.Message);
            }
        }
    }
}
