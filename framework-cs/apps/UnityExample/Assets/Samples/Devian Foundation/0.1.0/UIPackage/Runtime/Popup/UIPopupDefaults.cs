using UnityEngine;

namespace Devian
{
    internal static class UIPopupDefaults
    {
        public static readonly Color DefaultDimColor = Color.black;
        public const float DefaultDimAlpha = 0.75f;
        public const bool DefaultUseDim = true;
        public const bool DefaultBlockInputBehind = true;
        public const bool DefaultCloseOnBack = true;
        public const bool DefaultCloseOnEscape = true;
        public const bool DefaultCloseOnDimClick = false;
        public const PopupDuplicatePolicy DefaultDuplicatePolicy = PopupDuplicatePolicy.Allow;
        public const bool DefaultPlayOpenTransition = true;
        public const bool DefaultPlayCloseTransition = true;
    }
}
