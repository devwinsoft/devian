using System;

namespace Devian
{
    /// <summary>
    /// Entity identifier. int-wrapping value type for type-safe entity references.
    /// </summary>
    [Serializable]
    public struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public int Value;

        public EntityId(int value) { Value = value; }

        public bool Equals(EntityId other) => Value == other.Value;
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value;

        public static implicit operator int(EntityId id) => id.Value;
        public static implicit operator EntityId(int value) => new EntityId(value);
    }
}
