using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class InventorySnapshotEquipment
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemUid { get; set; } = string.Empty;
        public int ItemLevel { get; set; } = 1;
    }

    public sealed class InventorySnapshotCard
    {
        public string ItemId { get; set; } = string.Empty;
        public int ItemLevel { get; set; } = 1;
        public int Amount { get; set; }
    }

    public sealed class InventorySnapshotMaterial
    {
        public string ItemId { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public sealed class InventorySnapshotHero
    {
        public string ItemId { get; set; } = string.Empty;
        public int ItemLevel { get; set; } = 1;
        public int Amount { get; set; }
        public Dictionary<EQUIP_SLOT_TYPE, string> Equips { get; } = new();
    }

    public sealed class InventorySnapshot
    {
        public Dictionary<CURRENCY_TYPE, long> CurrencyBalances { get; } = new();
        public Dictionary<string, InventorySnapshotEquipment> Equipments { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, InventorySnapshotCard> Cards { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, InventorySnapshotMaterial> Materials { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, InventorySnapshotHero> Heroes { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> Rentals { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> Passes { get; } = new(StringComparer.Ordinal);
        public Dictionary<ITEM_GRADE_TYPE, int> TreasureCounts { get; } = new();
        public int TreasureCurrentLevel { get; set; } = 1;
        public int TreasureCurrentExp { get; set; }
        public long LastStaminaUpdateUtcMs { get; set; }

        public void ClearInventoryState()
        {
            CurrencyBalances.Clear();
            Equipments.Clear();
            Cards.Clear();
            Materials.Clear();
            Heroes.Clear();
            Rentals.Clear();
            Passes.Clear();
            LastStaminaUpdateUtcMs = 0L;
        }

        public void ClearTreasureState()
        {
            TreasureCounts.Clear();
            TreasureCurrentLevel = 1;
            TreasureCurrentExp = 0;
        }

        public void Clear()
        {
            ClearInventoryState();
            ClearTreasureState();
        }
    }
}
