import { STATUS } from '../../Generated/Game.g';
import { AbilityBattleBase } from './AbilityBattleBase';

export class AbilityBattleStatus extends AbilityBattleBase {
    private mTable: STATUS | null = null;

    get statusId(): string { return this.mTable?.status_id ?? ''; }
    get nameId(): string { return this.mTable?.name_id ?? ''; }
    get affectList(): readonly string[] { return this.mTable?.affect_list ?? []; }

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
