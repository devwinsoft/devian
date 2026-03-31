import { STAT_TYPE } from '../../Generated/Game.g';

export abstract class AbilityBase {
    private mStats: Map<STAT_TYPE, number> = new Map();

    get stats(): ReadonlyMap<STAT_TYPE, number> {
        return this.mStats;
    }

    get atkPhysical(): number {
        return this.getScaledStat(
            STAT_TYPE.AFFECT_ATK_PHY_ADD,
            STAT_TYPE.AFFECT_ATK_PHY_PER,
            STAT_TYPE.ITEM_ATK_PHY,
            STAT_TYPE.UNIT_ATK_PHY,
        );
    }

    get atkMagical(): number {
        return this.getScaledStat(
            STAT_TYPE.AFFECT_ATK_MAG_ADD,
            STAT_TYPE.AFFECT_ATK_MAG_PER,
            STAT_TYPE.ITEM_ATK_MAG,
            STAT_TYPE.UNIT_ATK_MAG,
        );
    }

    get defPhysical(): number {
        return this.getScaledStat(
            STAT_TYPE.AFFECT_DEF_PHY_ADD,
            STAT_TYPE.AFFECT_DEF_PHY_PER,
            STAT_TYPE.ITEM_DEF_PHY,
            STAT_TYPE.UNIT_DEF_PHY,
        );
    }

    get defMagical(): number {
        return this.getScaledStat(
            STAT_TYPE.AFFECT_DEF_MAG_ADD,
            STAT_TYPE.AFFECT_DEF_MAG_PER,
            STAT_TYPE.ITEM_DEF_MAG,
            STAT_TYPE.UNIT_DEF_MAG,
        );
    }

    get maxHP(): number {
        return this.getScaledStat(
            STAT_TYPE.AFFECT_HP_ADD,
            STAT_TYPE.AFFECT_HP_PER,
            STAT_TYPE.ITEM_HP,
            STAT_TYPE.UNIT_HP,
        );
    }

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
        return this.stats;
    }

    abstract clone(): AbilityBase;

    protected copyStatsFrom(source: AbilityBase): void {
        for (const [k, v] of source.mStats) {
            this.mStats.set(k, v);
        }
    }

    private getScaledStat(
        affectAddType: STAT_TYPE,
        affectPerType: STAT_TYPE,
        itemAddType: STAT_TYPE,
        unitAddType: STAT_TYPE,
    ): number {
        const affectItemAdd = this.getInt(affectAddType) + this.getInt(itemAddType);
        const scaled = Math.trunc(affectItemAdd * (100 + this.getInt(affectPerType)) * 0.01);
        return scaled + this.getInt(unitAddType);
    }
}
