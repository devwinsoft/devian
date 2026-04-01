import { ITEM_MATERIAL } from '../../Generated/Game.g';
import { AbilityItemBase } from './AbilityItemBase';

export class AbilityItemMaterial extends AbilityItemBase {
    private mTable: ITEM_MATERIAL | null = null;

    get itemId(): string { return this.mTable?.item_id ?? ''; }

    init(table: ITEM_MATERIAL): void {
        this.mTable = table;
    }

    clone(): AbilityItemMaterial {
        const c = new AbilityItemMaterial();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
