import { PROJECTILE } from '../../Generated/Game.g';
import { AbilityBattleBase } from './AbilityBattleBase';

export class AbilityBattleProjectile extends AbilityBattleBase {
    private mTable: PROJECTILE | null = null;

    get projectileId(): string { return this.mTable?.ProjectileId ?? ''; }
    get nameId(): string { return this.mTable?.NameId ?? ''; }
    get affectList(): readonly string[] { return this.mTable?.AffectList ?? []; }

    init(table: PROJECTILE): void {
        this.mTable = table;
    }

    clone(): AbilityBattleProjectile {
        const c = new AbilityBattleProjectile();
        c.mTable = this.mTable;
        c.copyStatsFrom(this);
        return c;
    }
}
