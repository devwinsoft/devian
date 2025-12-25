namespace Devian.Common
{
    /// <summary>
    /// Standard identifier types for Devian Framework.
    /// G prefix = "Generated" or "Global" standard types.
    /// </summary>
    
    /// <summary>Unique identifier (64-bit)</summary>
    public readonly struct GId
    {
        public long Value { get; }
        public GId(long value) => Value = value;
        public static implicit operator long(GId id) => id.Value;
        public static implicit operator GId(long value) => new(value);
        public override string ToString() => Value.ToString();
    }

    /// <summary>Entity/Object identifier (32-bit)</summary>
    public readonly struct GEntityId
    {
        public int Value { get; }
        public GEntityId(int value) => Value = value;
        public static implicit operator int(GEntityId id) => id.Value;
        public static implicit operator GEntityId(int value) => new(value);
        public override string ToString() => Value.ToString();
    }

    /// <summary>Table row key (32-bit)</summary>
    public readonly struct GKey
    {
        public int Value { get; }
        public GKey(int value) => Value = value;
        public static implicit operator int(GKey key) => key.Value;
        public static implicit operator GKey(int value) => new(value);
        public override string ToString() => Value.ToString();
    }

    /// <summary>Timestamp in milliseconds (64-bit)</summary>
    public readonly struct GTimestamp
    {
        public long Value { get; }
        public GTimestamp(long value) => Value = value;
        public static implicit operator long(GTimestamp ts) => ts.Value;
        public static implicit operator GTimestamp(long value) => new(value);
        public override string ToString() => Value.ToString();
    }
}
