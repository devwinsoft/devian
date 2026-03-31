using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public static class AbilityItemFactory
    {
        public static CommonResult<AbilityItemCard> CreateCard(string itemId, int itemLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return CommonResult<AbilityItemCard>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateCard: itemId is null or empty.");
            }

            var table = TB_ITEM_CARD.Get(itemId);
            if (table == null)
            {
                return CommonResult<AbilityItemCard>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_CARD not found: {itemId}");
            }

            return CreateCard(table, itemLevel);
        }

        public static CommonResult<AbilityItemCard> CreateCard(ITEM_CARD table, int itemLevel = 1)
        {
            if (table == null)
            {
                return CommonResult<AbilityItemCard>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateCard: table is null.");
            }

            var levelTable = resolveCardLevelTable(table.ItemId, itemLevel);
            if (levelTable.IsFailure)
                return CommonResult<AbilityItemCard>.Failure(levelTable.Error!);

            var ability = new AbilityItemCard();
            ability.Init(table, levelTable.Value);
            return CommonResult<AbilityItemCard>.Success(ability);
        }

        public static CommonResult<AbilityItemMaterial> CreateMaterial(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return CommonResult<AbilityItemMaterial>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateMaterial: itemId is null or empty.");
            }

            var table = TB_ITEM_MATERIAL.Get(itemId);
            if (table == null)
            {
                return CommonResult<AbilityItemMaterial>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_MATERIAL not found: {itemId}");
            }

            return CreateMaterial(table);
        }

        public static CommonResult<AbilityItemMaterial> CreateMaterial(ITEM_MATERIAL table)
        {
            if (table == null)
            {
                return CommonResult<AbilityItemMaterial>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateMaterial: table is null.");
            }

            var ability = new AbilityItemMaterial();
            ability.Init(table);
            return CommonResult<AbilityItemMaterial>.Success(ability);
        }

        public static CommonResult<AbilityItemHero> CreateHero(
            string heroId,
            int itemLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return CommonResult<AbilityItemHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateHero: heroId is null or empty.");
            }

            var table = TB_ITEM_HERO.Get(heroId);
            if (table == null)
            {
                return CommonResult<AbilityItemHero>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_HERO not found: {heroId}");
            }

            return CreateHero(table, itemLevel);
        }

        public static CommonResult<AbilityItemHero> CreateHero(
            ITEM_HERO table,
            int itemLevel = 1)
        {
            if (table == null)
            {
                return CommonResult<AbilityItemHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateHero: table is null.");
            }

            var levelTable = resolveHeroLevelTable(table.ItemId, itemLevel);
            if (levelTable.IsFailure)
                return CommonResult<AbilityItemHero>.Failure(levelTable.Error!);

            var ability = new AbilityItemHero();
            ability.Init(table, levelTable.Value);
            return CommonResult<AbilityItemHero>.Success(ability);
        }

        public static CommonResult<AbilityItemEquip> CreateEquip(
            string itemId,
            string itemUid,
            int itemLevel = 1,
            string ownerUnitId = null,
            int ownerSlotNumber = 0)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return CommonResult<AbilityItemEquip>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: itemId is null or empty.");
            }

            var table = TB_ITEM_EQUIP.Get(itemId);
            if (table == null)
            {
                return CommonResult<AbilityItemEquip>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_EQUIP not found: {itemId}");
            }

            return CreateEquip(table, itemUid, itemLevel, ownerUnitId, ownerSlotNumber);
        }

        public static CommonResult<AbilityItemEquip> CreateEquip(
            ITEM_EQUIP table,
            string itemUid,
            int itemLevel = 1,
            string ownerUnitId = null,
            int ownerSlotNumber = 0)
        {
            if (table == null)
            {
                return CommonResult<AbilityItemEquip>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: table is null.");
            }

            if (string.IsNullOrWhiteSpace(itemUid))
            {
                return CommonResult<AbilityItemEquip>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: itemUid is null or empty.");
            }

            if (ownerSlotNumber < 0)
            {
                return CommonResult<AbilityItemEquip>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"AbilityItemFactory.CreateEquip: ownerSlotNumber is invalid: {ownerSlotNumber}");
            }

            var hasOwnerUnitId = !string.IsNullOrWhiteSpace(ownerUnitId);
            if ((ownerSlotNumber > 0 && !hasOwnerUnitId) || (ownerSlotNumber == 0 && hasOwnerUnitId))
            {
                return CommonResult<AbilityItemEquip>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateEquip: ownerUnitId/ownerSlotNumber must both be set or both be empty.");
            }

            var levelTable = resolveEquipLevelTable(table.ItemId, itemLevel);
            if (levelTable.IsFailure)
                return CommonResult<AbilityItemEquip>.Failure(levelTable.Error!);

            var ability = new AbilityItemEquip();
            ability.Init(table, levelTable.Value, itemUid);

            if (ownerSlotNumber > 0)
                ability.SetOwner(ownerUnitId, ownerSlotNumber);

            return CommonResult<AbilityItemEquip>.Success(ability);
        }

        static CommonResult<ITEM_CARD_LEVEL> resolveCardLevelTable(
            string itemId,
            int itemLevel)
        {
            var resolveLevel = resolveItemLevel(itemLevel, "AbilityItemFactory.CreateCard");
            if (resolveLevel.IsFailure)
                return CommonResult<ITEM_CARD_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findCardLevelRow(itemId, resolveLevel.Value);
            if (levelTable == null)
            {
                return CommonResult<ITEM_CARD_LEVEL>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_CARD_LEVEL not found: itemId={itemId}, level={resolveLevel.Value}");
            }

            return CommonResult<ITEM_CARD_LEVEL>.Success(levelTable);
        }

        static CommonResult<ITEM_HERO_LEVEL> resolveHeroLevelTable(
            string itemId,
            int itemLevel)
        {
            var resolveLevel = resolveItemLevel(itemLevel, "AbilityItemFactory.CreateHero");
            if (resolveLevel.IsFailure)
                return CommonResult<ITEM_HERO_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findHeroLevelRow(itemId, resolveLevel.Value);
            if (levelTable == null)
            {
                return CommonResult<ITEM_HERO_LEVEL>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_HERO_LEVEL not found: itemId={itemId}, level={resolveLevel.Value}");
            }

            return CommonResult<ITEM_HERO_LEVEL>.Success(levelTable);
        }

        static CommonResult<ITEM_EQUIP_LEVEL> resolveEquipLevelTable(
            string itemId,
            int itemLevel)
        {
            var resolveLevel = resolveItemLevel(itemLevel, "AbilityItemFactory.CreateEquip");
            if (resolveLevel.IsFailure)
                return CommonResult<ITEM_EQUIP_LEVEL>.Failure(resolveLevel.Error!);

            var levelTable = findEquipLevelRow(itemId, resolveLevel.Value);
            if (levelTable == null)
            {
                return CommonResult<ITEM_EQUIP_LEVEL>.Failure(
                    COMMON_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"ITEM_EQUIP_LEVEL not found: itemId={itemId}, level={resolveLevel.Value}");
            }

            return CommonResult<ITEM_EQUIP_LEVEL>.Success(levelTable);
        }

        static CommonResult<int> resolveItemLevel(int itemLevel, string context)
        {
            if (itemLevel < 1)
            {
                return CommonResult<int>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"{context}: itemLevel must be >= 1. actual={itemLevel}");
            }

            return CommonResult<int>.Success(itemLevel);
        }

        static ITEM_CARD_LEVEL findCardLevelRow(string itemId, int level)
        {
            var rows = TB_ITEM_CARD_LEVEL.GetByGroup(itemId);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && row.ItemLevel == level)
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
                if (row != null && row.ItemLevel == level)
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
                if (row != null && row.ItemLevel == level)
                    return row;
            }

            return null;
        }
    }
}
