using UnityEngine;

namespace Devian
{
    internal static class UIToastDefaults
    {
        public const string DefaultGroupId = "System";
        public const string DefaultFramePrefabName = "ui_toast_frame";
        public const int DefaultMaxVisibleCount = 1;
        public const float DefaultDuration = 2f;
        public const float DefaultSpacing = 8f;

        public static readonly Vector2 DefaultAnchoredOffset = new Vector2(0f, -80f);
    }
}
