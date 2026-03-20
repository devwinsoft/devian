using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public sealed class UITweenRunner : AutoSingleton<UITweenRunner>
    {
        protected override void onInitAwake()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        internal UITweenHandle Play(UITransitionPlayer player, UITransitionPreset preset, Action onComplete)
        {
            if (player == null || preset == null)
            {
                return UITweenHandle.CreateCanceled();
            }

            var handle = new UITweenHandle();
            var coroutine = StartCoroutine(RunSingle(player, preset, handle, onComplete));
            handle.Bind(this, coroutine);
            return handle;
        }

        internal UITweenHandle Play(UITransitionPlayer player, UITweenSequence sequence, Action onComplete)
        {
            if (player == null || sequence == null || sequence.IsEmpty)
            {
                return UITweenHandle.CreateCanceled();
            }

            var handle = new UITweenHandle();
            var coroutine = StartCoroutine(RunSequence(player, sequence, handle, onComplete));
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

        private IEnumerator RunSingle(
            UITransitionPlayer player,
            UITransitionPreset preset,
            UITweenHandle handle,
            Action onComplete)
        {
            yield return RunGroup(player, new[] { preset }, handle);

            if (handle.IsCanceled)
            {
                yield break;
            }

            handle.Complete();
            onComplete?.Invoke();
        }

        private IEnumerator RunSequence(
            UITransitionPlayer player,
            UITweenSequence sequence,
            UITweenHandle handle,
            Action onComplete)
        {
            for (var i = 0; i < sequence.GroupCount; i++)
            {
                yield return RunGroup(player, sequence.GetGroup(i), handle);

                if (handle.IsCanceled)
                {
                    yield break;
                }
            }

            handle.Complete();
            onComplete?.Invoke();
        }

        private IEnumerator RunGroup(
            UITransitionPlayer player,
            IReadOnlyList<UITransitionPreset> presets,
            UITweenHandle handle)
        {
            if (presets == null || presets.Count == 0)
            {
                yield break;
            }

            player.BeginGroup();

            for (var i = 0; i < presets.Count; i++)
            {
                player.ApplyFrom(presets[i]);
            }

            var groupDuration = GetGroupDuration(presets);

            if (groupDuration <= 0f)
            {
                for (var i = 0; i < presets.Count; i++)
                {
                    player.ApplyTo(presets[i]);
                }

                yield break;
            }

            var elapsed = 0f;
            while (elapsed < groupDuration)
            {
                if (handle.IsCanceled)
                {
                    yield break;
                }

                for (var i = 0; i < presets.Count; i++)
                {
                    player.ApplyAt(presets[i], elapsed);
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            for (var i = 0; i < presets.Count; i++)
            {
                player.ApplyTo(presets[i]);
            }
        }

        private static float GetGroupDuration(IReadOnlyList<UITransitionPreset> presets)
        {
            var duration = 0f;
            for (var i = 0; i < presets.Count; i++)
            {
                var preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                var candidate = Mathf.Max(0f, preset.Delay) + Mathf.Max(0f, preset.Duration);
                if (candidate > duration)
                {
                    duration = candidate;
                }
            }

            return duration;
        }
    }
}
