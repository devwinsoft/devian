using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class ShopCatalogDaily : ShopCatalogBase
    {
        const int DailySelectableProductCount = 5;
        const int DailyDiscountProductCount = 3;
        internal const int MaxManualRefreshCountPerDay = 5;
        long _remainAdsRefreshTimeMs;
        long _manualRefreshRemainTimeMs;
        int _manualRefreshRemainCount = MaxManualRefreshCountPerDay;

        readonly struct ManualRefreshState
        {
            public ManualRefreshState(
                long serverNowUtcMs,
                long nextManualRefreshUtcMs,
                int storedManualRefreshRemainCount,
                long remainManualRefreshTimeMs,
                int remainManualRefreshCount,
                bool shouldNormalizeStoredState)
            {
                ServerNowUtcMs = serverNowUtcMs > 0L ? serverNowUtcMs : 0L;
                NextManualRefreshUtcMs = nextManualRefreshUtcMs > 0L ? nextManualRefreshUtcMs : 0L;
                StoredManualRefreshRemainCount = normalizeManualRefreshRemainCount(storedManualRefreshRemainCount);
                ManualRefreshRemainTimeMs = remainManualRefreshTimeMs > 0L ? remainManualRefreshTimeMs : 0L;
                ManualRefreshRemainCount = normalizeManualRefreshRemainCount(remainManualRefreshCount);
                ShouldNormalizeStoredState = shouldNormalizeStoredState;
            }

            public long ServerNowUtcMs { get; }
            public long NextManualRefreshUtcMs { get; }
            public int StoredManualRefreshRemainCount { get; }
            public long ManualRefreshRemainTimeMs { get; }
            public int ManualRefreshRemainCount { get; }
            public bool ShouldNormalizeStoredState { get; }
        }

        public ShopCatalogDaily()
            : this(storage: null, storageData: null, products: null, catalogConfig: null)
        {
        }

        public ShopCatalogDaily(
            ShopStorage storage,
            ShopCatalogDailyStorageData storageData,
            SHOP_CATALOG catalogConfig = null)
            : this(storage, storageData, products: null, catalogConfig)
        {
        }

        internal ShopCatalogDaily(IReadOnlyList<ShopProductBase> products, SHOP_CATALOG catalogConfig = null)
            : this(storage: null, storageData: null, products: products, catalogConfig)
        {
        }

        internal ShopCatalogDaily(
            ShopStorage storage,
            ShopCatalogDailyStorageData storageData,
            IReadOnlyList<ShopProductBase> products,
            SHOP_CATALOG catalogConfig)
            : base(SHOP_CATALOG_TYPE.DAILY, storage, storageData, catalogConfig, products)
        {
        }

        ShopCatalogDailyStorageData DailyStorage => StorageData as ShopCatalogDailyStorageData;
        public long RemainAdsRefreshTimeMs => _remainAdsRefreshTimeMs > 0L ? _remainAdsRefreshTimeMs : 0L;
        public long ManualRefreshRemainTimeMs => _manualRefreshRemainTimeMs > 0L ? _manualRefreshRemainTimeMs : 0L;
        public int ManualRefreshRemainCount => normalizeManualRefreshRemainCount(_manualRefreshRemainCount);

        internal void SetRemainAdsRefreshTimeMs(long remainTimeMs)
        {
            _remainAdsRefreshTimeMs = remainTimeMs > 0L ? remainTimeMs : 0L;
        }

        internal void SetManualRefreshState(long remainTimeMs, int remainCount)
        {
            _manualRefreshRemainTimeMs = remainTimeMs > 0L ? remainTimeMs : 0L;
            _manualRefreshRemainCount = normalizeManualRefreshRemainCount(remainCount);
        }

        public override async Task<GameResult> RefreshByAdsAsync(CancellationToken ct = default)
        {
            if (!IsInitialized)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "ShopCatalogDaily is not initialized.");
            }

            if (IsLocked)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "Daily shop catalog is locked.");
            }

            var evaluated = evaluateManualRefreshState(requireServerTime: true);
            if (evaluated.IsFailure)
                return GameResult.Failure(evaluated.Error!);

            var manualState = evaluated.Value;
            SetManualRefreshState(
                manualState.ManualRefreshRemainTimeMs,
                manualState.ManualRefreshRemainCount);

            if (manualState.ManualRefreshRemainCount <= 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.SHOP_ITEM_DAILY_MANUAL_REFRESH_COUNT_EXHAUSTED,
                    $"Daily manual refresh count exhausted: remainCount={manualState.ManualRefreshRemainCount}, nextRefreshUtcMs={manualState.NextManualRefreshUtcMs}");
            }

            var adsManager = AdsManager.Instance;
            var show = await adsManager.ShowAsync(ct);
            if (show.IsFailure)
            {
                var details = show.Error != null
                    ? $"inner={show.Error.Code}:{show.Error.Message}"
                    : "inner=unknown";
                return GameResult.Failure(
                    GAME_ERROR_TYPE.SHOP_ADS_SHOW_FAILED,
                    $"Daily manual refresh ad show failed: {details}");
            }

            ClearRuntimeStateForRefresh(clearAdsFreeRemainState: false);
            RefreshProducts();
            applyManualRefreshSuccess(manualState.ServerNowUtcMs, manualState);
            ApplyNextAutoRefreshUtcMs(manualState.ServerNowUtcMs);

            try
            {
                var save = await SaveDataManager.Instance.SaveGameStorageAsync(
                    saveCloud: false,
                    ct);
                if (save.IsFailure)
                    Debug.LogWarning($"[ShopCatalogDaily] Local save failed after manual refresh: {save.Error}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShopCatalogDaily] Local save threw after manual refresh: {ex.Message}");
            }

            return GameResult.Ok();
        }

        protected override IReadOnlyList<ShopProductBase> onRefresh()
        {
            if (PrebuiltProducts != null)
                return PrebuiltProducts;

            return loadOrCreateDailyProducts();
        }

        internal override GameResult<bool> SyncRuntimeState(bool requireServerTime)
        {
            var evaluated = evaluateManualRefreshState(requireServerTime);
            if (evaluated.IsFailure)
                return GameResult<bool>.Failure(evaluated.Error!);

            var state = evaluated.Value;
            if (state.ShouldNormalizeStoredState && DailyStorage != null)
            {
                DailyStorage.manualRefreshUtcMs = state.NextManualRefreshUtcMs;
                DailyStorage.manualRefreshRemainCount = state.ManualRefreshRemainCount;
            }

            SetManualRefreshState(
                state.ManualRefreshRemainTimeMs,
                state.ManualRefreshRemainCount);

            return GameResult<bool>.Success(state.ShouldNormalizeStoredState);
        }

        GameResult<ManualRefreshState> evaluateManualRefreshState(bool requireServerTime)
        {
            var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
            if (serverNowUtcMs <= 0L)
            {
                if (requireServerTime)
                {
                    return GameResult<ManualRefreshState>.Failure(
                        GAME_ERROR_TYPE.GAME_SERVER_TIME_UNAVAILABLE,
                        "Server time is invalid.");
                }

                return GameResult<ManualRefreshState>.Success(
                    new ManualRefreshState(
                        0L,
                        0L,
                        0,
                        ManualRefreshRemainTimeMs,
                        ManualRefreshRemainCount,
                        false));
            }

            var nextManualRefreshUtcMs = DailyStorage?.manualRefreshUtcMs ?? 0L;
            var storedManualRefreshRemainCount = DailyStorage?.manualRefreshRemainCount ?? MaxManualRefreshCountPerDay;
            var remainManualRefreshCount = normalizeManualRefreshRemainCount(storedManualRefreshRemainCount);
            var shouldNormalizeStoredState = false;
            if (nextManualRefreshUtcMs <= serverNowUtcMs)
            {
                shouldNormalizeStoredState =
                    nextManualRefreshUtcMs > 0L
                    || remainManualRefreshCount != MaxManualRefreshCountPerDay;
                nextManualRefreshUtcMs = 0L;
                remainManualRefreshCount = MaxManualRefreshCountPerDay;
            }
            else if (remainManualRefreshCount != storedManualRefreshRemainCount)
            {
                shouldNormalizeStoredState = true;
            }

            var remainManualRefreshTimeMs = GetRemainingToNextRefreshMs(
                serverNowUtcMs,
                nextManualRefreshUtcMs);

            return GameResult<ManualRefreshState>.Success(
                new ManualRefreshState(
                    serverNowUtcMs,
                    nextManualRefreshUtcMs,
                    remainManualRefreshCount,
                    remainManualRefreshTimeMs,
                    remainManualRefreshCount,
                    shouldNormalizeStoredState));
        }

        void applyManualRefreshSuccess(long serverNowUtcMs, ManualRefreshState state)
        {
            if (DailyStorage == null || serverNowUtcMs <= 0L)
                return;

            var nextManualRefreshUtcMs = state.NextManualRefreshUtcMs;
            var remainManualRefreshCount = state.ManualRefreshRemainCount;
            if (nextManualRefreshUtcMs <= serverNowUtcMs)
            {
                nextManualRefreshUtcMs = GetNextRefreshUtcMs(serverNowUtcMs, MillisecondsPerDay);
                remainManualRefreshCount = MaxManualRefreshCountPerDay;
            }

            if (remainManualRefreshCount > 0)
                remainManualRefreshCount--;

            DailyStorage.manualRefreshUtcMs = nextManualRefreshUtcMs;
            DailyStorage.manualRefreshRemainCount = remainManualRefreshCount;
            SetManualRefreshState(
                GetRemainingToNextRefreshMs(serverNowUtcMs, nextManualRefreshUtcMs),
                remainManualRefreshCount);
        }

        IReadOnlyList<ShopProductBase> loadOrCreateDailyProducts()
        {
            var rows = TB_SHOP_ITEM_DAILY.GetAll();
            var dynamicProducts = tryBuildDailyProductsFromStorage(rows, out var storedProducts)
                ? storedProducts
                : createDailyProductsFromRows(rows, DailySelectableProductCount, DailyDiscountProductCount);

            normalizeDailyDynamicStorage(dynamicProducts);
            return composeDailyProducts(dynamicProducts, rows);
        }

        bool tryBuildDailyProductsFromStorage(
            IReadOnlyList<SHOP_ITEM_DAILY> rows,
            out IReadOnlyList<ShopProductBase> products)
        {
            products = null;
            if (DailyStorage == null)
                return false;

            var states = DailyStorage.dailyCatalogProducts;
            if (states == null || states.Count <= 0)
                return false;

            var list = new List<ShopProductBase>(states.Count);
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null)
                    return false;

                var normalizedShopId = NormalizeShopId(state.shopId);
                if (string.IsNullOrEmpty(normalizedShopId))
                    return false;

                if (!seenShopIds.Add(normalizedShopId))
                    return false;

                var row = TB_SHOP_ITEM_DAILY.Get(normalizedShopId);
                if (row == null)
                    return false;

                if (isAdsOrFreeCurrencyType(row.currency_type))
                    return false;

                var product = ShopProductFactory.CreateDailyProduct(row, normalizeDiscountType(state.discountType));
                if (product == null)
                    return false;

                product.SetRemainCount(state.remainCount);
                list.Add(product);
            }

            if (list.Count != DailySelectableProductCount)
                return false;

            products = list;
            return true;
        }

        void normalizeDailyDynamicStorage(IReadOnlyList<ShopProductBase> dynamicProducts)
        {
            if (DailyStorage == null)
                return;

            DailyStorage.dailyCatalogProducts = createDailyProductStates(dynamicProducts);

            if (dynamicProducts == null || dynamicProducts.Count <= 0)
                return;

            for (var i = 0; i < dynamicProducts.Count; i++)
            {
                var normalizedShopId = NormalizeShopId(dynamicProducts[i]?.shop_id);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                Storage.RemoveProductRemainCount(CatalogType, normalizedShopId);
            }
        }

        IReadOnlyList<ShopProductBase> composeDailyProducts(
            IReadOnlyList<ShopProductBase> dynamicProducts,
            IReadOnlyList<SHOP_ITEM_DAILY> rows)
        {
            var list = new List<ShopProductBase>((dynamicProducts?.Count ?? 0) + 4);
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);

            if (dynamicProducts != null && dynamicProducts.Count > 0)
            {
                for (var i = 0; i < dynamicProducts.Count; i++)
                {
                    var product = dynamicProducts[i];
                    if (product == null)
                        continue;

                    var normalizedShopId = NormalizeShopId(product.shop_id);
                    if (string.IsNullOrEmpty(normalizedShopId) || !seenShopIds.Add(normalizedShopId))
                        continue;

                    list.Add(product);
                }
            }

            appendDailyFixedAdsFreeProducts(list, rows, seenShopIds);
            return list;
        }

        static List<ShopDailyProductState> createDailyProductStates(IReadOnlyList<ShopProductBase> products)
        {
            var result = new List<ShopDailyProductState>(products?.Count ?? 0);
            if (products == null || products.Count <= 0)
                return result;

            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < products.Count; i++)
            {
                var product = products[i];
                if (!isDailyStoredProduct(product))
                    continue;

                var normalizedShopId = NormalizeShopId(product.shop_id);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                result.Add(new ShopDailyProductState
                {
                    shopId = normalizedShopId,
                    discountType = product.DiscountType,
                    remainCount = product.RemainCount,
                });
            }

            return result;
        }

        static IReadOnlyList<ShopProductBase> createDailyProductsFromRows(
            IReadOnlyList<SHOP_ITEM_DAILY> rows,
            int targetCount,
            int targetDiscountCount)
        {
            var selectedRows = selectDailyRows(rows, targetCount);
            var discountTypesByShopId = selectDailyDiscountTypes(selectedRows, targetDiscountCount);
            var products = new List<ShopProductBase>(selectedRows.Count);
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < selectedRows.Count; i++)
            {
                var row = selectedRows[i];
                if (row == null)
                    continue;

                var normalizedShopId = NormalizeShopId(row.shop_item_id);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                var discountType = SHOP_DISCOUNT_TYPE.NONE;
                discountTypesByShopId.TryGetValue(normalizedShopId, out discountType);

                var product = ShopProductFactory.CreateDailyProduct(row, discountType);
                if (product != null)
                    products.Add(product);
            }

            return products;
        }

        static List<SHOP_ITEM_DAILY> selectDailyRows(IReadOnlyList<SHOP_ITEM_DAILY> rows, int targetCount)
        {
            var mandatoryRows = new List<SHOP_ITEM_DAILY>();
            var weightedRows = new List<SHOP_ITEM_DAILY>();
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);

            if (rows == null || rows.Count <= 0)
                return mandatoryRows;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.shop_item_id))
                    continue;

                if (isAdsOrFreeCurrencyType(row.currency_type))
                    continue;

                var normalizedShopId = row.shop_item_id.Trim();
                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                var selectRate = sanitizeDailySelectRate(row.select_rate);
                if (selectRate < 0f)
                {
                    mandatoryRows.Add(row);
                    continue;
                }

                if (selectRate > 0f)
                    weightedRows.Add(row);
            }

            var selectedRows = new List<SHOP_ITEM_DAILY>(Math.Max(targetCount, mandatoryRows.Count));
            selectedRows.AddRange(mandatoryRows);
            if (targetCount <= 0 || selectedRows.Count >= targetCount || weightedRows.Count <= 0)
                return selectedRows;

            var remainingCount = targetCount - selectedRows.Count;
            for (var i = 0; i < remainingCount && weightedRows.Count > 0; i++)
            {
                if (!trySelectDailyRow(weightedRows, out var selectedRow) || selectedRow == null)
                    break;

                selectedRows.Add(selectedRow);
                weightedRows.Remove(selectedRow);
            }

            return selectedRows;
        }

        static bool trySelectDailyRow(IReadOnlyList<SHOP_ITEM_DAILY> rows, out SHOP_ITEM_DAILY selectedRow)
        {
            selectedRow = null;

            var totalRate = 0f;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!isSelectableDailyRow(row))
                    continue;

                totalRate += row.select_rate;
            }

            if (!(totalRate > 0f))
                return false;

            var roll = UnityEngine.Random.value * totalRate;
            var cumulative = 0f;
            SHOP_ITEM_DAILY lastRow = null;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!isSelectableDailyRow(row))
                    continue;

                cumulative += row.select_rate;
                lastRow = row;
                if (roll < cumulative)
                {
                    selectedRow = row;
                    return true;
                }
            }

            if (lastRow == null)
                return false;

            selectedRow = lastRow;
            return true;
        }

        static bool isSelectableDailyRow(SHOP_ITEM_DAILY row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.shop_item_id))
                return false;

            if (isAdsOrFreeCurrencyType(row.currency_type))
                return false;

            return sanitizeDailySelectRate(row.select_rate) > 0f;
        }

        void appendDailyFixedAdsFreeProducts(
            List<ShopProductBase> products,
            IReadOnlyList<SHOP_ITEM_DAILY> rows,
            HashSet<string> seenShopIds)
        {
            if (products == null || rows == null || rows.Count <= 0)
                return;

            seenShopIds ??= new HashSet<string>(StringComparer.Ordinal);
            if (seenShopIds.Count <= 0 && products.Count > 0)
            {
                for (var i = 0; i < products.Count; i++)
                {
                    var existingId = NormalizeShopId(products[i]?.shop_id);
                    if (!string.IsNullOrEmpty(existingId))
                        seenShopIds.Add(existingId);
                }
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || !isAdsOrFreeCurrencyType(row.currency_type))
                    continue;

                var normalizedShopId = NormalizeShopId(row.shop_item_id);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                var product = ShopProductFactory.CreateDailyProduct(row, SHOP_DISCOUNT_TYPE.NONE);
                if (product != null)
                {
                    ApplyStoredProductState(product);
                    products.Add(product);
                }
            }
        }

        static float sanitizeDailySelectRate(float selectRate)
        {
            if (float.IsNaN(selectRate) || float.IsInfinity(selectRate))
                return 0f;

            return selectRate;
        }

        static Dictionary<string, SHOP_DISCOUNT_TYPE> selectDailyDiscountTypes(
            IReadOnlyList<SHOP_ITEM_DAILY> selectedRows,
            int targetDiscountCount)
        {
            var discountTypesByShopId = new Dictionary<string, SHOP_DISCOUNT_TYPE>(StringComparer.Ordinal);
            if (selectedRows == null || selectedRows.Count <= 0 || targetDiscountCount <= 0)
                return discountTypesByShopId;

            var candidates = new List<SHOP_ITEM_DAILY>(selectedRows.Count);
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < selectedRows.Count; i++)
            {
                var row = selectedRows[i];
                var normalizedShopId = NormalizeShopId(row?.shop_item_id);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                if (!hasSelectableDiscountRate(row))
                    continue;

                candidates.Add(row);
            }

            var discountSelectionCount = Math.Min(targetDiscountCount, candidates.Count);
            for (var i = 0; i < discountSelectionCount && candidates.Count > 0; i++)
            {
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var row = candidates[index];
                candidates.RemoveAt(index);

                var normalizedShopId = NormalizeShopId(row?.shop_item_id);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                discountTypesByShopId[normalizedShopId] = selectDailyDiscountType(row);
            }

            return discountTypesByShopId;
        }

        static SHOP_DISCOUNT_TYPE selectDailyDiscountType(SHOP_ITEM_DAILY row)
        {
            if (row == null)
                return SHOP_DISCOUNT_TYPE.NONE;

            var rate10 = sanitizeDiscountRate(row.discount_rate10_per);
            var rate20 = sanitizeDiscountRate(row.discount_rate20_per);
            var rate30 = sanitizeDiscountRate(row.discount_rate30_per);
            var rate50 = sanitizeDiscountRate(row.discount_rate50_per);
            var totalRate = rate10 + rate20 + rate30 + rate50;
            if (!(totalRate > 0f))
                return SHOP_DISCOUNT_TYPE.NONE;

            var roll = UnityEngine.Random.value * totalRate;
            var cumulative = rate10;
            if (roll < cumulative)
                return SHOP_DISCOUNT_TYPE.PER10;

            cumulative += rate20;
            if (roll < cumulative)
                return SHOP_DISCOUNT_TYPE.PER20;

            cumulative += rate30;
            if (roll < cumulative)
                return SHOP_DISCOUNT_TYPE.PER30;

            return rate50 > 0f ? SHOP_DISCOUNT_TYPE.PER50 : SHOP_DISCOUNT_TYPE.NONE;
        }

        static bool hasSelectableDiscountRate(SHOP_ITEM_DAILY row)
        {
            if (row == null)
                return false;

            var totalRate =
                sanitizeDiscountRate(row.discount_rate10_per) +
                sanitizeDiscountRate(row.discount_rate20_per) +
                sanitizeDiscountRate(row.discount_rate30_per) +
                sanitizeDiscountRate(row.discount_rate50_per);
            return totalRate > 0f;
        }

        static float sanitizeDiscountRate(float rate)
        {
            if (float.IsNaN(rate) || float.IsInfinity(rate))
                return 0f;

            return rate > 0f ? rate : 0f;
        }

        static bool isDailyStoredProduct(ShopProductBase product)
        {
            if (product == null || product.catalog_type != SHOP_CATALOG_TYPE.DAILY)
                return false;

            return product.ProductType != SHOP_PRODUCT_TYPE.ADS
                && product.ProductType != SHOP_PRODUCT_TYPE.FREE;
        }

        static bool isAdsOrFreeCurrencyType(CURRENCY_TYPE currencyType)
        {
            return currencyType == CURRENCY_TYPE.ADS
                || currencyType == CURRENCY_TYPE.FREE;
        }

        static int normalizeManualRefreshRemainCount(int remainCount)
        {
            if (remainCount <= 0)
                return 0;

            if (remainCount >= MaxManualRefreshCountPerDay)
                return MaxManualRefreshCountPerDay;

            return remainCount;
        }

        static SHOP_DISCOUNT_TYPE normalizeDiscountType(SHOP_DISCOUNT_TYPE discountType)
        {
            switch (discountType)
            {
                case SHOP_DISCOUNT_TYPE.PER10:
                case SHOP_DISCOUNT_TYPE.PER20:
                case SHOP_DISCOUNT_TYPE.PER30:
                case SHOP_DISCOUNT_TYPE.PER50:
                    return discountType;
                default:
                    return SHOP_DISCOUNT_TYPE.NONE;
            }
        }
    }
}
