import { ITEM_HERO, ITEM_HERO_LEVEL, EQUIP_SLOT_TYPE } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';
import { AbilityItemEquip } from './AbilityItemEquip';
import { AbilityEquipPlacementFailure, AbilityEquipSlotPolicy } from './AbilityEquipSlotPolicy';

export class AbilityItemHero extends AbilityItemBase {
    private mTable: ITEM_HERO | null = null;
    private mLevelTable: ITEM_HERO_LEVEL | null = null;
    private readonly mEquips: Map<EQUIP_SLOT_TYPE, AbilityItemEquip> = new Map();

    get unitId(): string { return this.mTable?.unit_id ?? ''; }
    get itemId(): string { return this.mTable?.item_id ?? ''; }
    get equips(): ReadonlyMap<EQUIP_SLOT_TYPE, AbilityItemEquip> { return this.mEquips; }

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

    getEquip(slotType: EQUIP_SLOT_TYPE): AbilityItemEquip | undefined {
        return this.mEquips.get(slotType);
    }

    setEquip(equip: AbilityItemEquip, slotType: EQUIP_SLOT_TYPE): boolean {
        if (!equip || slotType === EQUIP_SLOT_TYPE.NONE) return false;
        if (AbilityEquipSlotPolicy.getPlacementFailure(equip, slotType, this.mEquips) !== AbilityEquipPlacementFailure.None)
            return false;
        if (slotType === EQUIP_SLOT_TYPE.HAND_MAIN && AbilityEquipSlotPolicy.isTwoHanded(equip))
            this.removeEquip(EQUIP_SLOT_TYPE.HAND_SUB);

        const prev = this.mEquips.get(slotType);
        if (prev) {
            if (AbilityItemEquip.isSame(prev, equip)) {
                prev.setOwner(this.itemId, slotType);
                return true;
            }

            if (prev.ownerUnitId === this.itemId && prev.ownerSlotType === slotType)
                prev.clearOwner();
        }

        const existingSlot = this.findEquipSlot(equip);
        if (existingSlot !== EQUIP_SLOT_TYPE.NONE) {
            if (existingSlot === slotType)
                return true;

            this.mEquips.delete(existingSlot);
        }

        this.mEquips.set(slotType, equip);
        equip.setOwner(this.itemId, slotType);
        return true;
    }

    removeEquip(slotType: EQUIP_SLOT_TYPE): boolean {
        const equip = this.mEquips.get(slotType);
        if (!equip) return false;

        if (equip.ownerUnitId === this.itemId && equip.ownerSlotType === slotType)
            equip.clearOwner();

        this.mEquips.delete(slotType);
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

    private findEquipSlot(equip: AbilityItemEquip): EQUIP_SLOT_TYPE {
        for (const [slotType, slottedEquip] of this.mEquips) {
            if (AbilityItemEquip.isSame(slottedEquip, equip))
                return slotType;
        }

        return EQUIP_SLOT_TYPE.NONE;
    }
}
