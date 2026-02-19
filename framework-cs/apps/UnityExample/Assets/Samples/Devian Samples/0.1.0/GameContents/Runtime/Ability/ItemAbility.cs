using Devian.Domain.Game;

namespace Devian
{
    public sealed class ItemAbility : BaseAbility
    {
        ITEM mTable = null;

        public string ItemId => mTable?.ItemId ?? string.Empty;

        public void Init(ITEM table)
        {
            mTable = table;
        }
    }
}
