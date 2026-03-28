namespace Devian
{
    internal sealed class RedDotNode
    {
        public RedDotNode(string key, string parentKey)
        {
            Key = key;
            ParentKey = parentKey;
        }

        public string Key { get; }
        public string ParentKey { get; }
        public bool SelfOn { get; set; }
        public int ActiveChildCount { get; set; }

        public bool IsOn => SelfOn || HasActiveChild;
        public bool HasActiveChild => ActiveChildCount > 0;
    }
}
