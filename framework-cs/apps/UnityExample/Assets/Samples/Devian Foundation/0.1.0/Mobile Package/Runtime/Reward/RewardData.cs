using Devian.Domain.Game;

namespace Devian
{
    public readonly struct RewardData
    {
        public RewardData(REWARD_TYPE type, string id, int amount)
        {
            Type = type;
            Id = id;
            Amount = amount;
        }

        public REWARD_TYPE Type { get; }
        public string Id { get; }
        public int Amount { get; }
    }
}
