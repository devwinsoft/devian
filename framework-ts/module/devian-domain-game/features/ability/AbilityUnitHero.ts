import { STAT_TYPE, UNIT_HERO } from '../../Generated/Game.g';
import { AbilityEquip } from './AbilityEquip';
import { AbilityUnitBase } from './AbilityUnitBase';

export class AbilityUnitHero extends AbilityUnitBase {
    private mTable: UNIT_HERO | null = null;
    private readonly mEquips: Map<number, AbilityEquip> = new Map();

    get unitId(): string { return this.mTable?.UnitId ?? ''; }
    get equips(): ReadonlyMap<number, AbilityEquip> { return this.mEquips; }

    init(table: UNIT_HERO): void {
        this.mTable = table;
        this.addStat(STAT_TYPE.UNIT_HP_MAX, table.MaxHp);
    }

    equip(equip: AbilityEquip, slotNumber: number): boolean {
        if (!equip || slotNumber <= 0) return false;
        if (equip.isEquipped) equip.clearOwner();
        const prev = this.mEquips.get(slotNumber);
        if (prev) prev.clearOwner();
        this.mEquips.set(slotNumber, equip);
        equip.setOwner(this.unitId, slotNumber);
        return true;
    }

    unequip(slotNumber: number): boolean {
        const equip = this.mEquips.get(slotNumber);
        if (!equip) return false;
        equip.clearOwner();
        this.mEquips.delete(slotNumber);
        return true;
    }

    clone(): AbilityUnitHero {
        const c = new AbilityUnitHero();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
