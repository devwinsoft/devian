#nullable enable
using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

namespace Devian
{
    public sealed class AuthFirebaseSample : MonoBehaviour
    {
        private FirebaseApp? _app;
        private FirebaseAuth? _auth;
        private bool _isInitialized;

        private async void Start()
        {
            // Auto-run for quick testing in sample stage.
            await InitializeAsync();

            if (_isInitialized)
            {
                await SignInAnonymouslyAsync();
            }
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            Debug.Log("[AuthFirebaseSample] Initializing Firebase...");

            DependencyStatus status;
            try
            {
                status = await FirebaseApp.CheckAndFixDependenciesAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthFirebaseSample] Firebase dependency check failed: {ex}");
                return;
            }

            if (status != DependencyStatus.Available)
            {
                Debug.LogError($"[AuthFirebaseSample] Firebase dependencies not available: {status}");
                return;
            }

            _app = FirebaseApp.DefaultInstance;
            _auth = FirebaseAuth.DefaultInstance;

            _isInitialized = true;
            Debug.Log("[AuthFirebaseSample] Firebase initialized.");
        }

        public async Task SignInAnonymouslyAsync()
        {
            if (!_isInitialized || _auth == null)
            {
                Debug.LogError("[AuthFirebaseSample] Not initialized.");
                return;
            }

            Debug.Log("[AuthFirebaseSample] Signing in anonymously...");

            try
            {
                var result = await _auth.SignInAnonymouslyAsync();
                var user = result?.User;

                if (user == null)
                {
                    Debug.LogError("[AuthFirebaseSample] Sign-in succeeded but user is null.");
                    return;
                }

                Debug.Log($"[AuthFirebaseSample] Signed in. uid={user.UserId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthFirebaseSample] Sign-in failed: {ex}");
            }
        }

        public async Task GetIdTokenAsync(bool forceRefresh)
        {
            if (!_isInitialized || _auth == null)
            {
                Debug.LogError("[AuthFirebaseSample] Not initialized.");
                return;
            }

            var user = _auth.CurrentUser;
            if (user == null)
            {
                Debug.LogError("[AuthFirebaseSample] No current user. Sign in first.");
                return;
            }

            try
            {
                var token = await user.TokenAsync(forceRefresh);
                Debug.Log($"[AuthFirebaseSample] ID token acquired. (len={token?.Length ?? 0})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthFirebaseSample] TokenAsync failed: {ex}");
            }
        }
    }
}
