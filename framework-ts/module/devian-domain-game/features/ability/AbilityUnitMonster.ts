import { UNIT_MONSTER, UNIT_MONSTER_LEVEL } from '../../Generated/Game.g';
import { AbilityUnitBase } from './AbilityUnitBase';

export class AbilityUnitMonster extends AbilityUnitBase {
    private mTable: UNIT_MONSTER | null = null;
    private mLevelTable: UNIT_MONSTER_LEVEL | null = null;

    get unitId(): string { return this.mTable?.unit_id ?? ''; }

    init(table: UNIT_MONSTER, levelTable: UNIT_MONSTER_LEVEL): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.initUnitState(levelTable.unit_level, levelTable.max_hp);
    }

    clone(): AbilityUnitMonster {
        const c = new AbilityUnitMonster();
        c.mTable = this.mTable;
        c.mLevelTable = this.mLevelTable;
        c.copyStatsFrom(this);
        c.copyUnitStateFrom(this);
        return c;
    }
}
