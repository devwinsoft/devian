// SSOT: skills/devian-unity/50-mobile-system/30-purchase-system/14-purchase-settings/SKILL.md

using UnityEngine;

namespace Devian
{
    [CreateAssetMenu(fileName = "PurchaseSettings", menuName = "Devian/MobileSystem/Purchase Settings")]
    public sealed class PurchaseSettings : ScriptableObject
    {
        public const string ResourcesPath = "Devian/PurchaseSettings";
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/PurchaseSettings.asset";

        [Header("Season Purchase")]
        [SerializeField]
        [Tooltip("Block season purchase N days before season end")]
        int _seasonPurchaseBlockedBeforeEndDays = 3;

        [Header("Recovery")]
        [SerializeField]
        [Tooltip("Max retry count for purchase verification recovery")]
        int _maxVerifyRecoveryRetries = 3;

        [SerializeField]
        [Tooltip("Poll count for already-owned deferred queue recovery (~5s)")]
        int _alreadyOwnedRecoveryPollCount = 50;

        public int SeasonPurchaseBlockedBeforeEndDays => _seasonPurchaseBlockedBeforeEndDays;
        public int MaxVerifyRecoveryRetries => _maxVerifyRecoveryRetries;
        public int AlreadyOwnedRecoveryPollCount => _alreadyOwnedRecoveryPollCount;
    }
}
