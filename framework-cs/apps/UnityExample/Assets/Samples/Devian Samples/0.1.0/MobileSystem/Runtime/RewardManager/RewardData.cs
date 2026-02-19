namespace Devian
{
    public enum RewardType
    {
        Item = 0,
        Currency = 1,
    }

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
