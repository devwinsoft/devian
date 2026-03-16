using UnityEngine;

namespace Devian
{
    [CreateAssetMenu(fileName = "SaveDataSettings", menuName = "Devian/MobileSystem/SaveData Settings")]
    public sealed class SaveDataSettings : ScriptableObject
    {
        public const string ResourcesPath = "Devian/SaveDataSettings";
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/SaveDataSettings.asset";

        [Header("Local Storage")]
        [SerializeField] SaveLocalRoot _localRoot = SaveLocalRoot.PersistentData;

        [Header("Primary Save")]
        [SerializeField] string _primaryLocalFilename = "save/main.json";
        [SerializeField] string _primaryCloudSlot = "main";

        public SaveLocalRoot LocalRoot => _localRoot;
        public string PrimaryLocalFilename => _primaryLocalFilename;
        public string PrimaryCloudSlot => _primaryCloudSlot;
    }
}
