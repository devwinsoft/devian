using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public struct UITransitionAlphaClip
    {
        public bool Enabled;
        [Min(0f)] public float StartTime;
        [Min(0f)] public float Duration;
        public UITweenEase Ease;
        public float From;
        public float To;
    }

    [Serializable]
    public struct UITransitionMoveClip
    {
        public bool Enabled;
        [Min(0f)] public float StartTime;
        [Min(0f)] public float Duration;
        public UITweenEase Ease;
        public Vector2 FromOffset;
        public Vector2 ToOffset;
    }

    [Serializable]
    public struct UITransitionScaleClip
    {
        public bool Enabled;
        [Min(0f)] public float StartTime;
        [Min(0f)] public float Duration;
        public UITweenEase Ease;
        public Vector3 From;
        public Vector3 To;
    }

    [Serializable]
    public sealed class UITransitionPreset
    {
        public UITransitionAlphaClip[] AlphaClips = Array.Empty<UITransitionAlphaClip>();
        public UITransitionMoveClip[] MoveClips = Array.Empty<UITransitionMoveClip>();
        public UITransitionScaleClip[] ScaleClips = Array.Empty<UITransitionScaleClip>();
    }
}
