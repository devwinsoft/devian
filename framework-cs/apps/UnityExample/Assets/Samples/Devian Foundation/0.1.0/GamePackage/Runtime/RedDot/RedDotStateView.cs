namespace Devian
{
    public readonly struct RedDotStateView
    {
        public RedDotStateView(string key, bool isOn, bool selfOn, bool hasActiveChild)
        {
            Key = key;
            IsOn = isOn;
            SelfOn = selfOn;
            HasActiveChild = hasActiveChild;
        }

        public string Key { get; }
        public bool IsOn { get; }
        public bool SelfOn { get; }
        public bool HasActiveChild { get; }
    }
}
