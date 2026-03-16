using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using UnityEngine;
using Firebase.Analytics;

namespace Devian
{
    /// <summary>
    /// Firebase Analytics wrapper.
    /// Provides cross-platform LogEvent API for custom analytics events.
    /// </summary>
    public sealed class AnalyzeManager : CompoSingleton<AnalyzeManager>
    {
        private const string Tag = nameof(AnalyzeManager);

        private bool _initialized;

        public bool IsInitialized => _initialized;

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
                return Task.FromResult(CommonResult.Ok());

            _initialized = true;
            Debug.Log($"[{Tag}] Initialized.");
            return Task.FromResult(CommonResult.Ok());
        }

        public void LogEvent(string eventName)
        {
            if (!guardInitialized(eventName)) return;

            FirebaseAnalytics.LogEvent(eventName);
        }

        public void LogEvent(string eventName, string paramName, string paramValue)
        {
            if (!guardInitialized(eventName)) return;

            FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);
        }

        public void LogEvent(string eventName, string paramName, long paramValue)
        {
            if (!guardInitialized(eventName)) return;

            FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);
        }

        public void LogEvent(string eventName, params Parameter[] parameters)
        {
            if (!guardInitialized(eventName)) return;

            FirebaseAnalytics.LogEvent(eventName, parameters);
        }

        private bool guardInitialized(string eventName)
        {
            if (_initialized) return true;

            Debug.LogWarning($"[{Tag}] LogEvent('{eventName}') dropped — not initialized.");
            return false;
        }
    }
}
