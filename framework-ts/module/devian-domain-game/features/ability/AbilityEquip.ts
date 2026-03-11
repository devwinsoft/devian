import { ITEM_EQUIP } from '../../Generated/Game.g';
import { AbilityBase } from './AbilityBase';

export class AbilityEquip extends AbilityBase {
    private mTable: ITEM_EQUIP | null = null;
    private mItemUid: string = '';
    private mOwnerUnitId: string = '';
    private mOwnerSlotNumber: number = 0;

    get itemUid(): string { return this.mItemUid; }
    get equipId(): string { return this.mTable?.EquipId ?? ''; }
    get ownerUnitId(): string { return this.mOwnerUnitId; }
    get ownerSlotNumber(): number { return this.mOwnerSlotNumber; }
    get isEquipped(): boolean { return this.mOwnerSlotNumber > 0; }

    init(table: ITEM_EQUIP, itemUid: string): void {
        this.mTable = table;
        this.mItemUid = itemUid;
    }

    setOwner(unitId: string, slotNumber: number): void {
        this.mOwnerUnitId = unitId;
        this.mOwnerSlotNumber = slotNumber;
    }

    clearOwner(): void {
        this.mOwnerUnitId = '';
        this.mOwnerSlotNumber = 0;
    }

    clone(): AbilityEquip {
        const c = new AbilityEquip();
        c.mTable = this.mTable;
        c.mItemUid = this.mItemUid;
        c.mOwnerUnitId = this.mOwnerUnitId;
        c.mOwnerSlotNumber = this.mOwnerSlotNumber;
        c.copyStatsFrom(this);
        return c;
    }
}
