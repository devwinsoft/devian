using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class InventoryWallet
    {
        readonly Dictionary<CURRENCY_TYPE, long> mBalances = new();

        public long Get(CURRENCY_TYPE currencyType)
        {
            return currencyType switch
            {
                CURRENCY_TYPE.ADS => 0L,
                CURRENCY_TYPE.ARENA_COIN => GetOrZero(CURRENCY_TYPE.ARENA_COIN),
                CURRENCY_TYPE.FREE => 0L,
                CURRENCY_TYPE.FRIENDSHIP => GetOrZero(CURRENCY_TYPE.FRIENDSHIP),
                CURRENCY_TYPE.GOLD => GetOrZero(CURRENCY_TYPE.GOLD),
                CURRENCY_TYPE.GUILD_COIN => GetOrZero(CURRENCY_TYPE.GUILD_COIN),
                CURRENCY_TYPE.JEWEL => Get(CURRENCY_TYPE.JEWEL_FREE) + Get(CURRENCY_TYPE.JEWEL_PAID),
                CURRENCY_TYPE.JEWEL_FREE => GetOrZero(CURRENCY_TYPE.JEWEL_FREE),
                CURRENCY_TYPE.JEWEL_PAID => GetOrZero(CURRENCY_TYPE.JEWEL_PAID),
                CURRENCY_TYPE.STAMINA => GetOrZero(CURRENCY_TYPE.STAMINA),
                _ => 0L
            };
        }

        internal bool TryAdd(CURRENCY_TYPE currencyType, long amount)
        {
            if (currencyType == CURRENCY_TYPE.ADS
                || currencyType == CURRENCY_TYPE.FREE
                || currencyType == CURRENCY_TYPE.JEWEL)
                return false;

            mBalances.TryGetValue(currencyType, out var current);
            mBalances[currencyType] = current + amount;
            return true;
        }

        public IEnumerable<KeyValuePair<CURRENCY_TYPE, long>> EnumerateForSave()
        {
            foreach (var kv in mBalances)
            {
                if (kv.Key == CURRENCY_TYPE.ADS
                    || kv.Key == CURRENCY_TYPE.FREE
                    || kv.Key == CURRENCY_TYPE.JEWEL)
                    continue;

                yield return kv;
            }
        }

        internal void Clear()
        {
            mBalances.Clear();
        }

        internal void CopyFrom(InventoryWallet source)
        {
            mBalances.Clear();
            if (source == null)
                return;

            foreach (var kv in source.mBalances)
                mBalances[kv.Key] = kv.Value;
        }

        long GetOrZero(CURRENCY_TYPE currencyType)
        {
            return mBalances.TryGetValue(currencyType, out var value) ? value : 0L;
        }
    }
}
