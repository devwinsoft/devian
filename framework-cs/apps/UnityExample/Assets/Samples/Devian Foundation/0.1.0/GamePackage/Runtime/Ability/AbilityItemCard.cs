using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemCard : AbilityItemBase
    {
        ITEM_CARD mTable = null;
        ITEM_CARD_LEVEL mLevelTable = null;

        public override string ItemId => mTable?.item_id ?? string.Empty;

        public void Init(ITEM_CARD table, ITEM_CARD_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

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
            var c = new AbilityItemCard();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            return c;
        }

        internal GameResult _LevelUp()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemCard._LevelUp: card is not initialized. item_id={ItemId}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextCardLevelTable(ItemId, mLevelTable.item_level);
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

        internal GameResult<int> ResolveLevelUpCost()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult<int>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemCard.ResolveLevelUpCost: card is not initialized. item_id={ItemId}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextCardLevelTable(ItemId, mLevelTable.item_level);
            if (nextLevelTable.IsFailure)
                return GameResult<int>.Failure(nextLevelTable.Error!);

            return GameResult<int>.Success(mLevelTable.levelup_count);
        }

        internal GameResult<ItemLevelUpCurrencyCost> ResolveLevelUpCurrencyCost()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult<ItemLevelUpCurrencyCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemCard.ResolveLevelUpCurrencyCost: card is not initialized. item_id={ItemId}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextCardLevelTable(ItemId, mLevelTable.item_level);
            if (nextLevelTable.IsFailure)
                return GameResult<ItemLevelUpCurrencyCost>.Failure(nextLevelTable.Error!);

            if (mLevelTable.levelup_price < 0)
            {
                return GameResult<ItemLevelUpCurrencyCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemCard.ResolveLevelUpCurrencyCost: levelup_price must be >= 0. item_id={ItemId}, itemLevel={mLevelTable.item_level}, levelup_price={mLevelTable.levelup_price}");
            }

            if (mLevelTable.levelup_price == 0)
                return GameResult<ItemLevelUpCurrencyCost>.Success(new ItemLevelUpCurrencyCost(default, 0));

            if (mLevelTable.levelup_currency == CURRENCY_TYPE.FREE
                || mLevelTable.levelup_currency == CURRENCY_TYPE.ADS)
            {
                return GameResult<ItemLevelUpCurrencyCost>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemCard.ResolveLevelUpCurrencyCost: levelup_currency is not spendable. item_id={ItemId}, itemLevel={mLevelTable.item_level}, levelup_currency={mLevelTable.levelup_currency}, levelup_price={mLevelTable.levelup_price}");
            }

            return GameResult<ItemLevelUpCurrencyCost>.Success(
                new ItemLevelUpCurrencyCost(mLevelTable.levelup_currency, mLevelTable.levelup_price));
        }
    }
}
