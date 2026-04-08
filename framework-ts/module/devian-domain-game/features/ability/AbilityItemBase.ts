import { UNIT_STAT_TYPE } from '../../Generated/Game.g';
import { AbilityBase } from './AbilityBase';

export abstract class AbilityItemBase extends AbilityBase {
    abstract get itemId(): string;
    get amount(): number { return this.getInt(UNIT_STAT_TYPE.ITEM_AMOUNT); }
    get itemLevel(): number { return this.getInt(UNIT_STAT_TYPE.ITEM_LEVEL); }

    addAmount(delta: number): void {
        this.addStat(UNIT_STAT_TYPE.ITEM_AMOUNT, delta);
    }

    protected initLevelStats(
        itemLevel: number,
        statType00: UNIT_STAT_TYPE, statValue00: number,
        statType01: UNIT_STAT_TYPE, statValue01: number,
        statType02: UNIT_STAT_TYPE, statValue02: number,
        statType03: UNIT_STAT_TYPE, statValue03: number,
    ): void {
        this.setStat(UNIT_STAT_TYPE.ITEM_LEVEL, itemLevel);
        this.applyLevelStat(statType00, statValue00);
        this.applyLevelStat(statType01, statValue01);
        this.applyLevelStat(statType02, statValue02);
        this.applyLevelStat(statType03, statValue03);
    }

    private applyLevelStat(statType: UNIT_STAT_TYPE, statValue: number): void {
        if (statType === UNIT_STAT_TYPE.NONE) return;
        this.setStat(statType, statValue);
    }
}
