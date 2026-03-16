using System.Collections.Generic;

namespace Devian
{
    public readonly struct EntitlementsSnapshot
    {
        public EntitlementsSnapshot(
            IReadOnlyList<string> ownedSeasonPasses,
            IReadOnlyDictionary<string, long> currencyBalances,
            IReadOnlyDictionary<string, long> rentals,
            long serverNowUtcMs)
        {
            OwnedSeasonPasses = ownedSeasonPasses;
            CurrencyBalances = currencyBalances;
            Rentals = rentals;
            ServerNowUtcMs = serverNowUtcMs;
        }

        public IReadOnlyList<string> OwnedSeasonPasses { get; }
        public IReadOnlyDictionary<string, long> CurrencyBalances { get; }
        public IReadOnlyDictionary<string, long> Rentals { get; }
        public long ServerNowUtcMs { get; }
    }
}
