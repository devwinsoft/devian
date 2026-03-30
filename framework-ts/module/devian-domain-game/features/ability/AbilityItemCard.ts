import { ITEM_CARD } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';

export class AbilityItemCard extends AbilityItemBase {
    private mTable: ITEM_CARD | null = null;

    get itemId(): string { return this.mTable?.ItemId ?? ''; }

    init(table: ITEM_CARD): void {
        this.mTable = table;
    }

    clone(): AbilityItemCard {
        const c = new AbilityItemCard();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
