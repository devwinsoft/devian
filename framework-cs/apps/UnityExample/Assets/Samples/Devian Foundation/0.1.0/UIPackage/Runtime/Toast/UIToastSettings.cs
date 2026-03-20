using UnityEngine;

namespace Devian
{
    [CreateAssetMenu(fileName = "UIToastSettings", menuName = "Devian/UIPackage/UIToast Settings")]
    public sealed class UIToastSettings : ScriptableObject
    {
        public const string ResourcesPath = "Devian/UIToastSettings";
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/UIToastSettings.asset";

        [SerializeField] private ToastGroupConfig[] _groupConfigs;

        public ToastGroupConfig[] GroupConfigs => _groupConfigs;
    }
}
