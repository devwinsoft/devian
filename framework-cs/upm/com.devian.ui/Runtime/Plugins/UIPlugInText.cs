using Devian.Domain.Common;
using TMPro;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Binds ST_TEXT value to a TMP_Text component.
    /// Subscribes to UI_MESSAGE.InitOnce and UI_MESSAGE.ReloadText via UIManager.messageSystem.
    /// </summary>
    public class UIPlugInText : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private TEXT_ID _textId;
        [SerializeField] private EntityId _owner;

        private void OnEnable()
        {
            UIManager.messageSystem.SubcribeOnce(_owner, UI_MESSAGE.InitOnce, onInitOnce);
            UIManager.messageSystem.Subcribe(_owner, UI_MESSAGE.ReloadText, onReloadText);
        }

        private void OnDisable()
        {
            UIManager.messageSystem.UnSubcribe(_owner);
        }

        private void onInitOnce(object[] args)
        {
            applyText();
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
