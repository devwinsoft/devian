import { SLOT_TYPE, STAT_TYPE, UNIT_HERO, UNIT_HERO_LEVEL } from '../../Generated/Game.g';
import { AbilityItemEquip } from './AbilityItemEquip';
import { AbilityEquipPlacementFailure, AbilityEquipSlotPolicy } from './AbilityEquipSlotPolicy';
import { AbilityUnitBase } from './AbilityUnitBase';

export class AbilityUnitHero extends AbilityUnitBase {
    private mTable: UNIT_HERO | null = null;
    private mLevelTable: UNIT_HERO_LEVEL | null = null;
    private readonly mEquips: Map<SLOT_TYPE, AbilityItemEquip> = new Map();

    get unitId(): string { return this.mTable?.unit_id ?? ''; }
    get equips(): ReadonlyMap<SLOT_TYPE, AbilityItemEquip> { return this.mEquips; }

    init(table: UNIT_HERO, levelTable: UNIT_HERO_LEVEL): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.initUnitState(levelTable.unit_level, levelTable.max_hp);
    }

    equip(equip: AbilityItemEquip, slotType: SLOT_TYPE): boolean {
        if (!equip || slotType === SLOT_TYPE.NONE)
            return false;

        if (AbilityEquipSlotPolicy.getPlacementFailure(equip, slotType, this.mEquips) !== AbilityEquipPlacementFailure.None)
            return false;

        if (slotType === SLOT_TYPE.HAND_MAIN && AbilityEquipSlotPolicy.isTwoHanded(equip))
            this.unequip(SLOT_TYPE.HAND_SUB);

        const prev = this.mEquips.get(slotType);
        if (prev) {
            if (AbilityItemEquip.isSame(prev, equip)) {
                prev.setOwner(this.unitId, slotType);
                return true;
            }

            this.unequip(slotType);
        }

        const existingSlot = this.findEquipSlot(equip);
        if (existingSlot !== SLOT_TYPE.NONE) {
            if (existingSlot === slotType)
                return true;

            this.unequip(existingSlot);
        }

        if (equip.isEquipped)
            equip.clearOwner();

        this.mEquips.set(slotType, equip);
        equip.setOwner(this.unitId, slotType);
        this.applyEquipStats(equip, +1);
        return true;
    }

    unequip(slotType: SLOT_TYPE): boolean {
        const equip = this.mEquips.get(slotType);
        if (!equip)
            return false;

        this.applyEquipStats(equip, -1);

        if (equip.ownerUnitId === this.unitId && equip.ownerSlotType === slotType)
            equip.clearOwner();

        this.mEquips.delete(slotType);
        return true;
    }

    clone(): AbilityUnitHero {
        const c = new AbilityUnitHero();
        c.mTable = this.mTable;
        c.mLevelTable = this.mLevelTable;
        c.copyStatsFrom(this);
        c.copyUnitStateFrom(this);
        for (const [slot, equip] of this.mEquips)
            c.mEquips.set(slot, equip);
        return c;
    }

    private applyEquipStats(equip: AbilityItemEquip, sign: number): void {
        if (!equip || sign === 0)
            return;

        for (const [statType, statValue] of equip.stats) {
            if (!this.shouldApplyEquipStat(statType))
                continue;

            this.addStat(statType, statValue * sign);
        }
    }

    private findEquipSlot(equip: AbilityItemEquip): SLOT_TYPE {
        for (const [slotType, slottedEquip] of this.mEquips) {
            if (AbilityItemEquip.isSame(slottedEquip, equip))
                return slotType;
        }

        return SLOT_TYPE.NONE;
    }

    private shouldApplyEquipStat(statType: STAT_TYPE): boolean {
        return statType !== STAT_TYPE.NONE
            && statType !== STAT_TYPE.ITEM_LEVEL
            && statType !== STAT_TYPE.ITEM_AMOUNT;
    }
}
