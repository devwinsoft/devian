using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public static class AbilityUnitFactory
    {
        public static CommonResult<AbilityUnitHero> CreateHero(string unitId, int unitLevel = 1)
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

            return CreateHero(table, unitLevel);
        }

        public static CommonResult<AbilityUnitHero> CreateHero(UNIT_HERO table, int unitLevel = 1)
        {
            if (table == null)
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: table is null.");
            }

            var levelTable = resolveHeroLevelTable(table.UnitId, unitLevel);
            if (levelTable.IsFailure)
                return CommonResult<AbilityUnitHero>.Failure(levelTable.Error!);

            var ability = new AbilityUnitHero();
            ability.Init(table, levelTable.Value);
            return CommonResult<AbilityUnitHero>.Success(ability);
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

            var resolvedUnitLevel = resolveHeroContextUnitLevel(context);
            if (resolvedUnitLevel.IsFailure)
                return CommonResult<AbilityUnitHero>.Failure(resolvedUnitLevel.Error!);

            var create = CreateHero(table, resolvedUnitLevel.Value);
            if (create.IsFailure)
                return create;

            var ability = create.Value;

            var projectEquips = addEquips(ability, context.Equips);
            if (projectEquips.IsFailure)
                return CommonResult<AbilityUnitHero>.Failure(projectEquips.Error!);
            resetCurrentHp(ability);

            return CommonResult<AbilityUnitHero>.Success(ability);
        }

        public static CommonResult<AbilityUnitHero> CreateHero(AbilityItemHero itemHero)
        {
            if (itemHero == null)
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: itemHero is null.");
            }

            if (string.IsNullOrWhiteSpace(itemHero.UnitId))
            {
                return CommonResult<AbilityUnitHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: itemHero.UnitId is null or empty.");
            }

            return CreateHero(new AbilityUnitHeroContext
            {
                UnitId = itemHero.UnitId,
                UnitLevel = itemHero.ItemLevel,
                Equips = itemHero.Equips,
            });
        }

        public static CommonResult<AbilityUnitMonster> CreateMonster(string unitId, int unitLevel = 1)
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

            return CreateMonster(table, unitLevel);
        }

        public static CommonResult<AbilityUnitMonster> CreateMonster(UNIT_MONSTER table, int unitLevel = 1)
        {
            if (table == null)
            {
                return CommonResult<AbilityUnitMonster>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateMonster: table is null.");
            }

            var levelTable = resolveMonsterLevelTable(table.UnitId, unitLevel);
            if (levelTable.IsFailure)
                return CommonResult<AbilityUnitMonster>.Failure(levelTable.Error!);

            var ability = new AbilityUnitMonster();
            ability.Init(table, levelTable.Value);
            return CommonResult<AbilityUnitMonster>.Success(ability);
        }

        static CommonResult<UNIT_HERO_LEVEL> resolveHeroLevelTable(string unitId, int unitLevel)
        {
            var resolveLevel = resolveUnitLevel(unitLevel, "AbilityUnitFactory.CreateHero");
            if (resolveLevel.IsFailure)
                return CommonResult<UNIT_HERO_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findHeroLevelRow(unitId, resolveLevel.Value);
            if (levelTable == null)
            {
                return CommonResult<UNIT_HERO_LEVEL>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_HERO_LEVEL not found: unitId={unitId}, level={resolveLevel.Value}");
            }

            return CommonResult<UNIT_HERO_LEVEL>.Success(levelTable);
        }

        static CommonResult<UNIT_MONSTER_LEVEL> resolveMonsterLevelTable(string unitId, int unitLevel)
        {
            var resolveLevel = resolveUnitLevel(unitLevel, "AbilityUnitFactory.CreateMonster");
            if (resolveLevel.IsFailure)
                return CommonResult<UNIT_MONSTER_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findMonsterLevelRow(unitId, resolveLevel.Value);
            if (levelTable == null)
            {
                return CommonResult<UNIT_MONSTER_LEVEL>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_MONSTER_LEVEL not found: unitId={unitId}, level={resolveLevel.Value}");
            }

            return CommonResult<UNIT_MONSTER_LEVEL>.Success(levelTable);
        }

        static CommonResult<int> resolveHeroContextUnitLevel(AbilityUnitHeroContext context)
        {
            return resolveUnitLevel(context.UnitLevel, "AbilityUnitFactory.CreateHero");
        }

        static CommonResult<int> resolveUnitLevel(int unitLevel, string context)
        {
            if (unitLevel < 1)
            {
                return CommonResult<int>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"{context}: unitLevel must be >= 1. actual={unitLevel}");
            }

            return CommonResult<int>.Success(unitLevel);
        }

        static UNIT_HERO_LEVEL findHeroLevelRow(string unitId, int unitLevel)
        {
            foreach (var row in TB_UNIT_HERO_LEVEL.GetByGroup(unitId))
            {
                if (row.UnitLevel == unitLevel)
                    return row;
            }

            return null;
        }

        static UNIT_MONSTER_LEVEL findMonsterLevelRow(string unitId, int unitLevel)
        {
            foreach (var row in TB_UNIT_MONSTER_LEVEL.GetByGroup(unitId))
            {
                if (row.UnitLevel == unitLevel)
                    return row;
            }

            return null;
        }

        static CommonResult addEquips(
            AbilityUnitHero ability,
            System.Collections.Generic.IReadOnlyDictionary<int, AbilityItemEquip> equips)
        {
            if (ability == null || equips == null)
                return CommonResult.Ok();

            foreach (var kv in equips)
            {
                if (kv.Key <= 0)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: equip slot must be >= 1. actual={kv.Key}");
                }

                if (kv.Value == null)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: equip is null. slot={kv.Key}");
                }

                if (kv.Value.Clone() is not AbilityItemEquip equipClone)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: failed to clone equip. slot={kv.Key}");
                }

                if (!ability.Equip(equipClone, kv.Key))
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: failed to project equip. slot={kv.Key}, itemUid={kv.Value.ItemUid}");
                }
            }

            return CommonResult.Ok();
        }

        static void resetCurrentHp(AbilityUnitBase ability)
        {
            if (ability == null)
                return;

            ability.ResetCurrentHpToMax();
        }
    }
}
