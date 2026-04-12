using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    [CreateAssetMenu(
        fileName = "FirstRewardSettings",
        menuName = "Devian/MobilePackage/First Reward Settings")]
    public sealed class FirstRewardSettings : ScriptableObject
    {
        // Resources.Load 경로 (정본 SSOT)
        public const string ResourcesPath = "Devian/FirstRewardSettings";

        // 프로젝트 에셋 경로 (정본 SSOT)
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/FirstRewardSettings.asset";

        [SerializeField] public CString InitialRewards = "[{\"type\":\"CURRENCY\",\"id\":\"GOLD\",\"amount\":1000}]";
        [SerializeField] public UNIT_HERO_ID SelectedHeroUnitId = new();

        // Editor-only: temporary ID selectors for Add row (cleared on save)
        [HideInInspector, SerializeField] internal ITEM_CARD_ID     _editorCardId     = new();
        [HideInInspector, SerializeField] internal ITEM_MATERIAL_ID _editorMaterialId = new();
        [HideInInspector, SerializeField] internal ITEM_EQUIP_ID    _editorEquipId    = new();
        [HideInInspector, SerializeField] internal ITEM_HERO_ID     _editorHeroId     = new();
        [HideInInspector, SerializeField] internal ITEM_RENTAL_ID   _editorRentalId   = new();
        [HideInInspector, SerializeField] internal ITEM_PASS_ID     _editorPassId     = new();
    }
}
