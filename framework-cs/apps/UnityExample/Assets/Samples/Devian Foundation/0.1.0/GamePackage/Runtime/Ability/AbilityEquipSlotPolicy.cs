using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public enum AbilityEquipPlacementFailure
    {
        None = 0,
        SlotNotAllowed = 1,
        HandSubBlockedByTwoHandedMain = 2,
    }

    public static class AbilityEquipSlotPolicy
    {
        public static EQUIP_SLOT GetRule(EQUIP_TYPE equipType)
        {
            return TB_EQUIP_SLOT.Get(equipType);
        }

        public static bool IsAllowed(AbilityItemEquip equip, SLOT_TYPE slotType)
        {
            if (equip == null || slotType == SLOT_TYPE.NONE)
                return false;

            return IsAllowed(GetRule(equip.EquipType), slotType);
        }

        public static bool IsAllowed(EQUIP_SLOT rule, SLOT_TYPE slotType)
        {
            return rule != null
                && slotType != SLOT_TYPE.NONE
                && rule.allowed_slots != null
                && rule.allowed_slots.Contains(slotType);
        }

        public static bool IsTwoHanded(AbilityItemEquip equip)
        {
            return equip != null && IsTwoHanded(GetRule(equip.EquipType));
        }

        public static bool IsTwoHanded(EQUIP_SLOT rule)
        {
            return rule != null && rule.two_handed;
        }

        public static bool HasBlockingTwoHandedMain(IReadOnlyDictionary<SLOT_TYPE, AbilityItemEquip> equips)
        {
            if (equips == null)
                return false;

            return equips.TryGetValue(SLOT_TYPE.HAND_MAIN, out var mainHand)
                && mainHand != null
                && IsTwoHanded(mainHand);
        }

        public static AbilityEquipPlacementFailure GetPlacementFailure(
            AbilityItemEquip equip,
            SLOT_TYPE slotType,
            IReadOnlyDictionary<SLOT_TYPE, AbilityItemEquip> equips)
        {
            if (equip == null)
                return AbilityEquipPlacementFailure.SlotNotAllowed;

            return GetPlacementFailure(GetRule(equip.EquipType), slotType, equips);
        }

        public static AbilityEquipPlacementFailure GetPlacementFailure(
            EQUIP_SLOT rule,
            SLOT_TYPE slotType,
            IReadOnlyDictionary<SLOT_TYPE, AbilityItemEquip> equips)
        {
            if (!IsAllowed(rule, slotType))
                return AbilityEquipPlacementFailure.SlotNotAllowed;

            if (slotType == SLOT_TYPE.HAND_SUB && HasBlockingTwoHandedMain(equips))
                return AbilityEquipPlacementFailure.HandSubBlockedByTwoHandedMain;

            return AbilityEquipPlacementFailure.None;
        }
    }
}
