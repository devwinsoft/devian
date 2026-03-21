using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public struct UITransitionSnapshot
    {
        public float BaseAlpha;
        public Vector2 BaseAnchoredPosition;
        public Vector3 BaseScale;
    }

    public struct UITransitionFrameResult
    {
        public bool HasAlpha;
        public float Alpha;

        public bool HasAnchoredPosition;
        public Vector2 AnchoredPosition;

        public bool HasScale;
        public Vector3 Scale;
    }

    public sealed class UICompiledTransitionData
    {
        public float Duration;
        public UITransitionAlphaClip[] AlphaClips = Array.Empty<UITransitionAlphaClip>();
        public UITransitionMoveClip[] MoveClips = Array.Empty<UITransitionMoveClip>();
        public UITransitionScaleClip[] ScaleClips = Array.Empty<UITransitionScaleClip>();

        public bool IsEmpty =>
            AlphaClips.Length == 0
            && MoveClips.Length == 0
            && ScaleClips.Length == 0;

        public bool UsesAlpha => AlphaClips.Length > 0;
        public bool UsesAnchoredPosition => MoveClips.Length > 0;
        public bool UsesScale => ScaleClips.Length > 0;

        public UITransitionFrameResult Evaluate(float elapsed, UITransitionSnapshot snapshot)
        {
            var result = new UITransitionFrameResult();

            if (TryEvaluateAlpha(elapsed, out var alpha))
            {
                result.HasAlpha = true;
                result.Alpha = alpha;
            }

            if (TryEvaluateMove(elapsed, out var moveOffset))
            {
                result.HasAnchoredPosition = true;
                result.AnchoredPosition = snapshot.BaseAnchoredPosition + moveOffset;
            }

            if (TryEvaluateScale(elapsed, out var scale))
            {
                result.HasScale = true;
                result.Scale = scale;
            }

            return result;
        }

        private bool TryEvaluateAlpha(float elapsed, out float value)
        {
            var hasValue = false;
            value = 0f;

            for (var i = 0; i < AlphaClips.Length; i++)
            {
                var clip = AlphaClips[i];
                if (!clip.Enabled || elapsed < clip.StartTime)
                {
                    continue;
                }

                value = EvaluateFloat(clip.StartTime, clip.Duration, clip.Ease, clip.From, clip.To, elapsed);
                hasValue = true;
            }

            return hasValue;
        }

        private bool TryEvaluateMove(float elapsed, out Vector2 value)
        {
            var hasValue = false;
            value = Vector2.zero;

            for (var i = 0; i < MoveClips.Length; i++)
            {
                var clip = MoveClips[i];
                if (!clip.Enabled || elapsed < clip.StartTime)
                {
                    continue;
                }

                value = Vector2.LerpUnclamped(
                    clip.FromOffset,
                    clip.ToOffset,
                    EvaluateNormalizedTime(clip.StartTime, clip.Duration, clip.Ease, elapsed));
                hasValue = true;
            }

            return hasValue;
        }

        private bool TryEvaluateScale(float elapsed, out Vector3 value)
        {
            var hasValue = false;
            value = Vector3.one;

            for (var i = 0; i < ScaleClips.Length; i++)
            {
                var clip = ScaleClips[i];
                if (!clip.Enabled || elapsed < clip.StartTime)
                {
                    continue;
                }

                value = Vector3.LerpUnclamped(
                    clip.From,
                    clip.To,
                    EvaluateNormalizedTime(clip.StartTime, clip.Duration, clip.Ease, elapsed));
                hasValue = true;
            }

            return hasValue;
        }

        private static float EvaluateFloat(
            float startTime,
            float duration,
            UITweenEase ease,
            float from,
            float to,
            float elapsed)
        {
            return Mathf.LerpUnclamped(from, to, EvaluateNormalizedTime(startTime, duration, ease, elapsed));
        }

        private static float EvaluateNormalizedTime(
            float startTime,
            float duration,
            UITweenEase ease,
            float elapsed)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            var raw = Mathf.Clamp01((elapsed - startTime) / duration);
            return UITweenEaseUtil.Evaluate(ease, raw);
        }
    }

    internal static class UITransitionCompiler
    {
        public static UICompiledTransitionData Compile(UITransitionPreset preset)
        {
            if (preset == null)
            {
                return null;
            }

            var alphaClips = new List<UITransitionAlphaClip>();
            var moveClips = new List<UITransitionMoveClip>();
            var scaleClips = new List<UITransitionScaleClip>();

            AddPreset(alphaClips, moveClips, scaleClips, preset, 0f);

            return CreateCompiledData(alphaClips, moveClips, scaleClips);
        }

        public static UICompiledTransitionData Compile(UITweenSequence sequence)
        {
            if (sequence == null || sequence.IsEmpty)
            {
                return null;
            }

            var alphaClips = new List<UITransitionAlphaClip>();
            var moveClips = new List<UITransitionMoveClip>();
            var scaleClips = new List<UITransitionScaleClip>();
            var cursor = 0f;

            for (var i = 0; i < sequence.GroupCount; i++)
            {
                var group = sequence.GetGroup(i);
                var groupDuration = 0f;

                for (var j = 0; j < group.Count; j++)
                {
                    var preset = group[j];
                    if (preset == null)
                    {
                        continue;
                    }

                    AddPreset(alphaClips, moveClips, scaleClips, preset, cursor);
                    var presetDuration = GetPresetDuration(preset);
                    if (presetDuration > groupDuration)
                    {
                        groupDuration = presetDuration;
                    }
                }

                cursor += groupDuration;
            }

            return CreateCompiledData(alphaClips, moveClips, scaleClips);
        }

        private static void AddPreset(
            List<UITransitionAlphaClip> alphaClips,
            List<UITransitionMoveClip> moveClips,
            List<UITransitionScaleClip> scaleClips,
            UITransitionPreset preset,
            float timeOffset)
        {
            if (preset == null)
            {
                return;
            }

            AddAlphaClips(alphaClips, preset.AlphaClips, timeOffset);
            AddMoveClips(moveClips, preset.MoveClips, timeOffset);
            AddScaleClips(scaleClips, preset.ScaleClips, timeOffset);
        }

        private static void AddAlphaClips(
            List<UITransitionAlphaClip> destination,
            UITransitionAlphaClip[] source,
            float timeOffset)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var clip = source[i];
                if (!clip.Enabled)
                {
                    continue;
                }

                clip.StartTime += timeOffset;
                destination.Add(clip);
            }
        }

        private static void AddMoveClips(
            List<UITransitionMoveClip> destination,
            UITransitionMoveClip[] source,
            float timeOffset)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var clip = source[i];
                if (!clip.Enabled)
                {
                    continue;
                }

                clip.StartTime += timeOffset;
                destination.Add(clip);
            }
        }

        private static void AddScaleClips(
            List<UITransitionScaleClip> destination,
            UITransitionScaleClip[] source,
            float timeOffset)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var clip = source[i];
                if (!clip.Enabled)
                {
                    continue;
                }

                clip.StartTime += timeOffset;
                destination.Add(clip);
            }
        }

        private static UICompiledTransitionData CreateCompiledData(
            List<UITransitionAlphaClip> alphaClips,
            List<UITransitionMoveClip> moveClips,
            List<UITransitionScaleClip> scaleClips)
        {
            var data = new UICompiledTransitionData
            {
                AlphaClips = alphaClips.ToArray(),
                MoveClips = moveClips.ToArray(),
                ScaleClips = scaleClips.ToArray()
            };

            data.Duration = Mathf.Max(
                GetAlphaDuration(data.AlphaClips),
                GetMoveDuration(data.MoveClips),
                GetScaleDuration(data.ScaleClips));

            return data;
        }

        private static float GetPresetDuration(UITransitionPreset preset)
        {
            return Mathf.Max(
                GetAlphaDuration(preset.AlphaClips),
                GetMoveDuration(preset.MoveClips),
                GetScaleDuration(preset.ScaleClips));
        }

        private static float GetAlphaDuration(UITransitionAlphaClip[] clips)
        {
            var duration = 0f;
            if (clips == null)
            {
                return duration;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (!clip.Enabled)
                {
                    continue;
                }

                duration = Mathf.Max(duration, clip.StartTime + Mathf.Max(0f, clip.Duration));
            }

            return duration;
        }

        private static float GetMoveDuration(UITransitionMoveClip[] clips)
        {
            var duration = 0f;
            if (clips == null)
            {
                return duration;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (!clip.Enabled)
                {
                    continue;
                }

                duration = Mathf.Max(duration, clip.StartTime + Mathf.Max(0f, clip.Duration));
            }

            return duration;
        }

        private static float GetScaleDuration(UITransitionScaleClip[] clips)
        {
            var duration = 0f;
            if (clips == null)
            {
                return duration;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (!clip.Enabled)
                {
                    continue;
                }

                duration = Mathf.Max(duration, clip.StartTime + Mathf.Max(0f, clip.Duration));
            }

            return duration;
        }
    }
}
