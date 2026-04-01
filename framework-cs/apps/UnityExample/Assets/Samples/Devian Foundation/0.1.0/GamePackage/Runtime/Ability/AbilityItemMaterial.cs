using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemMaterial : AbilityItemBase
    {
        ITEM_MATERIAL mTable = null;

        public override string ItemId => mTable?.item_id ?? string.Empty;

        public void Init(ITEM_MATERIAL table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemMaterial();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
