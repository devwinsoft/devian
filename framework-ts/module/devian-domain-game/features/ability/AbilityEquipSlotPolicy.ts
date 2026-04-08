import { EQUIP_SLOT, EQUIP_TYPE, EQUIP_SLOT_TYPE, TB_EQUIP_SLOT } from '../../Generated/Game.g';
import { AbilityItemEquip } from './AbilityItemEquip';

export enum AbilityEquipPlacementFailure {
    None = 'none',
    SlotNotAllowed = 'slot_not_allowed',
    HandSubBlockedByTwoHandedMain = 'hand_sub_blocked_by_two_handed_main',
}

export class AbilityEquipSlotPolicy {
    static getRule(equipType: EQUIP_TYPE): EQUIP_SLOT | undefined {
        return TB_EQUIP_SLOT.get(equipType);
    }

    static isAllowed(equip: AbilityItemEquip | null | undefined, slotType: EQUIP_SLOT_TYPE): boolean {
        if (!equip || slotType === EQUIP_SLOT_TYPE.NONE)
            return false;

        return this.isAllowedByRule(this.getRule(equip.equipType), slotType);
    }

    static isAllowedByRule(rule: EQUIP_SLOT | null | undefined, slotType: EQUIP_SLOT_TYPE): boolean {
        return !!rule && slotType !== EQUIP_SLOT_TYPE.NONE && rule.allowed_slots.includes(slotType);
    }

    static isTwoHanded(equip: AbilityItemEquip | null | undefined): boolean {
        return !!equip && this.isTwoHandedByRule(this.getRule(equip.equipType));
    }

    static isTwoHandedByRule(rule: EQUIP_SLOT | null | undefined): boolean {
        return !!rule && rule.two_handed;
    }

    static hasBlockingTwoHandedMain(equips: ReadonlyMap<EQUIP_SLOT_TYPE, AbilityItemEquip>): boolean {
        const mainHand = equips.get(EQUIP_SLOT_TYPE.HAND_MAIN);
        return !!mainHand && this.isTwoHanded(mainHand);
    }

    static getPlacementFailure(
        equip: AbilityItemEquip | null | undefined,
        slotType: EQUIP_SLOT_TYPE,
        equips: ReadonlyMap<EQUIP_SLOT_TYPE, AbilityItemEquip>,
    ): AbilityEquipPlacementFailure {
        if (!equip)
            return AbilityEquipPlacementFailure.SlotNotAllowed;

        return this.getPlacementFailureByRule(this.getRule(equip.equipType), slotType, equips);
    }

    static getPlacementFailureByRule(
        rule: EQUIP_SLOT | null | undefined,
        slotType: EQUIP_SLOT_TYPE,
        equips: ReadonlyMap<EQUIP_SLOT_TYPE, AbilityItemEquip>,
    ): AbilityEquipPlacementFailure {
        if (!this.isAllowedByRule(rule, slotType))
            return AbilityEquipPlacementFailure.SlotNotAllowed;

        if (slotType === EQUIP_SLOT_TYPE.HAND_SUB && this.hasBlockingTwoHandedMain(equips))
            return AbilityEquipPlacementFailure.HandSubBlockedByTwoHandedMain;

        return AbilityEquipPlacementFailure.None;
    }
}
