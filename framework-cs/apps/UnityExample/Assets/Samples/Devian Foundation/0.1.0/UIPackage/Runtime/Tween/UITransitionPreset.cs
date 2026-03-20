using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public sealed class UITransitionPreset
    {
        [Min(0f)] public float Duration = 0.2f;
        [Min(0f)] public float Delay = 0f;
        public UITweenEase Ease = UITweenEase.OutQuad;

        public bool UseAlpha = false;
        public float FromAlpha = 0f;
        public float ToAlpha = 1f;

        public bool UseAnchoredPosition = false;
        public Vector2 FromAnchoredPosition = Vector2.zero;
        public Vector2 ToAnchoredPosition = Vector2.zero;

        public bool UseScale = false;
        public Vector3 FromScale = Vector3.one;
        public Vector3 ToScale = Vector3.one;
    }
}
