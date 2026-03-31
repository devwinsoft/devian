using System.Collections.Generic;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public static class AbilityUnitFactory
    {
        public static CommonResult<AbilityUnitHero> CreateHero(string unitId, IReadOnlyDictionary<STAT_TYPE, int> overrideStats = null)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: unitId is null or empty.");
            }

            var table = TB_UNIT_HERO.Get(unitId);
            if (table == null)
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_HERO not found: {unitId}");
            }

            return createHero(table, overrideStats);
        }

        public static CommonResult<AbilityUnitHero> CreateHero(AbilityUnitHeroContext context)
        {
            if (context == null)
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: context is null.");
            }

            if (string.IsNullOrWhiteSpace(context.UnitId))
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitHeroContext.UnitId is required.");
            }

            var table = TB_UNIT_HERO.Get(context.UnitId);
            if (table == null)
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_HERO not found: {context.UnitId}");
            }

            var create = createHero(table, null);
            if (create.IsFailure)
                return create;

            var ability = create.Value;

            if (context.CopyItemLevel && context.SourceItemHero != null)
                ability.SetStat(STAT_TYPE.ITEM_LEVEL, context.SourceItemHero.ItemLevel);

            var project = projectHeroEquips(
                ability,
                context.SourceEquips ?? context.SourceItemHero?.Equips,
                context.CloneEquips);
            if (project.IsFailure)
                return CommonResult<AbilityUnitHero>.Failure(project.Error!);

            AbilityFactoryStatUtil.ApplyStats(ability, context.OverrideStats);
            return CommonResult<AbilityUnitHero>.Success(ability);
        }

        public static CommonResult<AbilityUnitHero> CreateHero(
            AbilityItemHero itemHero,
            string unitId,
            IReadOnlyDictionary<STAT_TYPE, int> overrideStats = null,
            bool cloneEquips = true)
        {
            if (itemHero == null)
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: itemHero is null.");
            }

            return CreateHero(new AbilityUnitHeroContext
            {
                UnitId = unitId,
                SourceItemHero = itemHero,
                OverrideStats = overrideStats,
                CopyItemLevel = true,
                CloneEquips = cloneEquips,
            });
        }

        public static CommonResult<AbilityUnitMonster> CreateMonster(string unitId, IReadOnlyDictionary<STAT_TYPE, int> overrideStats = null)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return CommonResult<AbilityUnitMonster>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateMonster: unitId is null or empty.");
            }

            var table = TB_UNIT_MONSTER.Get(unitId);
            if (table == null)
            {
                return CommonResult<AbilityUnitMonster>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_MONSTER not found: {unitId}");
            }

            return CreateMonster(table, overrideStats);
        }

        public static CommonResult<AbilityUnitMonster> CreateMonster(UNIT_MONSTER table, IReadOnlyDictionary<STAT_TYPE, int> overrideStats = null)
        {
            if (table == null)
            {
                return CommonResult<AbilityUnitMonster>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateMonster: table is null.");
            }

            var ability = new AbilityUnitMonster();
            ability.Init(table);
            AbilityFactoryStatUtil.ApplyStats(ability, overrideStats);
            return CommonResult<AbilityUnitMonster>.Success(ability);
        }

        static CommonResult projectHeroEquips(
            AbilityUnitHero ability,
            IReadOnlyDictionary<int, AbilityItemEquip> equips,
            bool cloneEquips)
        {
            if (ability == null || equips == null)
                return CommonResult.Ok();

            ability.ClearProjectedEquips();

            foreach (var kv in equips)
            {
                if (kv.Value == null)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: equips[{kv.Key}] is null.");
                }

                var equip = cloneEquips
                    ? (AbilityItemEquip)kv.Value.Clone()
                    : kv.Value;

                if (cloneEquips)
                    equip.ClearOwner();

                if (!ability.SetProjectedEquip(equip, kv.Key))
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: invalid equip slot {kv.Key}.");
                }
            }

            return CommonResult.Ok();
        }

        static CommonResult<AbilityUnitHero> createHero(UNIT_HERO table, IReadOnlyDictionary<STAT_TYPE, int> overrideStats)
        {
            if (table == null)
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: table is null.");
            }

            var ability = new AbilityUnitHero();
            ability.Init(table);
            AbilityFactoryStatUtil.ApplyStats(ability, overrideStats);
            return CommonResult<AbilityUnitHero>.Success(ability);
        }
    }
}
