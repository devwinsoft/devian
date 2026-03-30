using System.Collections.Generic;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public static class AbilityItemFactory
    {
        public static CommonResult<AbilityItemCard> CreateCard(string itemId, IReadOnlyDictionary<STAT_TYPE, int> stats = null)
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

            return CreateCard(table, stats);
        }

        public static CommonResult<AbilityItemCard> CreateCard(ITEM_CARD table, IReadOnlyDictionary<STAT_TYPE, int> stats = null)
        {
            if (table == null)
            {
                return CommonResult<AbilityItemCard>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateCard: table is null.");
            }

            var ability = new AbilityItemCard();
            ability.Init(table);
            AbilityFactoryStatUtil.ApplyStats(ability, stats);
            return CommonResult<AbilityItemCard>.Success(ability);
        }

        public static CommonResult<AbilityItemMaterial> CreateMaterial(string itemId, IReadOnlyDictionary<STAT_TYPE, int> stats = null)
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

            return CreateMaterial(table, stats);
        }

        public static CommonResult<AbilityItemMaterial> CreateMaterial(ITEM_MATERIAL table, IReadOnlyDictionary<STAT_TYPE, int> stats = null)
        {
            if (table == null)
            {
                return CommonResult<AbilityItemMaterial>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateMaterial: table is null.");
            }

            var ability = new AbilityItemMaterial();
            ability.Init(table);
            AbilityFactoryStatUtil.ApplyStats(ability, stats);
            return CommonResult<AbilityItemMaterial>.Success(ability);
        }

        public static CommonResult<AbilityItemHero> CreateHero(
            string heroId,
            IReadOnlyDictionary<STAT_TYPE, int> stats = null,
            IReadOnlyDictionary<int, AbilityItemEquip> equips = null,
            bool cloneEquips = false)
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

            return CreateHero(table, stats, equips, cloneEquips);
        }

        public static CommonResult<AbilityItemHero> CreateHero(
            ITEM_HERO table,
            IReadOnlyDictionary<STAT_TYPE, int> stats = null,
            IReadOnlyDictionary<int, AbilityItemEquip> equips = null,
            bool cloneEquips = false)
        {
            if (table == null)
            {
                return CommonResult<AbilityItemHero>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "AbilityItemFactory.CreateHero: table is null.");
            }

            var ability = new AbilityItemHero();
            ability.Init(table);
            AbilityFactoryStatUtil.ApplyStats(ability, stats);
            var attach = attachHeroEquips(ability, equips, cloneEquips);
            if (attach.IsFailure)
                return CommonResult<AbilityItemHero>.Failure(attach.Error!);

            return CommonResult<AbilityItemHero>.Success(ability);
        }

        public static CommonResult<AbilityItemEquip> CreateEquip(
            string itemId,
            string itemUid,
            IReadOnlyDictionary<STAT_TYPE, int> stats = null,
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

            return CreateEquip(table, itemUid, stats, ownerUnitId, ownerSlotNumber);
        }

        public static CommonResult<AbilityItemEquip> CreateEquip(
            ITEM_EQUIP table,
            string itemUid,
            IReadOnlyDictionary<STAT_TYPE, int> stats = null,
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

            var ability = new AbilityItemEquip();
            ability.Init(table, itemUid);
            AbilityFactoryStatUtil.ApplyStats(ability, stats);

            if (ownerSlotNumber > 0)
                ability.SetOwner(ownerUnitId, ownerSlotNumber);

            return CommonResult<AbilityItemEquip>.Success(ability);
        }

        static CommonResult attachHeroEquips(
            AbilityItemHero ability,
            IReadOnlyDictionary<int, AbilityItemEquip> equips,
            bool cloneEquips)
        {
            if (ability == null || equips == null)
                return CommonResult.Ok();

            foreach (var kv in equips)
            {
                if (kv.Value == null)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityItemFactory.CreateHero: equips[{kv.Key}] is null.");
                }

                var equip = cloneEquips
                    ? (AbilityItemEquip)kv.Value.Clone()
                    : kv.Value;

                if (cloneEquips)
                    equip.ClearOwner();

                if (!ability.Equip(equip, kv.Key))
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"AbilityItemFactory.CreateHero: invalid equip slot {kv.Key}.");
                }
            }

            return CommonResult.Ok();
        }
    }
}
