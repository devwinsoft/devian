using Devian.Domain.Game;

namespace Devian
{
    public static class AbilityItemFactory
    {
        public static GameResult<AbilityItemCard> CreateCard(string itemId, int itemLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return GameResult<AbilityItemCard>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateCard: item_id is null or empty.");
            }

            var table = TB_ITEM_CARD.Get(itemId);
            if (table == null)
            {
                return GameResult<AbilityItemCard>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_CARD not found: {itemId}");
            }

            return CreateCard(table, itemLevel);
        }

        public static GameResult<AbilityItemCard> CreateCard(ITEM_CARD table, int itemLevel = 1)
        {
            if (table == null)
            {
                return GameResult<AbilityItemCard>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateCard: table is null.");
            }

            var levelTable = resolveCardLevelTable(table.item_id, itemLevel);
            if (levelTable.IsFailure)
                return GameResult<AbilityItemCard>.Failure(levelTable.Error!);

            var ability = new AbilityItemCard();
            ability.Init(table, levelTable.Value);
            return GameResult<AbilityItemCard>.Success(ability);
        }

        public static GameResult<AbilityItemMaterial> CreateMaterial(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return GameResult<AbilityItemMaterial>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateMaterial: item_id is null or empty.");
            }

            var table = TB_ITEM_MATERIAL.Get(itemId);
            if (table == null)
            {
                return GameResult<AbilityItemMaterial>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_MATERIAL not found: {itemId}");
            }

            return CreateMaterial(table);
        }

        public static GameResult<AbilityItemMaterial> CreateMaterial(ITEM_MATERIAL table)
        {
            if (table == null)
            {
                return GameResult<AbilityItemMaterial>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateMaterial: table is null.");
            }

            var ability = new AbilityItemMaterial();
            ability.Init(table);
            return GameResult<AbilityItemMaterial>.Success(ability);
        }

        public static GameResult<AbilityItemHero> CreateHero(
            string heroId,
            int itemLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return GameResult<AbilityItemHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateHero: heroId is null or empty.");
            }

            var table = TB_ITEM_HERO.Get(heroId);
            if (table == null)
            {
                return GameResult<AbilityItemHero>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_HERO not found: {heroId}");
            }

            return CreateHero(table, itemLevel);
        }

        public static GameResult<AbilityItemHero> CreateHero(
            ITEM_HERO table,
            int itemLevel = 1)
        {
            if (table == null)
            {
                return GameResult<AbilityItemHero>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateHero: table is null.");
            }

            var levelTable = resolveHeroLevelTable(table.item_id, itemLevel);
            if (levelTable.IsFailure)
                return GameResult<AbilityItemHero>.Failure(levelTable.Error!);

            var ability = new AbilityItemHero();
            ability.Init(table, levelTable.Value);
            return GameResult<AbilityItemHero>.Success(ability);
        }

        public static GameResult<AbilityItemEquip> CreateEquip(
            string itemId,
            string itemUid,
            int itemLevel = 1,
            string ownerUnitId = null,
            EQUIP_SLOT_TYPE ownerSlotType = EQUIP_SLOT_TYPE.NONE)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: item_id is null or empty.");
            }

            var table = TB_ITEM_EQUIP.Get(itemId);
            if (table == null)
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_EQUIP not found: {itemId}");
            }

            return CreateEquip(table, itemUid, itemLevel, ownerUnitId, ownerSlotType);
        }

        public static GameResult<AbilityItemEquip> CreateEquip(
            ITEM_EQUIP table,
            string itemUid,
            int itemLevel = 1,
            string ownerUnitId = null,
            EQUIP_SLOT_TYPE ownerSlotType = EQUIP_SLOT_TYPE.NONE)
        {
            if (table == null)
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: table is null.");
            }

            if (string.IsNullOrWhiteSpace(itemUid))
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: itemUid is null or empty.");
            }

            var hasOwnerUnitId = !string.IsNullOrWhiteSpace(ownerUnitId);
            var hasOwnerSlot = ownerSlotType != EQUIP_SLOT_TYPE.NONE;
            if ((hasOwnerSlot && !hasOwnerUnitId) || (!hasOwnerSlot && hasOwnerUnitId))
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: ownerUnitId/ownerSlotType must both be set or both be empty.");
            }

            var levelTable = resolveEquipLevelTable(table.item_id, itemLevel);
            if (levelTable.IsFailure)
                return GameResult<AbilityItemEquip>.Failure(levelTable.Error!);

            var ability = new AbilityItemEquip();
            ability.Init(table, levelTable.Value, itemUid);

            if (ownerSlotType != EQUIP_SLOT_TYPE.NONE)
                ability.SetOwner(ownerUnitId, ownerSlotType);

            return GameResult<AbilityItemEquip>.Success(ability);
        }

        static GameResult<ITEM_CARD_LEVEL> resolveCardLevelTable(
            string itemId,
            int itemLevel)
        {
            var resolveLevel = resolveItemLevel(itemLevel, "AbilityItemFactory.CreateCard");
            if (resolveLevel.IsFailure)
                return GameResult<ITEM_CARD_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findCardLevelRow(itemId, resolveLevel.Value);
            if (levelTable == null)
            {
                return GameResult<ITEM_CARD_LEVEL>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_CARD_LEVEL not found: item_id={itemId}, level={resolveLevel.Value}");
            }

            return GameResult<ITEM_CARD_LEVEL>.Success(levelTable);
        }

        internal static GameResult<ITEM_CARD_LEVEL> ResolveNextCardLevelTable(string itemId, int currentItemLevel)
        {
            return resolveCardLevelTable(itemId, currentItemLevel + 1);
        }

        static GameResult<ITEM_HERO_LEVEL> resolveHeroLevelTable(
            string itemId,
            int itemLevel)
        {
            var resolveLevel = resolveItemLevel(itemLevel, "AbilityItemFactory.CreateHero");
            if (resolveLevel.IsFailure)
                return GameResult<ITEM_HERO_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findHeroLevelRow(itemId, resolveLevel.Value);
            if (levelTable == null)
            {
                return GameResult<ITEM_HERO_LEVEL>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_HERO_LEVEL not found: item_id={itemId}, level={resolveLevel.Value}");
            }

            return GameResult<ITEM_HERO_LEVEL>.Success(levelTable);
        }

        internal static GameResult<ITEM_HERO_LEVEL> ResolveNextHeroLevelTable(string itemId, int currentItemLevel)
        {
            return resolveHeroLevelTable(itemId, currentItemLevel + 1);
        }

        static GameResult<ITEM_EQUIP_LEVEL> resolveEquipLevelTable(
            string itemId,
            int itemLevel)
        {
            var resolveLevel = resolveItemLevel(itemLevel, "AbilityItemFactory.CreateEquip");
            if (resolveLevel.IsFailure)
                return GameResult<ITEM_EQUIP_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findEquipLevelRow(itemId, resolveLevel.Value);
            if (levelTable == null)
            {
                return GameResult<ITEM_EQUIP_LEVEL>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_EQUIP_LEVEL not found: item_id={itemId}, level={resolveLevel.Value}");
            }

            return GameResult<ITEM_EQUIP_LEVEL>.Success(levelTable);
        }

        internal static GameResult<ITEM_EQUIP_LEVEL> ResolveNextEquipLevelTable(string itemId, int currentItemLevel)
        {
            return resolveEquipLevelTable(itemId, currentItemLevel + 1);
        }

        static GameResult<int> resolveItemLevel(int itemLevel, string context)
        {
            if (itemLevel < 1)
            {
                return GameResult<int>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"{context}: item_level must be >= 1. actual={itemLevel}");
            }

            return GameResult<int>.Success(itemLevel);
        }

        static ITEM_CARD_LEVEL findCardLevelRow(string itemId, int level)
        {
            var rows = TB_ITEM_CARD_LEVEL.GetByGroup(itemId);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && row.item_level == level)
                    return row;
            }

            return null;
        }

        static ITEM_HERO_LEVEL findHeroLevelRow(string itemId, int level)
        {
            var rows = TB_ITEM_HERO_LEVEL.GetByGroup(itemId);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && row.item_level == level)
                    return row;
            }

            return null;
        }

        static ITEM_EQUIP_LEVEL findEquipLevelRow(string itemId, int level)
        {
            var rows = TB_ITEM_EQUIP_LEVEL.GetByGroup(itemId);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && row.item_level == level)
                    return row;
            }

            return null;
        }
    }
}
