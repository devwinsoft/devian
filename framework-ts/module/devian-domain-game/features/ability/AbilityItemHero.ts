import { ITEM_HERO, ITEM_HERO_LEVEL } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';
import { AbilityItemEquip } from './AbilityItemEquip';

export class AbilityItemHero extends AbilityItemBase {
    private mTable: ITEM_HERO | null = null;
    private mLevelTable: ITEM_HERO_LEVEL | null = null;
    private readonly mEquips: Map<number, AbilityItemEquip> = new Map();

    get unitId(): string { return this.mTable?.unit_id ?? ''; }
    get itemId(): string { return this.mTable?.item_id ?? ''; }
    get equips(): ReadonlyMap<number, AbilityItemEquip> { return this.mEquips; }

    init(table: ITEM_HERO, levelTable: ITEM_HERO_LEVEL): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.initLevelStats(
            levelTable.item_level,
            levelTable.stat_type00, levelTable.stat_value00,
            levelTable.stat_type01, levelTable.stat_value01,
            levelTable.stat_type02, levelTable.stat_value02,
            levelTable.stat_type03, levelTable.stat_value03,
        );
    }

    setEquip(equip: AbilityItemEquip, slotNumber: number): boolean {
        if (!equip || slotNumber <= 0) return false;

        const prev = this.mEquips.get(slotNumber);
        if (prev) {
            if (this.isSameEquip(prev, equip)) {
                prev.setOwner(this.itemId, slotNumber);
                return true;
            }

            if (prev.ownerUnitId === this.itemId && prev.ownerSlotNumber === slotNumber)
                prev.clearOwner();
        }

        const existingSlot = this.findEquipSlot(equip);
        if (existingSlot > 0) {
            if (existingSlot === slotNumber)
                return true;

            this.mEquips.delete(existingSlot);
        }

        this.mEquips.set(slotNumber, equip);
        equip.setOwner(this.itemId, slotNumber);
        return true;
    }

    removeEquip(slotNumber: number): boolean {
        const equip = this.mEquips.get(slotNumber);
        if (!equip) return false;

        if (equip.ownerUnitId === this.itemId && equip.ownerSlotNumber === slotNumber)
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
}
