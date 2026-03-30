using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemHero : AbilityItemBase
    {
        ITEM_HERO mTable = null;
        readonly Dictionary<int, AbilityItemEquip> mEquips = new();

        public string HeroId => mTable?.ItemId ?? string.Empty;
        public override string ItemId => mTable?.ItemId ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityItemEquip> Equips => mEquips;

        public void Init(ITEM_HERO table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemHero();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            foreach (var kv in mEquips)
                c.mEquips[kv.Key] = kv.Value;
            return c;
        }

        public bool Equip(AbilityItemEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0)
                return false;

            if (equip.IsEquipped)
                equip.ClearOwner();

            if (mEquips.TryGetValue(slotNumber, out var prev))
                prev.ClearOwner();

            mEquips[slotNumber] = equip;
            equip.SetOwner(HeroId, slotNumber);
            return true;
        }

        public bool Unequip(int slotNumber)
        {
            if (!mEquips.TryGetValue(slotNumber, out var equip))
                return false;

            equip.ClearOwner();
            mEquips.Remove(slotNumber);
            return true;
        }
    }
}
