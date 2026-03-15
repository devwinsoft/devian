using Devian.Domain.Game;
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

        // Editor-only: temporary ID selectors for Add row (cleared on save)
        [HideInInspector, SerializeField] internal ITEM_CARD_ID     _editorCardId     = new();
        [HideInInspector, SerializeField] internal ITEM_EQUIP_ID    _editorEquipId    = new();
        [HideInInspector, SerializeField] internal UNIT_HERO_ID     _editorHeroId     = new();
        [HideInInspector, SerializeField] internal ITEM_RENTAL_ID   _editorRentalId   = new();
        [HideInInspector, SerializeField] internal ITEM_PASS_ID     _editorPassId     = new();
    }
}
