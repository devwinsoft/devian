import { UNIT_STAT_TYPE } from '../../Generated/Game.g';

export abstract class AbilityBase {
    private mStats: Map<UNIT_STAT_TYPE, number> = new Map();

    get stats(): ReadonlyMap<UNIT_STAT_TYPE, number> {
        return this.mStats;
    }

    get atkPhysical(): number {
        return this.getScaledStat(
            UNIT_STAT_TYPE.AFFECT_ATK_PHY_ADD,
            UNIT_STAT_TYPE.AFFECT_ATK_PHY_PER,
            UNIT_STAT_TYPE.ITEM_ATK_PHY,
            UNIT_STAT_TYPE.UNIT_ATK_PHY,
        );
    }

    get atkMagical(): number {
        return this.getScaledStat(
            UNIT_STAT_TYPE.AFFECT_ATK_MAG_ADD,
            UNIT_STAT_TYPE.AFFECT_ATK_MAG_PER,
            UNIT_STAT_TYPE.ITEM_ATK_MAG,
            UNIT_STAT_TYPE.UNIT_ATK_MAG,
        );
    }

    get defPhysical(): number {
        return this.getScaledStat(
            UNIT_STAT_TYPE.AFFECT_DEF_PHY_ADD,
            UNIT_STAT_TYPE.AFFECT_DEF_PHY_PER,
            UNIT_STAT_TYPE.ITEM_DEF_PHY,
            UNIT_STAT_TYPE.UNIT_DEF_PHY,
        );
    }

    get defMagical(): number {
        return this.getScaledStat(
            UNIT_STAT_TYPE.AFFECT_DEF_MAG_ADD,
            UNIT_STAT_TYPE.AFFECT_DEF_MAG_PER,
            UNIT_STAT_TYPE.ITEM_DEF_MAG,
            UNIT_STAT_TYPE.UNIT_DEF_MAG,
        );
    }

    get maxHP(): number {
        return this.getScaledStat(
            UNIT_STAT_TYPE.AFFECT_HP_ADD,
            UNIT_STAT_TYPE.AFFECT_HP_PER,
            UNIT_STAT_TYPE.ITEM_HP,
            UNIT_STAT_TYPE.UNIT_HP,
        );
    }

    getStat(type: UNIT_STAT_TYPE): number {
        return this.mStats.get(type) ?? 0;
    }

    getInt(type: UNIT_STAT_TYPE): number {
        return this.mStats.get(type) ?? 0;
    }

    getFloat(type: UNIT_STAT_TYPE): number {
        return this.getInt(type) * 0.0001;
    }

    addStat(type: UNIT_STAT_TYPE, value: number): void;
    addStat(other: AbilityBase): void;
    addStat(typeOrOther: UNIT_STAT_TYPE | AbilityBase, value?: number): void {
        if (typeOrOther instanceof AbilityBase) {
            for (const [k, v] of typeOrOther.mStats) {
                this.addStat(k, v);
            }
        } else {
            const cur = this.mStats.get(typeOrOther) ?? 0;
            this.mStats.set(typeOrOther, cur + value!);
        }
    }

    setStat(type: UNIT_STAT_TYPE, value: number): void {
        this.mStats.set(type, value);
    }

    clearStat(type: UNIT_STAT_TYPE): void {
        this.mStats.delete(type);
    }

    clearStats(): void {
        this.mStats.clear();
    }

    getStats(): ReadonlyMap<UNIT_STAT_TYPE, number> {
        return this.stats;
    }

    abstract clone(): AbilityBase;

    protected copyStatsFrom(source: AbilityBase): void {
        for (const [k, v] of source.mStats) {
            this.mStats.set(k, v);
        }
    }

    private getScaledStat(
        affectAddType: UNIT_STAT_TYPE,
        affectPerType: UNIT_STAT_TYPE,
        itemAddType: UNIT_STAT_TYPE,
        unitAddType: UNIT_STAT_TYPE,
    ): number {
        const affectItemAdd = this.getInt(affectAddType) + this.getInt(itemAddType);
        const scaled = Math.trunc(affectItemAdd * (100 + this.getInt(affectPerType)) * 0.01);
        return scaled + this.getInt(unitAddType);
    }
}
