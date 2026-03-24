using Devian.Domain.Common;
using TMPro;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Binds ST_TEXT value to a TMP_Text component.
    /// Applies initial text on UI init and subscribes to ReloadText updates.
    /// </summary>
    public class UIComponentText : UIComponentBase
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private TEXT_ID _textId;

        protected override void onInit(Canvas canvas)
        {
            applyText();
            UIManager.messageSystem.Subcribe(GetEntityId(), UI_MESSAGE.ReloadText, onReloadText);
        }

        protected override void onDestroy()
        {
            UIManager.messageSystem?.UnSubcribe(GetEntityId());
        }

        private bool onReloadText(object[] args)
        {
            applyText();
            return false;
        }

        private void applyText()
        {
            if (_text == null) return;
            if (_textId == null || !_textId.IsValid()) return;

            _text.text = ST_TEXT.Get(_textId.Value);
        }
    }
}
