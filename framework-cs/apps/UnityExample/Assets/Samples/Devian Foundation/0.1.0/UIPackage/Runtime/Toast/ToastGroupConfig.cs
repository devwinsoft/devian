using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public sealed class ToastGroupConfig
    {
        public string GroupId = UIToastDefaults.DefaultGroupId;
        public UI_TOAST_FRAME_ID ToastFrameId = UIToastDefaults.DefaultFramePrefabName;
        public ToastAnchorPreset AnchorPreset = ToastAnchorPreset.TopCenter;
        public Vector2 AnchoredOffset = UIToastDefaults.DefaultAnchoredOffset;
        public int MaxVisibleCount = UIToastDefaults.DefaultMaxVisibleCount;
        public float DefaultDuration = UIToastDefaults.DefaultDuration;
        public ToastLayoutDirection LayoutDirection = ToastLayoutDirection.Down;
        public ToastDuplicatePolicy DuplicatePolicy = ToastDuplicatePolicy.Allow;
    }
}
