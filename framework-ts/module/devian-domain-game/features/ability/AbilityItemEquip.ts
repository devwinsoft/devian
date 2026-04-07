import { EQUIP_TYPE, ITEM_EQUIP, ITEM_EQUIP_LEVEL, SLOT_TYPE } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';

export class AbilityItemEquip extends AbilityItemBase {
    private mTable: ITEM_EQUIP | null = null;
    private mLevelTable: ITEM_EQUIP_LEVEL | null = null;
    private mItemUid: string = '';
    private mOwnerUnitId: string = '';
    private mOwnerSlotType: SLOT_TYPE = SLOT_TYPE.NONE;

    get itemUid(): string { return this.mItemUid; }
    get itemId(): string { return this.mTable?.item_id ?? ''; }
    get equipType(): EQUIP_TYPE { return this.mTable?.equip_type ?? 0; }
    get ownerUnitId(): string { return this.mOwnerUnitId; }
    get ownerSlotType(): SLOT_TYPE { return this.mOwnerSlotType; }
    get isEquipped(): boolean { return this.mOwnerSlotType !== SLOT_TYPE.NONE; }

    init(table: ITEM_EQUIP, levelTable: ITEM_EQUIP_LEVEL, itemUid: string): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.mItemUid = itemUid;
        this.initLevelStats(
            levelTable.item_level,
            levelTable.stat_type00, levelTable.stat_value00,
            levelTable.stat_type01, levelTable.stat_value01,
            levelTable.stat_type02, levelTable.stat_value02,
            levelTable.stat_type03, levelTable.stat_value03,
        );
    }

    setOwner(unitId: string, slotType: SLOT_TYPE): void {
        this.mOwnerUnitId = unitId;
        this.mOwnerSlotType = slotType;
    }

    clearOwner(): void {
        this.mOwnerUnitId = '';
        this.mOwnerSlotType = SLOT_TYPE.NONE;
    }

    clone(): AbilityItemEquip {
        const c = new AbilityItemEquip();
        c.mTable = this.mTable;
        c.mLevelTable = this.mLevelTable;
        c.mItemUid = this.mItemUid;
        c.mOwnerUnitId = this.mOwnerUnitId;
        c.mOwnerSlotType = this.mOwnerSlotType;
        c.copyStatsFrom(this);
        return c;
    }

    static isSame(left: AbilityItemEquip | null | undefined, right: AbilityItemEquip | null | undefined): boolean {
        if (left === right)
            return true;

        if (!left || !right)
            return false;

        return !!left.itemUid && left.itemUid === right.itemUid;
    }
}
