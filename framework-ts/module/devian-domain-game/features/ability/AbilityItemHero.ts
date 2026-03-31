import { ITEM_HERO, ITEM_HERO_LEVEL, STAT_TYPE } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';
import { AbilityItemEquip } from './AbilityItemEquip';

export class AbilityItemHero extends AbilityItemBase {
    private mTable: ITEM_HERO | null = null;
    private mLevelTable: ITEM_HERO_LEVEL | null = null;
    private readonly mEquips: Map<number, AbilityItemEquip> = new Map();

    get heroId(): string { return this.mTable?.ItemId ?? ''; }
    get itemId(): string { return this.mTable?.ItemId ?? ''; }
    get equips(): ReadonlyMap<number, AbilityItemEquip> { return this.mEquips; }

    init(table: ITEM_HERO, levelTable: ITEM_HERO_LEVEL): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.initLevelStats(
            levelTable.ItemLevel,
            levelTable.StatType00, levelTable.StatValue00,
            levelTable.StatType01, levelTable.StatValue01,
            levelTable.StatType02, levelTable.StatValue02,
            levelTable.StatType03, levelTable.StatValue03,
        );
    }

    equip(equip: AbilityItemEquip, slotNumber: number): boolean {
        if (!equip || slotNumber <= 0) return false;

        if (equip.isEquipped) {
            if (equip.ownerUnitId !== this.heroId)
                return false;

            const current = this.mEquips.get(equip.ownerSlotNumber);
            if (current && this.isSameEquip(current, equip)) {
                if (equip.ownerSlotNumber === slotNumber)
                    return true;

                if (!this.unequip(equip.ownerSlotNumber))
                    return false;
            }
            else {
                equip.clearOwner();
            }
        }

        const prev = this.mEquips.get(slotNumber);
        if (prev) {
            if (this.isSameEquip(prev, equip)) {
                prev.setOwner(this.heroId, slotNumber);
                return true;
            }

            if (!this.unequip(slotNumber))
                return false;
        }

        const existingSlot = this.findEquipSlot(equip);
        if (existingSlot > 0) {
            if (existingSlot === slotNumber)
                return true;

            if (!this.unequip(existingSlot))
                return false;
        }

        this.mEquips.set(slotNumber, equip);
        equip.setOwner(this.heroId, slotNumber);
        this.applyEquipStats(equip, +1);
        return true;
    }

    unequip(slotNumber: number): boolean {
        const equip = this.mEquips.get(slotNumber);
        if (!equip) return false;

        this.applyEquipStats(equip, -1);

        if (equip.ownerUnitId === this.heroId && equip.ownerSlotNumber === slotNumber)
            equip.clearOwner();

        this.mEquips.delete(slotNumber);
        return true;
    }

    clone(): AbilityItemHero {
        const c = new AbilityItemHero();
        c.mTable = this.mTable;
        c.mLevelTable = this.mLevelTable;
        c.copyStatsFrom(this);
        for (const [slot, equip] of this.mEquips)
            c.mEquips.set(slot, equip);
        return c;
    }

    private applyEquipStats(equip: AbilityItemEquip, sign: number): void {
        if (!equip || sign === 0)
            return;

        for (const [statType, statValue] of equip.getStats()) {
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
