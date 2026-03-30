import { STAT_TYPE } from '../../Generated/Game.g';
import { AbilityBase } from './AbilityBase';

export abstract class AbilityItemBase extends AbilityBase {
    abstract get itemId(): string;
    get amount(): number { return this.getInt(STAT_TYPE.ITEM_AMOUNT); }
    get level(): number { return this.getInt(STAT_TYPE.ITEM_LEVEL); }

    addAmount(delta: number): void {
        this.addStat(STAT_TYPE.ITEM_AMOUNT, delta);
    }
}
