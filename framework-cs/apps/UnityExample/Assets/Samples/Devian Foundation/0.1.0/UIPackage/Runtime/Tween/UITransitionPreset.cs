using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public struct UITransitionAlphaClip
    {
        [Min(0f)] public float StartTime;
        [Min(0f)] public float Duration;
        public UITweenEase Ease;
        public float From;
        public float To;
    }

    [Serializable]
    public struct UITransitionMoveClip
    {
        [Min(0f)] public float StartTime;
        [Min(0f)] public float Duration;
        public UITweenEase Ease;
        public Vector2 FromOffset;
        public Vector2 ToOffset;
    }

    [Serializable]
    public struct UITransitionScaleClip
    {
        [Min(0f)] public float StartTime;
        [Min(0f)] public float Duration;
        public UITweenEase Ease;
        public Vector3 From;
        public Vector3 To;
    }

    [Serializable]
    public struct UITransitionPreferredSizeClip
    {
        [Min(0f)] public float StartTime;
        [Min(0f)] public float Duration;
        public UITweenEase Ease;
        public Vector2 From;
        public Vector2 To;
    }

    [Serializable]
    public sealed class UITransitionPreset
    {
        public UITransitionAlphaClip[] AlphaClips = Array.Empty<UITransitionAlphaClip>();
        public UITransitionMoveClip[] MoveClips = Array.Empty<UITransitionMoveClip>();
        public UITransitionScaleClip[] ScaleClips = Array.Empty<UITransitionScaleClip>();
        public UITransitionPreferredSizeClip[] PreferredSizeClips = Array.Empty<UITransitionPreferredSizeClip>();
    }
}
