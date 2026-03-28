using System;

namespace Devian
{
    public sealed class RedDotChanged
    {
        public RedDotChanged(string key, bool isOn, bool selfOn, bool hasActiveChild)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
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
