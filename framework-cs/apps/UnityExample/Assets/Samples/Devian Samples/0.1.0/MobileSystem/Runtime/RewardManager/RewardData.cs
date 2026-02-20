using Devian.Domain.Game;

namespace Devian
{
    public readonly struct RewardData
    {
        public RewardData(RewardType type, string id, long amount)
        {
            Type = type;
            Id = id;
            Amount = amount;
        }

        public RewardType Type { get; }
        public string Id { get; }
        public long Amount { get; }
    }
}
