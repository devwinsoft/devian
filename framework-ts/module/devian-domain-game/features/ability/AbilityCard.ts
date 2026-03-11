import { ITEM_CARD } from '../../Generated/Game.g';
import { AbilityBase } from './AbilityBase';

export class AbilityCard extends AbilityBase {
    private mTable: ITEM_CARD | null = null;

    get cardId(): string { return this.mTable?.CardId ?? ''; }

    init(table: ITEM_CARD): void {
        this.mTable = table;
    }

    clone(): AbilityCard {
        const c = new AbilityCard();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
