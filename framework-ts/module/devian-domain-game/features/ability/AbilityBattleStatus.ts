import { STATUS } from '../../Generated/Game.g';
import { AbilityBattleBase } from './AbilityBattleBase';

export class AbilityBattleStatus extends AbilityBattleBase {
    private mTable: STATUS | null = null;

    get statusId(): string { return this.mTable?.StatusId ?? ''; }
    get nameId(): string { return this.mTable?.NameId ?? ''; }
    get affectList(): readonly string[] { return this.mTable?.AffectList ?? []; }

    init(table: STATUS): void {
        this.mTable = table;
    }

    clone(): AbilityBattleStatus {
        const c = new AbilityBattleStatus();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
