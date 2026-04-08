import { UNIT_STAT_TYPE } from '../../Generated/Game.g';
import { AbilityBase } from './AbilityBase';

export abstract class AbilityUnitBase extends AbilityBase {
    private mCurHP: number = 0;

    abstract get unitId(): string;
    get unitLevel(): number { return this.getInt(UNIT_STAT_TYPE.UNIT_LEVEL); }
    get curHP(): number { return this.mCurHP; }

    protected initUnitState(unitLevel: number, maxHp: number): void {
        this.setStat(UNIT_STAT_TYPE.UNIT_LEVEL, unitLevel);
        this.setStat(UNIT_STAT_TYPE.UNIT_HP, maxHp);
        this.mCurHP = this.maxHP;
    }

    protected copyUnitStateFrom(source: AbilityUnitBase): void {
        this.mCurHP = source.mCurHP;
    }
}
