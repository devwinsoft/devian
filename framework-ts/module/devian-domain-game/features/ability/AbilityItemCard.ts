import { ITEM_CARD, ITEM_CARD_LEVEL } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';

export class AbilityItemCard extends AbilityItemBase {
    private mTable: ITEM_CARD | null = null;
    private mLevelTable: ITEM_CARD_LEVEL | null = null;

    get itemId(): string { return this.mTable?.ItemId ?? ''; }

    init(table: ITEM_CARD, levelTable: ITEM_CARD_LEVEL): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.initLevelStats(
            levelTable.ItemLevel,
            levelTable.StatType00, levelTable.StatValue00,
            levelTable.StatType01, levelTable.StatValue01,
            levelTable.StatType02, levelTable.StatValue02,
            levelTable.StatType03, levelTable.StatValue03,
        );
    }

    clone(): AbilityItemCard {
        const c = new AbilityItemCard();
        c.mTable = this.mTable;
        c.mLevelTable = this.mLevelTable;
        c.copyStatsFrom(this);
        return c;
    }
}
