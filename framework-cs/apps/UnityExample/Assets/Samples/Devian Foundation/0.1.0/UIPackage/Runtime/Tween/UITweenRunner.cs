using System;
using System.Collections;
using UnityEngine;

namespace Devian
{
    public sealed class UITweenRunner : AutoSingleton<UITweenRunner>
    {
        protected override void onInitAwake()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        internal UITweenHandle Play(UITransitionPlayer player, UICompiledTransitionData data, Action onComplete)
        {
            if (player == null || data == null || data.IsEmpty)
            {
                return UITweenHandle.CreateCanceled();
            }

            var handle = new UITweenHandle();
            var coroutine = StartCoroutine(RunCompiled(player, data, handle, onComplete));
            handle.Bind(this, coroutine);
            return handle;
        }

        internal void StopManaged(Coroutine coroutine)
        {
            if (coroutine == null || this == null)
            {
                return;
            }

            StopCoroutine(coroutine);
        }

        private IEnumerator RunCompiled(
            UITransitionPlayer player,
            UICompiledTransitionData data,
            UITweenHandle handle,
            Action onComplete)
        {
            if (player == null || data == null || data.IsEmpty)
            {
                yield break;
            }

            var snapshot = player.CaptureSnapshot();

            if (data.Duration <= 0f)
            {
                player.Apply(data.Evaluate(0f, snapshot));
                handle.Complete();
                onComplete?.Invoke();

                yield break;
            }

            var elapsed = 0f;
            while (elapsed < data.Duration)
            {
                if (handle.IsCanceled)
                {
                    yield break;
                }

                player.Apply(data.Evaluate(elapsed, snapshot));

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            player.Apply(data.Evaluate(data.Duration, snapshot));
            handle.Complete();
            onComplete?.Invoke();
        }
    }
}
