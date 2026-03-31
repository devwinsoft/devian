import { AFFECT } from '../../Generated/Game.g';
import { AbilityBase } from './AbilityBase';

export class AbilityAffect extends AbilityBase {
    private mTable: AFFECT | null = null;

    get affectId(): string { return this.mTable?.AffectId ?? ''; }
    get nameId(): string { return this.mTable?.NameId ?? ''; }

    init(table: AFFECT): void {
        this.mTable = table;
    }

    clone(): AbilityAffect {
        const c = new AbilityAffect();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
