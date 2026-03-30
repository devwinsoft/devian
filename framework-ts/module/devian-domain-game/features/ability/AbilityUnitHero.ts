import { STAT_TYPE, UNIT_HERO } from '../../Generated/Game.g';
import { AbilityItemEquip } from './AbilityItemEquip';
import { AbilityUnitBase } from './AbilityUnitBase';

export class AbilityUnitHero extends AbilityUnitBase {
    private mTable: UNIT_HERO | null = null;
    private readonly mEquips: Map<number, AbilityItemEquip> = new Map();

    get unitId(): string { return this.mTable?.UnitId ?? ''; }
    get equips(): ReadonlyMap<number, AbilityItemEquip> { return this.mEquips; }

    init(table: UNIT_HERO): void {
        this.mTable = table;
        this.addStat(STAT_TYPE.UNIT_HP_MAX, table.MaxHp);
    }

    clearProjectedEquips(): void {
        for (const [slot, equip] of this.mEquips) {
            if (equip.ownerUnitId === this.unitId && equip.ownerSlotNumber === slot)
                equip.clearOwner();
        }
        this.mEquips.clear();
    }

    setProjectedEquip(equip: AbilityItemEquip, slotNumber: number): boolean {
        if (!equip || slotNumber <= 0) return false;
        if (equip.isEquipped) equip.clearOwner();
        const prev = this.mEquips.get(slotNumber);
        if (prev) prev.clearOwner();
        this.mEquips.set(slotNumber, equip);
        equip.setOwner(this.unitId, slotNumber);
        return true;
    }

    clone(): AbilityUnitHero {
        const c = new AbilityUnitHero();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        for (const [slot, equip] of this.mEquips)
            c.mEquips.set(slot, equip);
        return c;
    }
}
