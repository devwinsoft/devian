using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogEvent : ShopCatalogBase
    {
        public ShopCatalogEvent(
            ShopStorage storage = null,
            ShopCatalogEventStorageData storageData = null,
            SHOP_CATALOG catalogConfig = null)
            : this(storage, storageData, products: null, catalogConfig)
        {
        }

        internal ShopCatalogEvent(
            ShopStorage storage,
            ShopCatalogEventStorageData storageData,
            IReadOnlyList<ShopProductBase> products,
            SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.EVENT, storage, storageData, catalogConfig, products)
        {
        }

        protected override IReadOnlyList<ShopProductBase> onRefresh()
        {
            if (PrebuiltProducts != null)
                return PrebuiltProducts;

            return buildActiveEventProducts(TB_SHOP_ITEM_EVENT.GetAll(), RemoteDataManager.ServerNowUtcMs);
        }

        internal override long GetNextProductRefreshUtcMs(long serverNowUtcMs)
        {
            return getNextEventRefreshUtcMs(TB_SHOP_ITEM_EVENT.GetAll(), serverNowUtcMs);
        }

        static IReadOnlyList<ShopProductBase> buildActiveEventProducts(
            IReadOnlyList<SHOP_ITEM_EVENT> rows,
            long serverNowUtcMs)
        {
            if (rows == null || rows.Count <= 0 || serverNowUtcMs <= 0L)
                return Array.Empty<ShopProductBase>();

            var products = new List<ShopProductBase>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!isEventRowActive(row, serverNowUtcMs))
                    continue;

                var product = ShopProductFactory.CreateEventProduct(row);
                if (product != null)
                    products.Add(product);
            }

            return products;
        }

        static long getNextEventRefreshUtcMs(
            IReadOnlyList<SHOP_ITEM_EVENT> rows,
            long serverNowUtcMs)
        {
            if (rows == null || rows.Count <= 0 || serverNowUtcMs <= 0L)
                return 0L;

            var nextRefreshUtcMs = 0L;
            for (var i = 0; i < rows.Count; i++)
            {
                if (!tryGetEventWindow(rows[i], out var startUtcMs, out var endUtcMs))
                    continue;

                if (startUtcMs > serverNowUtcMs
                    && (nextRefreshUtcMs <= 0L || startUtcMs < nextRefreshUtcMs))
                {
                    nextRefreshUtcMs = startUtcMs;
                }

                if (endUtcMs > serverNowUtcMs
                    && (nextRefreshUtcMs <= 0L || endUtcMs < nextRefreshUtcMs))
                {
                    nextRefreshUtcMs = endUtcMs;
                }
            }

            return nextRefreshUtcMs;
        }

        static bool isEventRowActive(SHOP_ITEM_EVENT row, long serverNowUtcMs)
        {
            if (!tryGetEventWindow(row, out var startUtcMs, out var endUtcMs))
                return false;

            return startUtcMs <= serverNowUtcMs && serverNowUtcMs < endUtcMs;
        }

        static bool tryGetEventWindow(
            SHOP_ITEM_EVENT row,
            out long startUtcMs,
            out long endUtcMs)
        {
            startUtcMs = normalizeEventUtcMs(row?.start_time.utcTimeMs ?? 0L);
            endUtcMs = normalizeEventUtcMs(row?.end_time.utcTimeMs ?? 0L);
            return row != null
                && !string.IsNullOrWhiteSpace(row.shop_item_id)
                && endUtcMs > 0L
                && endUtcMs > startUtcMs;
        }

        static long normalizeEventUtcMs(long rawUtcMs)
        {
            if (rawUtcMs <= 0L)
                return 0L;

            // SHOP_ITEM_EVENT source data may still be exported as Excel/OA serial days.
            if (rawUtcMs < 100000000000L)
            {
                try
                {
                    var oaUtc = DateTime.FromOADate(rawUtcMs);
                    return new DateTimeOffset(DateTime.SpecifyKind(oaUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
                }
                catch
                {
                    return 0L;
                }
            }

            return rawUtcMs;
        }
    }
}
