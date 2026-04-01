import { SKILL } from '../../Generated/Game.g';
import { AbilityBattleBase } from './AbilityBattleBase';

export class AbilityBattleSkill extends AbilityBattleBase {
    private mTable: SKILL | null = null;

    get skillId(): string { return this.mTable?.skill_id ?? ''; }
    get nameId(): string { return this.mTable?.name_id ?? ''; }
    get affectList(): readonly string[] { return this.mTable?.affect_list ?? []; }

    init(table: SKILL): void {
        this.mTable = table;
    }

    clone(): AbilityBattleSkill {
        const c = new AbilityBattleSkill();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
