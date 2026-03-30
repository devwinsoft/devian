import { ITEM_HERO } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';
import { AbilityItemEquip } from './AbilityItemEquip';

export class AbilityItemHero extends AbilityItemBase {
    private mTable: ITEM_HERO | null = null;
    private readonly mEquips: Map<number, AbilityItemEquip> = new Map();

    get heroId(): string { return this.mTable?.ItemId ?? ''; }
    get itemId(): string { return this.mTable?.ItemId ?? ''; }
    get equips(): ReadonlyMap<number, AbilityItemEquip> { return this.mEquips; }

    init(table: ITEM_HERO): void {
        this.mTable = table;
    }

    equip(equip: AbilityItemEquip, slotNumber: number): boolean {
        if (!equip || slotNumber <= 0) return false;
        if (equip.isEquipped) equip.clearOwner();
        const prev = this.mEquips.get(slotNumber);
        if (prev) prev.clearOwner();
        this.mEquips.set(slotNumber, equip);
        equip.setOwner(this.heroId, slotNumber);
        return true;
    }

    unequip(slotNumber: number): boolean {
        const equip = this.mEquips.get(slotNumber);
        if (!equip) return false;
        equip.clearOwner();
        this.mEquips.delete(slotNumber);
        return true;
    }

    clone(): AbilityItemHero {
        const c = new AbilityItemHero();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        for (const [slot, equip] of this.mEquips)
            c.mEquips.set(slot, equip);
        return c;
    }
}
