using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityItemBase : AbilityBase
    {
        public abstract string ItemId { get; }
        public int Amount => this[STAT_TYPE.ITEM_AMOUNT];
        public int Level => this[STAT_TYPE.ITEM_LEVEL];

        public void AddAmount(int delta)
        {
            AddStat(STAT_TYPE.ITEM_AMOUNT, delta);
        }
    }
}
