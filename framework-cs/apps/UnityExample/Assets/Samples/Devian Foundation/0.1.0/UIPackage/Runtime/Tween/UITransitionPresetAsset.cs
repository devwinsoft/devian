using UnityEngine;

namespace Devian
{
    [CreateAssetMenu(
        fileName = "ui_transition_preset",
        menuName = "Devian/UI/Transition Preset")]
    public sealed class UITransitionPresetAsset : ScriptableObject
    {
        [SerializeField] private UITransitionPreset _preset = new UITransitionPreset();

        public UITransitionPreset Preset
        {
            get
            {
                if (_preset == null)
                {
                    _preset = new UITransitionPreset();
                }

                return _preset;
            }
        }

        private void OnValidate()
        {
            if (_preset == null)
            {
                _preset = new UITransitionPreset();
            }
        }
    }
}
