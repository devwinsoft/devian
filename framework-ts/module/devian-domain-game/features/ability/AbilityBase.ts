import { STAT_TYPE } from '../../Generated/Game.g';

export abstract class AbilityBase {
    private mStats: Map<STAT_TYPE, number> = new Map();

    getStat(type: STAT_TYPE): number {
        return this.mStats.get(type) ?? 0;
    }

    getInt(type: STAT_TYPE): number {
        return this.mStats.get(type) ?? 0;
    }

    getFloat(type: STAT_TYPE): number {
        return this.getInt(type) * 0.0001;
    }

    addStat(type: STAT_TYPE, value: number): void;
    addStat(other: AbilityBase): void;
    addStat(typeOrOther: STAT_TYPE | AbilityBase, value?: number): void {
        if (typeOrOther instanceof AbilityBase) {
            for (const [k, v] of typeOrOther.mStats) {
                this.addStat(k, v);
            }
        } else {
            const cur = this.mStats.get(typeOrOther) ?? 0;
            this.mStats.set(typeOrOther, cur + value!);
        }
    }

    setStat(type: STAT_TYPE, value: number): void {
        this.mStats.set(type, value);
    }

    clearStat(type: STAT_TYPE): void {
        this.mStats.delete(type);
    }

    clearStats(): void {
        this.mStats.clear();
    }

    getStats(): ReadonlyMap<STAT_TYPE, number> {
        return this.mStats;
    }

    abstract clone(): AbilityBase;

    protected copyStatsFrom(source: AbilityBase): void {
        for (const [k, v] of source.mStats) {
            this.mStats.set(k, v);
        }
    }
}
