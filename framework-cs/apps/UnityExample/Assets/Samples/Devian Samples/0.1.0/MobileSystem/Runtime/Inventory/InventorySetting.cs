using UnityEngine;

namespace Devian
{
    [CreateAssetMenu(
        fileName = "InventorySettings",
        menuName = "Devian/MobileSystem/Inventory Settings")]
    public sealed class InventorySetting : ScriptableObject
    {
        // Resources.Load 경로 (정본 SSOT)
        public const string ResourcesPath = "Devian/InventorySettings";

        // 프로젝트 에셋 경로 (정본 SSOT)
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/InventorySettings.asset";

        [SerializeField] public CString InitialInventory = "[{\"type\":\"CURRENCY\",\"id\":\"GOLD\",\"amount\":1000}]";
    }
}
