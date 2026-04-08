using Devian.Domain.Game;

namespace Devian
{
    public static class AbilityUnitFactory
    {
        public static GameResult<AbilityUnitHero> CreateHero(string unitId, int unitLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: unit_id is null or empty.");
            }

            var table = TB_UNIT_HERO.Get(unitId);
            if (table == null)
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_HERO not found: {unitId}");
            }

            return CreateHero(table, unitLevel);
        }

        public static GameResult<AbilityUnitHero> CreateHero(UNIT_HERO table, int unitLevel = 1)
        {
            if (table == null)
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: table is null.");
            }

            var levelTable = resolveHeroLevelTable(table.unit_id, unitLevel);
            if (levelTable.IsFailure)
                return GameResult<AbilityUnitHero>.Failure(levelTable.Error!);

            var ability = new AbilityUnitHero();
            ability.Init(table, levelTable.Value);
            return GameResult<AbilityUnitHero>.Success(ability);
        }

        public static GameResult<AbilityUnitHero> CreateHero(AbilityUnitHeroContext context)
        {
            if (context == null)
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: context is null.");
            }

            if (string.IsNullOrWhiteSpace(context.UnitId))
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitHeroContext.UnitId is required.");
            }

            var table = TB_UNIT_HERO.Get(context.UnitId);
            if (table == null)
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_HERO not found: {context.UnitId}");
            }

            var resolvedUnitLevel = resolveHeroContextUnitLevel(context);
            if (resolvedUnitLevel.IsFailure)
                return GameResult<AbilityUnitHero>.Failure(resolvedUnitLevel.Error!);

            var create = CreateHero(table, resolvedUnitLevel.Value);
            if (create.IsFailure)
                return create;

            var ability = create.Value;

            var projectEquips = addEquips(ability, context.Equips);
            if (projectEquips.IsFailure)
                return GameResult<AbilityUnitHero>.Failure(projectEquips.Error!);
            resetCurrentHp(ability);

            return GameResult<AbilityUnitHero>.Success(ability);
        }

        public static GameResult<AbilityUnitHero> CreateHero(AbilityItemHero itemHero)
        {
            if (itemHero == null)
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: itemHero is null.");
            }

            if (string.IsNullOrWhiteSpace(itemHero.UnitId))
            {
                return GameResult<AbilityUnitHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateHero: itemHero.UnitId is null or empty.");
            }

            return CreateHero(new AbilityUnitHeroContext
            {
                UnitId = itemHero.UnitId,
                UnitLevel = itemHero.ItemLevel,
                Equips = itemHero.Equips,
            });
        }

        public static GameResult<AbilityUnitMonster> CreateMonster(string unitId, int unitLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return GameResult<AbilityUnitMonster>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateMonster: unit_id is null or empty.");
            }

            var table = TB_UNIT_MONSTER.Get(unitId);
            if (table == null)
            {
                return GameResult<AbilityUnitMonster>.Failure(
                    GAME_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_MONSTER not found: {unitId}");
            }

            return CreateMonster(table, unitLevel);
        }

        public static GameResult<AbilityUnitMonster> CreateMonster(UNIT_MONSTER table, int unitLevel = 1)
        {
            if (table == null)
            {
                return GameResult<AbilityUnitMonster>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityUnitFactory.CreateMonster: table is null.");
            }

            var levelTable = resolveMonsterLevelTable(table.unit_id, unitLevel);
            if (levelTable.IsFailure)
                return GameResult<AbilityUnitMonster>.Failure(levelTable.Error!);

            var ability = new AbilityUnitMonster();
            ability.Init(table, levelTable.Value);
            return GameResult<AbilityUnitMonster>.Success(ability);
        }

        static GameResult<UNIT_HERO_LEVEL> resolveHeroLevelTable(string unitId, int unitLevel)
        {
            var resolveLevel = resolveUnitLevel(unitLevel, "AbilityUnitFactory.CreateHero");
            if (resolveLevel.IsFailure)
                return GameResult<UNIT_HERO_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findHeroLevelRow(unitId, resolveLevel.Value);
            if (levelTable == null)
            {
                return GameResult<UNIT_HERO_LEVEL>.Failure(
                    GAME_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_HERO_LEVEL not found: unit_id={unitId}, level={resolveLevel.Value}");
            }

            return GameResult<UNIT_HERO_LEVEL>.Success(levelTable);
        }

        static GameResult<UNIT_MONSTER_LEVEL> resolveMonsterLevelTable(string unitId, int unitLevel)
        {
            var resolveLevel = resolveUnitLevel(unitLevel, "AbilityUnitFactory.CreateMonster");
            if (resolveLevel.IsFailure)
                return GameResult<UNIT_MONSTER_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findMonsterLevelRow(unitId, resolveLevel.Value);
            if (levelTable == null)
            {
                return GameResult<UNIT_MONSTER_LEVEL>.Failure(
                    GAME_ERROR_TYPE.ABILITY_UNIT_TABLE_NOT_FOUND,
                    $"UNIT_MONSTER_LEVEL not found: unit_id={unitId}, level={resolveLevel.Value}");
            }

            return GameResult<UNIT_MONSTER_LEVEL>.Success(levelTable);
        }

        static GameResult<int> resolveHeroContextUnitLevel(AbilityUnitHeroContext context)
        {
            return resolveUnitLevel(context.UnitLevel, "AbilityUnitFactory.CreateHero");
        }

        static GameResult<int> resolveUnitLevel(int unitLevel, string context)
        {
            if (unitLevel < 1)
            {
                return GameResult<int>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"{context}: unitLevel must be >= 1. actual={unitLevel}");
            }

            return GameResult<int>.Success(unitLevel);
        }

        static UNIT_HERO_LEVEL findHeroLevelRow(string unitId, int unitLevel)
        {
            foreach (var row in TB_UNIT_HERO_LEVEL.GetByGroup(unitId))
            {
                if (row.unit_level == unitLevel)
                    return row;
            }

            return null;
        }

        static UNIT_MONSTER_LEVEL findMonsterLevelRow(string unitId, int unitLevel)
        {
            foreach (var row in TB_UNIT_MONSTER_LEVEL.GetByGroup(unitId))
            {
                if (row.unit_level == unitLevel)
                    return row;
            }

            return null;
        }

        static GameResult addEquips(
            AbilityUnitHero ability,
            System.Collections.Generic.IReadOnlyDictionary<EQUIP_SLOT_TYPE, AbilityItemEquip> equips)
        {
            if (ability == null || equips == null)
                return GameResult.Ok();

            foreach (var kv in equips)
            {
                if (kv.Key == EQUIP_SLOT_TYPE.NONE)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: equip slot must not be NONE. actual={kv.Key}");
                }

                if (kv.Value == null)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: equip is null. slot={kv.Key}");
                }

                if (kv.Value.Clone() is not AbilityItemEquip equipClone)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: failed to clone equip. slot={kv.Key}");
                }

                if (!ability._Equip(equipClone, kv.Key))
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"AbilityUnitFactory.CreateHero: failed to project equip. slot={kv.Key}, itemUid={kv.Value.ItemUid}");
                }
            }

            return GameResult.Ok();
        }

        static void resetCurrentHp(AbilityUnitBase ability)
        {
            if (ability == null)
                return;

            ability.ResetCurrentHpToMax();
        }
    }
}
