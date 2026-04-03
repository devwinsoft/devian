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

        public bool HasPreferredSize;
        public Vector2 PreferredSize;
    }

    public sealed class UICompiledTransitionData
    {
        public float Duration;
        public UITransitionAlphaClip[] AlphaClips = Array.Empty<UITransitionAlphaClip>();
        public UITransitionMoveClip[] MoveClips = Array.Empty<UITransitionMoveClip>();
        public UITransitionScaleClip[] ScaleClips = Array.Empty<UITransitionScaleClip>();
        public UITransitionPreferredSizeClip[] PreferredSizeClips = Array.Empty<UITransitionPreferredSizeClip>();

        public bool IsEmpty =>
            AlphaClips.Length == 0
            && MoveClips.Length == 0
            && ScaleClips.Length == 0
            && PreferredSizeClips.Length == 0;

        public bool UsesAlpha => AlphaClips.Length > 0;
        public bool UsesAnchoredPosition => MoveClips.Length > 0;
        public bool UsesScale => ScaleClips.Length > 0;
        public bool UsesPreferredSize => PreferredSizeClips.Length > 0;

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

            if (TryEvaluatePreferredSize(elapsed, out var preferredSize))
            {
                result.HasPreferredSize = true;
                result.PreferredSize = preferredSize;
            }

            return result;
        }

        private bool TryEvaluateAlpha(float elapsed, out float value)
        {
            var hasActiveValue = false;
            var hasUpcomingValue = false;
            var upcomingStartTime = float.MaxValue;
            value = 0f;

            for (var i = 0; i < AlphaClips.Length; i++)
            {
                var clip = AlphaClips[i];
                if (elapsed < clip.StartTime)
                {
                    if (clip.StartTime <= upcomingStartTime)
                    {
                        upcomingStartTime = clip.StartTime;
                        value = clip.From;
                        hasUpcomingValue = true;
                    }

                    continue;
                }

                value = EvaluateFloat(clip.StartTime, clip.Duration, clip.Ease, clip.From, clip.To, elapsed);
                hasActiveValue = true;
            }

            return hasActiveValue || hasUpcomingValue;
        }

        private bool TryEvaluateMove(float elapsed, out Vector2 value)
        {
            var hasActiveValue = false;
            var hasUpcomingValue = false;
            var upcomingStartTime = float.MaxValue;
            value = Vector2.zero;

            for (var i = 0; i < MoveClips.Length; i++)
            {
                var clip = MoveClips[i];
                if (elapsed < clip.StartTime)
                {
                    if (clip.StartTime <= upcomingStartTime)
                    {
                        upcomingStartTime = clip.StartTime;
                        value = clip.FromOffset;
                        hasUpcomingValue = true;
                    }

                    continue;
                }

                value = Vector2.LerpUnclamped(
                    clip.FromOffset,
                    clip.ToOffset,
                    EvaluateNormalizedTime(clip.StartTime, clip.Duration, clip.Ease, elapsed));
                hasActiveValue = true;
            }

            return hasActiveValue || hasUpcomingValue;
        }

        private bool TryEvaluateScale(float elapsed, out Vector3 value)
        {
            var hasActiveValue = false;
            var hasUpcomingValue = false;
            var upcomingStartTime = float.MaxValue;
            value = Vector3.one;

            for (var i = 0; i < ScaleClips.Length; i++)
            {
                var clip = ScaleClips[i];
                if (elapsed < clip.StartTime)
                {
                    if (clip.StartTime <= upcomingStartTime)
                    {
                        upcomingStartTime = clip.StartTime;
                        value = clip.From;
                        hasUpcomingValue = true;
                    }

                    continue;
                }

                value = Vector3.LerpUnclamped(
                    clip.From,
                    clip.To,
                    EvaluateNormalizedTime(clip.StartTime, clip.Duration, clip.Ease, elapsed));
                hasActiveValue = true;
            }

            return hasActiveValue || hasUpcomingValue;
        }

        private bool TryEvaluatePreferredSize(float elapsed, out Vector2 value)
        {
            var hasActiveValue = false;
            var hasUpcomingValue = false;
            var upcomingStartTime = float.MaxValue;
            value = Vector2.zero;

            for (var i = 0; i < PreferredSizeClips.Length; i++)
            {
                var clip = PreferredSizeClips[i];
                if (elapsed < clip.StartTime)
                {
                    if (clip.StartTime <= upcomingStartTime)
                    {
                        upcomingStartTime = clip.StartTime;
                        value = clip.From;
                        hasUpcomingValue = true;
                    }

                    continue;
                }

                value = Vector2.LerpUnclamped(
                    clip.From,
                    clip.To,
                    EvaluateNormalizedTime(clip.StartTime, clip.Duration, clip.Ease, elapsed));
                hasActiveValue = true;
            }

            return hasActiveValue || hasUpcomingValue;
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
            var preferredSizeClips = new List<UITransitionPreferredSizeClip>();

            AddPreset(alphaClips, moveClips, scaleClips, preferredSizeClips, preset, 0f);

            return CreateCompiledData(alphaClips, moveClips, scaleClips, preferredSizeClips);
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
            var preferredSizeClips = new List<UITransitionPreferredSizeClip>();
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

                    AddPreset(alphaClips, moveClips, scaleClips, preferredSizeClips, preset, cursor);
                    var presetDuration = GetPresetDuration(preset);
                    if (presetDuration > groupDuration)
                    {
                        groupDuration = presetDuration;
                    }
                }

                cursor += groupDuration;
            }

            return CreateCompiledData(alphaClips, moveClips, scaleClips, preferredSizeClips);
        }

        private static void AddPreset(
            List<UITransitionAlphaClip> alphaClips,
            List<UITransitionMoveClip> moveClips,
            List<UITransitionScaleClip> scaleClips,
            List<UITransitionPreferredSizeClip> preferredSizeClips,
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
            AddPreferredSizeClips(preferredSizeClips, preset.PreferredSizeClips, timeOffset);
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
                clip.StartTime += timeOffset;
                destination.Add(clip);
            }
        }

        private static void AddPreferredSizeClips(
            List<UITransitionPreferredSizeClip> destination,
            UITransitionPreferredSizeClip[] source,
            float timeOffset)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var clip = source[i];
                clip.StartTime += timeOffset;
                destination.Add(clip);
            }
        }

        private static UICompiledTransitionData CreateCompiledData(
            List<UITransitionAlphaClip> alphaClips,
            List<UITransitionMoveClip> moveClips,
            List<UITransitionScaleClip> scaleClips,
            List<UITransitionPreferredSizeClip> preferredSizeClips)
        {
            var data = new UICompiledTransitionData
            {
                AlphaClips = alphaClips.ToArray(),
                MoveClips = moveClips.ToArray(),
                ScaleClips = scaleClips.ToArray(),
                PreferredSizeClips = preferredSizeClips.ToArray()
            };

            data.Duration = Mathf.Max(
                GetAlphaDuration(data.AlphaClips),
                GetMoveDuration(data.MoveClips),
                GetScaleDuration(data.ScaleClips),
                GetPreferredSizeDuration(data.PreferredSizeClips));

            return data;
        }

        private static float GetPresetDuration(UITransitionPreset preset)
        {
            return Mathf.Max(
                GetAlphaDuration(preset.AlphaClips),
                GetMoveDuration(preset.MoveClips),
                GetScaleDuration(preset.ScaleClips),
                GetPreferredSizeDuration(preset.PreferredSizeClips));
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
                duration = Mathf.Max(duration, clip.StartTime + Mathf.Max(0f, clip.Duration));
            }

            return duration;
        }

        private static float GetPreferredSizeDuration(UITransitionPreferredSizeClip[] clips)
        {
            var duration = 0f;
            if (clips == null)
            {
                return duration;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                duration = Mathf.Max(duration, clip.StartTime + Mathf.Max(0f, clip.Duration));
            }

            return duration;
        }
    }
}
