// SSOT: skills/devian-common/13-feature-complex/SKILL.md

using System;

namespace Devian
{
    /// <summary>
    /// Lightweight masked integer (masking only, not for security).
    /// State is fully represented by (save1, save2) - serialization safe.
    /// </summary>
    [Serializable]
    public struct CInt : IEquatable<CInt>
    {
        public int save1;
        public int save2;

        /// <summary>
        /// Create CInt with initial value.
        /// </summary>
        public CInt(int value)
        {
            save1 = 0;
            save2 = 0;
            SetValue(value);
        }

        /// <summary>
        /// Get the actual integer value using permutation decoding.
        /// Permutation: value_b0 = s1_b0 ^ s2_b0, value_b1 = s1_b2 ^ s2_b2,
        ///              value_b2 = s1_b1 ^ s2_b1, value_b3 = s1_b3 ^ s2_b3
        /// </summary>
        public int GetValue()
        {
            byte s1_b0 = (byte)(save1 & 0xFF);
            byte s1_b1 = (byte)((save1 >> 8) & 0xFF);
            byte s1_b2 = (byte)((save1 >> 16) & 0xFF);
            byte s1_b3 = (byte)((save1 >> 24) & 0xFF);

            byte s2_b0 = (byte)(save2 & 0xFF);
            byte s2_b1 = (byte)((save2 >> 8) & 0xFF);
            byte s2_b2 = (byte)((save2 >> 16) & 0xFF);
            byte s2_b3 = (byte)((save2 >> 24) & 0xFF);

            byte v_b0 = (byte)(s1_b0 ^ s2_b0);
            byte v_b1 = (byte)(s1_b2 ^ s2_b2);
            byte v_b2 = (byte)(s1_b1 ^ s2_b1);
            byte v_b3 = (byte)(s1_b3 ^ s2_b3);

            return v_b0 | (v_b1 << 8) | (v_b2 << 16) | (v_b3 << 24);
        }

        /// <summary>
        /// Set the integer value using permutation encoding.
        /// Generates random mask bytes and computes save1/save2.
        /// </summary>
        public void SetValue(int value)
        {
            byte v_b0 = (byte)(value & 0xFF);
            byte v_b1 = (byte)((value >> 8) & 0xFF);
            byte v_b2 = (byte)((value >> 16) & 0xFF);
            byte v_b3 = (byte)((value >> 24) & 0xFF);

            // Generate random mask bytes (masking only)
            var rand = ComplexUtil.GetRandom();
            byte s2_b0 = (byte)rand.Next(256);
            byte s2_b1 = (byte)rand.Next(256);
            byte s2_b2 = (byte)rand.Next(256);
            byte s2_b3 = (byte)rand.Next(256);

            // Inverse permutation to get save1 bytes
            // s1_b0 = value_b0 ^ s2_b0
            // s1_b1 = value_b2 ^ s2_b1
            // s1_b2 = value_b1 ^ s2_b2
            // s1_b3 = value_b3 ^ s2_b3
            byte s1_b0 = (byte)(v_b0 ^ s2_b0);
            byte s1_b1 = (byte)(v_b2 ^ s2_b1);
            byte s1_b2 = (byte)(v_b1 ^ s2_b2);
            byte s1_b3 = (byte)(v_b3 ^ s2_b3);

            save1 = s1_b0 | (s1_b1 << 8) | (s1_b2 << 16) | (s1_b3 << 24);
            save2 = s2_b0 | (s2_b1 << 8) | (s2_b2 << 16) | (s2_b3 << 24);
        }

        /// <summary>
        /// Set raw save1/save2 values directly (for deserialization).
        /// </summary>
        public void SetRaw(int s1, int s2)
        {
            save1 = s1;
            save2 = s2;
        }

        // Implicit conversions
        public static implicit operator int(CInt c) => c.GetValue();
        public static implicit operator CInt(int value) => new CInt(value);

        // Equality
        public bool Equals(CInt other) => GetValue() == other.GetValue();
        public override bool Equals(object? obj) => obj is CInt c && Equals(c);
        public override int GetHashCode() => GetValue().GetHashCode();

        public static bool operator ==(CInt left, CInt right) => left.Equals(right);
        public static bool operator !=(CInt left, CInt right) => !left.Equals(right);

        public override string ToString() => GetValue().ToString();
    }
}
