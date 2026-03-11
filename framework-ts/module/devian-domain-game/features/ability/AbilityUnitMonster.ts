import { STAT_TYPE, UNIT_MONSTER } from '../../Generated/Game.g';
import { AbilityUnitBase } from './AbilityUnitBase';

export class AbilityUnitMonster extends AbilityUnitBase {
    private mTable: UNIT_MONSTER | null = null;

    get unitId(): string { return this.mTable?.UnitId ?? ''; }

    init(table: UNIT_MONSTER): void {
        this.mTable = table;
        this.addStat(STAT_TYPE.UNIT_HP_MAX, table.MaxHp);
    }

    clone(): AbilityUnitMonster {
        const c = new AbilityUnitMonster();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
