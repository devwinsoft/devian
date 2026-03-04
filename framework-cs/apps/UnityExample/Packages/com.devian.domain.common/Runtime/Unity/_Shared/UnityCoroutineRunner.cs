// Unity Shared - UnityCoroutineRunner
// SSOT: skills/devian-unity/11-common-system/32-unity-utils/SKILL.md
// NOTE: 이 파일은 Generated 폴더 산출물이 아닌 고정 유틸(수기 유지)이며,
//       정본은 upm 경로다. Packages는 복사본.

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Bridges Unity Coroutines to async/await via TaskCompletionSource.
    /// </summary>
    public static class UnityCoroutineRunner
    {
        /// <summary>
        /// Runs a coroutine and returns a Task that completes when the coroutine finishes.
        /// Supports CancellationToken for cancellation (StopCoroutine + TrySetCanceled).
        /// </summary>
        /// <param name="host">MonoBehaviour instance to run the coroutine on.</param>
        /// <param name="routine">The coroutine to execute.</param>
        /// <param name="ct">Optional cancellation token.</param>
        public static Task RunAsync(MonoBehaviour host, IEnumerator routine, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<bool>();

            CancellationTokenRegistration reg = default;
            Coroutine running = null;

            if (ct.CanBeCanceled)
            {
                reg = ct.Register(() =>
                {
                    if (running != null) host.StopCoroutine(running);
                    tcs.TrySetCanceled(ct);
                });
            }

            IEnumerator Wrapper()
            {
                yield return routine;
                reg.Dispose();
                tcs.TrySetResult(true);
            }

            running = host.StartCoroutine(Wrapper());
            return tcs.Task;
        }
    }
}
