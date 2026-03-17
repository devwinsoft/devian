using System.Collections.Generic;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogChest : ShopCatalogBase
    {
        readonly ShopCatalogChestStorageData _chestStorageData;
        int _level;
        int _currentExp;

        internal readonly struct ChestPurchaseRuntime
        {
            public ChestPurchaseRuntime(string rewardGroupId, int rewardAmount, int gainExp)
            {
                RewardGroupId = rewardGroupId ?? string.Empty;
                RewardAmount = rewardAmount < 1 ? 1 : rewardAmount;
                GainExp = gainExp > 0 ? gainExp : 0;
            }

            public string RewardGroupId { get; }
            public int RewardAmount { get; }
            public int GainExp { get; }
        }

        public ShopCatalogChest(
            ShopStorage storage = null,
            ShopCatalogChestStorageData storageData = null,
            SHOP_CATALOG catalogConfig = null)
            : this(storage, storageData, products: null, catalogConfig)
        {
        }

        internal ShopCatalogChest(
            ShopStorage storage,
            ShopCatalogChestStorageData storageData,
            IReadOnlyList<ShopProductBase> products,
            SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.CHEST, storage, storageData, catalogConfig, products)
        {
            _chestStorageData = storageData ?? new ShopCatalogChestStorageData();
            _level = _chestStorageData.level > 0 ? _chestStorageData.level : 1;
            _currentExp = _chestStorageData.currentExp > 0 ? _chestStorageData.currentExp : 0;
        }

        public int Level => _level;
        public int CurrentExp => IsMaxLevel ? 0 : (_currentExp > 0 ? _currentExp : 0);
        public int MaxExp
        {
            get
            {
                if (IsMaxLevel)
                    return 0;

                var row = GetCurrentLevelRow();
                return row != null && row.MaxExp > 0
                    ? row.MaxExp
                    : 0;
            }
        }

        internal int MaxLevel
        {
            get
            {
                getLevelBounds(out _, out var maxLevel);
                return maxLevel;
            }
        }

        internal bool IsMaxLevel => MaxLevel > 0 && _level >= MaxLevel;

        public void LevelUp()
        {
            if (tryGetNextRow(_level, out var nextRow) && nextRow != null)
                _level = nextRow.Level;

            normalizeProgressionState(persistState: Storage != null);
        }

        protected override void onInitialize()
        {
            normalizeProgressionState(persistState: Storage != null);
        }

        internal override CommonResult<bool> SyncRuntimeState(bool requireServerTime)
        {
            var didMutate = normalizeProgressionState(persistState: Storage != null);
            return CommonResult<bool>.Success(didMutate);
        }

        internal SHOP_CATALOG_CHEST GetCurrentLevelRow()
        {
            return TB_SHOP_CATALOG_CHEST.Get(_level) ?? findClosestRow(_level);
        }

        internal CommonResult<ChestPurchaseRuntime> ResolvePurchaseRuntime(ShopProductChest product)
        {
            if (product == null)
            {
                return CommonResult<ChestPurchaseRuntime>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    "Chest product is null.");
            }

            normalizeProgressionState(persistState: false);

            var row = GetCurrentLevelRow();
            if (row == null)
            {
                return CommonResult<ChestPurchaseRuntime>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"SHOP_CATALOG_CHEST row not found: level={_level}");
            }

            string rewardGroupId;
            int gainExp;
            switch (product.ChestType)
            {
                case SHOP_PRODUCT_CHEST_TYPE.ADS:
                    rewardGroupId = row.RewardAds;
                    gainExp = IsMaxLevel ? 0 : row.AdsExp;
                    break;
                case SHOP_PRODUCT_CHEST_TYPE.ONE:
                    rewardGroupId = row.RewardPaid01;
                    gainExp = IsMaxLevel ? 0 : row.GainExp01;
                    break;
                case SHOP_PRODUCT_CHEST_TYPE.TEN:
                    rewardGroupId = row.RewardPaid10;
                    gainExp = IsMaxLevel ? 0 : row.GainExp10;
                    break;
                default:
                    return CommonResult<ChestPurchaseRuntime>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"Unsupported chest type: shopId={product.ShopId}, chestType={product.ChestType}");
            }

            if (string.IsNullOrWhiteSpace(rewardGroupId))
            {
                return CommonResult<ChestPurchaseRuntime>.Failure(
                    COMMON_ERROR_TYPE.SHOP_REWARD_GROUP_EMPTY,
                    $"Chest reward group is empty: shopId={product.ShopId}, level={row.Level}, chestType={product.ChestType}");
            }

            return CommonResult<ChestPurchaseRuntime>.Success(
                new ChestPurchaseRuntime(rewardGroupId.Trim(), product.Amount, gainExp));
        }

        internal bool AddExp(int exp)
        {
            if (exp <= 0)
            {
                var didNormalize = normalizeProgressionState(persistState: Storage != null);
                return didNormalize;
            }

            return normalizeProgressionState(exp, persistState: Storage != null);
        }

        bool normalizeProgressionState(int pendingExp = 0, bool persistState = false)
        {
            var previousLevel = _level;
            var previousCurrentExp = _currentExp;

            if (!getLevelBounds(out var minLevel, out var maxLevel))
            {
                _level = 1;
                _currentExp = 0;
            }
            else
            {
                var normalizedLevel = _level > 0 ? _level : 1;
                if (normalizedLevel < 1)
                    normalizedLevel = 1;
                var currentRow = TB_SHOP_CATALOG_CHEST.Get(normalizedLevel) ?? findClosestRow(normalizedLevel);
                if (currentRow == null)
                {
                    _level = 1;
                    _currentExp = 0;
                }
                else
                {
                    normalizedLevel = currentRow.Level;
                    if (normalizedLevel < 1)
                        normalizedLevel = 1;
                    if (normalizedLevel < minLevel)
                        normalizedLevel = minLevel;
                    if (normalizedLevel > maxLevel)
                        normalizedLevel = maxLevel;

                    var normalizedCurrentExp = _currentExp > 0 ? _currentExp : 0;
                    if (normalizedLevel >= maxLevel)
                    {
                        normalizedLevel = maxLevel;
                        normalizedCurrentExp = 0;
                    }
                    else
                    {
                        if (pendingExp > 0)
                        {
                            var nextExp = (long)normalizedCurrentExp + pendingExp;
                            normalizedCurrentExp = nextExp > int.MaxValue
                                ? int.MaxValue
                                : (int)nextExp;
                        }

                        while (true)
                        {
                            currentRow = TB_SHOP_CATALOG_CHEST.Get(normalizedLevel) ?? findClosestRow(normalizedLevel);
                            if (currentRow == null)
                            {
                                normalizedLevel = maxLevel;
                                normalizedCurrentExp = 0;
                                break;
                            }

                            if (normalizedLevel >= maxLevel)
                            {
                                normalizedLevel = maxLevel;
                                normalizedCurrentExp = 0;
                                break;
                            }

                            var requiredExp = currentRow.MaxExp;
                            if (requiredExp <= 0)
                            {
                                normalizedCurrentExp = 0;
                                break;
                            }

                            if (normalizedCurrentExp < requiredExp)
                                break;

                            normalizedCurrentExp -= requiredExp;
                            if (!tryGetNextRow(normalizedLevel, out var nextRow) || nextRow == null)
                            {
                                normalizedLevel = maxLevel;
                                normalizedCurrentExp = 0;
                                break;
                            }

                            normalizedLevel = nextRow.Level;
                            if (normalizedLevel >= maxLevel)
                            {
                                normalizedLevel = maxLevel;
                                normalizedCurrentExp = 0;
                                break;
                            }
                        }
                    }

                    _level = normalizedLevel;
                    _currentExp = normalizedLevel >= maxLevel
                        ? 0
                        : (normalizedCurrentExp > 0 ? normalizedCurrentExp : 0);
                }
            }

            var didMutate = previousLevel != _level || previousCurrentExp != _currentExp;
            if (persistState)
                persistProgressionState();

            return didMutate;
        }

        void persistProgressionState()
        {
            var normalizedLevel = _level > 0 ? _level : 1;
            var normalizedCurrentExp = IsMaxLevel ? 0 : (_currentExp > 0 ? _currentExp : 0);

            _level = normalizedLevel;
            _currentExp = normalizedCurrentExp;

            if (Storage != null)
            {
                Storage.SetChestLevel(normalizedLevel);
                Storage.SetChestCurrentExp(normalizedCurrentExp);
            }

            _chestStorageData.level = normalizedLevel;
            _chestStorageData.currentExp = normalizedCurrentExp;
        }

        static SHOP_CATALOG_CHEST findClosestRow(int level)
        {
            level = level > 0 ? level : 1;
            var rows = TB_SHOP_CATALOG_CHEST.GetAll();
            SHOP_CATALOG_CHEST closestHigher = null;
            SHOP_CATALOG_CHEST closestLower = null;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Level < 1)
                    continue;

                if (row.Level >= level)
                {
                    if (closestHigher == null || row.Level < closestHigher.Level)
                        closestHigher = row;
                    continue;
                }

                if (closestLower == null || row.Level > closestLower.Level)
                    closestLower = row;
            }

            return closestHigher ?? closestLower;
        }

        static bool tryGetNextRow(int currentLevel, out SHOP_CATALOG_CHEST nextRow)
        {
            currentLevel = currentLevel > 0 ? currentLevel : 1;
            nextRow = null;
            var rows = TB_SHOP_CATALOG_CHEST.GetAll();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Level < 1 || row.Level <= currentLevel)
                    continue;

                if (nextRow == null || row.Level < nextRow.Level)
                    nextRow = row;
            }

            return nextRow != null;
        }

        static bool getLevelBounds(out int minLevel, out int maxLevel)
        {
            minLevel = 1;
            maxLevel = 0;

            var rows = TB_SHOP_CATALOG_CHEST.GetAll();
            if (rows == null || rows.Count <= 0)
                return false;

            var hasAny = false;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Level < 1)
                    continue;

                if (!hasAny)
                {
                    minLevel = row.Level > 0 ? row.Level : 1;
                    maxLevel = row.Level;
                    hasAny = true;
                    continue;
                }

                if (row.Level < minLevel)
                    minLevel = row.Level;
                if (row.Level > maxLevel)
                    maxLevel = row.Level;
            }

            return hasAny;
        }
    }
}
