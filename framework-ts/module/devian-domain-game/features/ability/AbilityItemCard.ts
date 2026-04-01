import { ITEM_CARD, ITEM_CARD_LEVEL } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';

export class AbilityItemCard extends AbilityItemBase {
    private mTable: ITEM_CARD | null = null;
    private mLevelTable: ITEM_CARD_LEVEL | null = null;

    get itemId(): string { return this.mTable?.item_id ?? ''; }

    init(table: ITEM_CARD, levelTable: ITEM_CARD_LEVEL): void {
        this.mTable = table;
        this.mLevelTable = levelTable;
        this.initLevelStats(
            levelTable.item_level,
            levelTable.stat_type00, levelTable.stat_value00,
            levelTable.stat_type01, levelTable.stat_value01,
            levelTable.stat_type02, levelTable.stat_value02,
            levelTable.stat_type03, levelTable.stat_value03,
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
