using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public sealed class PopupConfig
    {
        public string PopupId = UIPopupDefaults.DefaultPopupId;
        public UI_POPUP_FRAME_ID PopupFrameId = string.Empty;
        public bool UseDim = UIPopupDefaults.DefaultUseDim;
        public bool BlockInputBehind = UIPopupDefaults.DefaultBlockInputBehind;
        public bool CloseOnBack = UIPopupDefaults.DefaultCloseOnBack;
        public bool CloseOnEscape = UIPopupDefaults.DefaultCloseOnEscape;
        public bool CloseOnDimClick = UIPopupDefaults.DefaultCloseOnDimClick;
        public PopupDuplicatePolicy DuplicatePolicy = UIPopupDefaults.DefaultDuplicatePolicy;
        public bool PlayOpenTransition = UIPopupDefaults.DefaultPlayOpenTransition;
        public bool PlayCloseTransition = UIPopupDefaults.DefaultPlayCloseTransition;
    }
}
