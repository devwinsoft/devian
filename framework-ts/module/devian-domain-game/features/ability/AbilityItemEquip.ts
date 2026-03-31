import { ITEM_EQUIP, ITEM_EQUIP_LEVEL } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';

export class AbilityItemEquip extends AbilityItemBase {
    private mTable: ITEM_EQUIP | null = null;
    private mLevelTable: ITEM_EQUIP_LEVEL | null = null;
    private mItemUid: string = '';
    private mOwnerUnitId: string = '';
    private mOwnerSlotNumber: number = 0;

    get itemUid(): string { return this.mItemUid; }
    get itemId(): string { return this.mTable?.ItemId ?? ''; }
    get ownerUnitId(): string { return this.mOwnerUnitId; }
    get ownerSlotNumber(): number { return this.mOwnerSlotNumber; }
    get isEquipped(): boolean { return this.mOwnerSlotNumber > 0; }

    init(table: ITEM_EQUIP, levelTable: ITEM_EQUIP_LEVEL, itemUid: string): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.mItemUid = itemUid;
        this.initLevelStats(
            levelTable.ItemLevel,
            levelTable.StatType00, levelTable.StatValue00,
            levelTable.StatType01, levelTable.StatValue01,
            levelTable.StatType02, levelTable.StatValue02,
            levelTable.StatType03, levelTable.StatValue03,
        );
    }

    setOwner(unitId: string, slotNumber: number): void {
        this.mOwnerUnitId = unitId;
        this.mOwnerSlotNumber = slotNumber;
    }

    clearOwner(): void {
        this.mOwnerUnitId = '';
        this.mOwnerSlotNumber = 0;
    }

    clone(): AbilityItemEquip {
        const c = new AbilityItemEquip();
        c.mTable = this.mTable;
        c.mLevelTable = this.mLevelTable;
        c.mItemUid = this.mItemUid;
        c.mOwnerUnitId = this.mOwnerUnitId;
        c.mOwnerSlotNumber = this.mOwnerSlotNumber;
        c.copyStatsFrom(this);
        return c;
    }
}
