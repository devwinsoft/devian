import { STAT_TYPE, UNIT_HERO, UNIT_HERO_LEVEL } from '../../Generated/Game.g';
import { AbilityItemEquip } from './AbilityItemEquip';
import { AbilityUnitBase } from './AbilityUnitBase';

export class AbilityUnitHero extends AbilityUnitBase {
    private mTable: UNIT_HERO | null = null;
    private mLevelTable: UNIT_HERO_LEVEL | null = null;
    private readonly mEquips: Map<number, AbilityItemEquip> = new Map();

    get unitId(): string { return this.mTable?.UnitId ?? ''; }
    get equips(): ReadonlyMap<number, AbilityItemEquip> { return this.mEquips; }

    init(table: UNIT_HERO, levelTable: UNIT_HERO_LEVEL): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.initUnitState(levelTable.UnitLevel, levelTable.MaxHp);
    }

    equip(equip: AbilityItemEquip, slotNumber: number): boolean {
        if (!equip || slotNumber <= 0)
            return false;

        const prev = this.mEquips.get(slotNumber);
        if (prev) {
            if (this.isSameEquip(prev, equip)) {
                prev.setOwner(this.unitId, slotNumber);
                return true;
            }

            this.unequip(slotNumber);
        }

        const existingSlot = this.findEquipSlot(equip);
        if (existingSlot > 0) {
            if (existingSlot === slotNumber)
                return true;

            this.unequip(existingSlot);
        }

        if (equip.isEquipped)
            equip.clearOwner();

        this.mEquips.set(slotNumber, equip);
        equip.setOwner(this.unitId, slotNumber);
        this.applyEquipStats(equip, +1);
        return true;
    }

    unequip(slotNumber: number): boolean {
        const equip = this.mEquips.get(slotNumber);
        if (!equip)
            return false;

        this.applyEquipStats(equip, -1);

        if (equip.ownerUnitId === this.unitId && equip.ownerSlotNumber === slotNumber)
            equip.clearOwner();

        this.mEquips.delete(slotNumber);
        return true;
    }

    clearProjectedEquips(): void {
        for (const slot of Array.from(this.mEquips.keys()))
            this.unequip(slot);
    }

    setProjectedEquip(equip: AbilityItemEquip, slotNumber: number): boolean {
        return this.equip(equip, slotNumber);
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

    private findEquipSlot(equip: AbilityItemEquip): number {
        for (const [slotNumber, slottedEquip] of this.mEquips) {
            if (this.isSameEquip(slottedEquip, equip))
                return slotNumber;
        }

        return 0;
    }

    private isSameEquip(left: AbilityItemEquip | undefined, right: AbilityItemEquip | undefined): boolean {
        if (left === right)
            return true;

        if (!left || !right)
            return false;

        return left.itemUid.length > 0 && left.itemUid === right.itemUid;
    }

    private shouldApplyEquipStat(statType: STAT_TYPE): boolean {
        return statType !== STAT_TYPE.NONE
            && statType !== STAT_TYPE.ITEM_LEVEL
            && statType !== STAT_TYPE.ITEM_AMOUNT;
    }
}
