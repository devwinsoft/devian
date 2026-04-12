using Devian.Domain.Game;

namespace Devian
{
    internal readonly struct ItemLevelUpCurrencyCost
    {
        public ItemLevelUpCurrencyCost(CURRENCY_TYPE currencyType, int amount)
        {
            CurrencyType = currencyType;
            Amount = amount;
        }

        public CURRENCY_TYPE CurrencyType { get; }
        public int Amount { get; }
    }

    internal readonly struct EquipLevelUpMaterialCost
    {
        public EquipLevelUpMaterialCost(string materialItemId, int amount)
        {
            MaterialItemId = materialItemId;
            Amount = amount;
        }

        public string MaterialItemId { get; }
        public int Amount { get; }
    }

    public sealed class AbilityItemEquip : AbilityItemBase
    {
        ITEM_EQUIP mTable = null;
        ITEM_EQUIP_LEVEL mLevelTable = null;
        string mItemUid = string.Empty;
        string mOwnerUnitId = string.Empty;
        EQUIP_SLOT_TYPE mOwnerSlotType = EQUIP_SLOT_TYPE.NONE;

        public string ItemUid => mItemUid;
        public override string ItemId => mTable?.item_id ?? string.Empty;
        public EQUIP_TYPE EquipType => mTable != null ? mTable.equip_type : default;
        public string OwnerUnitId => mOwnerUnitId;
        public EQUIP_SLOT_TYPE OwnerSlotType => mOwnerSlotType;
        public bool IsEquipped => mOwnerSlotType != EQUIP_SLOT_TYPE.NONE;

        public void Init(ITEM_EQUIP table, ITEM_EQUIP_LEVEL levelTable, string itemUid)
        {
            mTable = table;
            mLevelTable = levelTable;
            mItemUid = itemUid;

            if (levelTable == null)
                return;

            InitLevelStats(
                levelTable.item_level,
                levelTable.stat_type00, levelTable.stat_value00,
                levelTable.stat_type01, levelTable.stat_value01,
                levelTable.stat_type02, levelTable.stat_value02,
                levelTable.stat_type03, levelTable.stat_value03);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemEquip();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.mItemUid = mItemUid;
            c.mOwnerUnitId = mOwnerUnitId;
            c.mOwnerSlotType = mOwnerSlotType;
            c.CopyStatsFrom(this);
            return c;
        }

        public static bool IsSame(AbilityItemEquip left, AbilityItemEquip right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return !string.IsNullOrWhiteSpace(left.ItemUid)
                && left.ItemUid == right.ItemUid;
        }

        internal void SetOwner(string unitId, EQUIP_SLOT_TYPE slotType)
        {
            mOwnerUnitId = unitId;
            mOwnerSlotType = slotType;
        }

        internal void ClearOwner()
        {
            mOwnerUnitId = string.Empty;
            mOwnerSlotType = EQUIP_SLOT_TYPE.NONE;
        }

        internal GameResult _LevelUp()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip._LevelUp: equip is not initialized. item_id={ItemId}, itemUid={ItemUid}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextEquipLevelTable(ItemId, mLevelTable.item_level);
            if (nextLevelTable.IsFailure)
                return GameResult.Failure(nextLevelTable.Error!);

            var currentLevelTable = mLevelTable;
            var next = nextLevelTable.Value;
            ReplaceLevelStats(
                next.item_level,
                currentLevelTable.stat_type00, currentLevelTable.stat_value00,
                currentLevelTable.stat_type01, currentLevelTable.stat_value01,
                currentLevelTable.stat_type02, currentLevelTable.stat_value02,
                currentLevelTable.stat_type03, currentLevelTable.stat_value03,
                next.stat_type00, next.stat_value00,
                next.stat_type01, next.stat_value01,
                next.stat_type02, next.stat_value02,
                next.stat_type03, next.stat_value03);
            mLevelTable = next;
            return GameResult.Ok();
        }

        internal GameResult<EquipLevelUpMaterialCost> ResolveLevelUpMaterialCost()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult<EquipLevelUpMaterialCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip.ResolveLevelUpMaterialCost: equip is not initialized. item_id={ItemId}, itemUid={ItemUid}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextEquipLevelTable(ItemId, mLevelTable.item_level);
            if (nextLevelTable.IsFailure)
                return GameResult<EquipLevelUpMaterialCost>.Failure(nextLevelTable.Error!);

            if (mLevelTable.levelup_count < 0)
            {
                return GameResult<EquipLevelUpMaterialCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip.ResolveLevelUpMaterialCost: levelup_count must be >= 0. item_id={ItemId}, itemUid={ItemUid}, itemLevel={mLevelTable.item_level}, levelup_count={mLevelTable.levelup_count}");
            }

            if (mLevelTable.levelup_count == 0)
                return GameResult<EquipLevelUpMaterialCost>.Success(new EquipLevelUpMaterialCost(string.Empty, 0));

            if (string.IsNullOrWhiteSpace(mLevelTable.levelup_material))
            {
                return GameResult<EquipLevelUpMaterialCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip.ResolveLevelUpMaterialCost: levelup_material is empty. item_id={ItemId}, itemUid={ItemUid}, itemLevel={mLevelTable.item_level}");
            }

            var materialTable = TB_ITEM_MATERIAL.Get(mLevelTable.levelup_material);
            if (materialTable == null)
            {
                return GameResult<EquipLevelUpMaterialCost>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"AbilityItemEquip.ResolveLevelUpMaterialCost: ITEM_MATERIAL not found. item_id={ItemId}, itemUid={ItemUid}, itemLevel={mLevelTable.item_level}, materialItemId={mLevelTable.levelup_material}");
            }

            return GameResult<EquipLevelUpMaterialCost>.Success(
                new EquipLevelUpMaterialCost(materialTable.item_id, mLevelTable.levelup_count));
        }

        internal GameResult<ItemLevelUpCurrencyCost> ResolveLevelUpCurrencyCost()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult<ItemLevelUpCurrencyCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip.ResolveLevelUpCurrencyCost: equip is not initialized. item_id={ItemId}, itemUid={ItemUid}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextEquipLevelTable(ItemId, mLevelTable.item_level);
            if (nextLevelTable.IsFailure)
                return GameResult<ItemLevelUpCurrencyCost>.Failure(nextLevelTable.Error!);

            if (mLevelTable.levelup_price < 0)
            {
                return GameResult<ItemLevelUpCurrencyCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip.ResolveLevelUpCurrencyCost: levelup_price must be >= 0. item_id={ItemId}, itemUid={ItemUid}, itemLevel={mLevelTable.item_level}, levelup_price={mLevelTable.levelup_price}");
            }

            if (mLevelTable.levelup_price == 0)
                return GameResult<ItemLevelUpCurrencyCost>.Success(new ItemLevelUpCurrencyCost(default, 0));

            if (mLevelTable.levelup_currency == CURRENCY_TYPE.FREE
                || mLevelTable.levelup_currency == CURRENCY_TYPE.ADS)
            {
                return GameResult<ItemLevelUpCurrencyCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip.ResolveLevelUpCurrencyCost: levelup_currency is not spendable. item_id={ItemId}, itemUid={ItemUid}, itemLevel={mLevelTable.item_level}, levelup_currency={mLevelTable.levelup_currency}, levelup_price={mLevelTable.levelup_price}");
            }

            return GameResult<ItemLevelUpCurrencyCost>.Success(
                new ItemLevelUpCurrencyCost(mLevelTable.levelup_currency, mLevelTable.levelup_price));
        }
    }
}
