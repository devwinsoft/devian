// SSOT: skills/devian-common/13-feature-complex/SKILL.md

using System;

namespace Devian
{
    /// <summary>
    /// Lightweight masked float (masking only, not a security feature).
    /// State is fully represented by (save1, save2) - serialization safe.
    /// Uses same permutation rules as CInt, with float-to-int bits conversion.
    /// </summary>
    [Serializable]
    public struct CFloat : IEquatable<CFloat>
    {
        public int save1;
        public int save2;

        /// <summary>
        /// Create CFloat with initial value.
        /// </summary>
        public CFloat(float value)
        {
            save1 = 0;
            save2 = 0;
            SetValue(value);
        }

        /// <summary>
        /// Get the actual float value using permutation decoding.
        /// </summary>
        public float GetValue()
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

            int bits = v_b0 | (v_b1 << 8) | (v_b2 << 16) | (v_b3 << 24);
            return BitConverter.Int32BitsToSingle(bits);
        }

        /// <summary>
        /// Set the float value using permutation encoding.
        /// </summary>
        public void SetValue(float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);

            byte v_b0 = (byte)(bits & 0xFF);
            byte v_b1 = (byte)((bits >> 8) & 0xFF);
            byte v_b2 = (byte)((bits >> 16) & 0xFF);
            byte v_b3 = (byte)((bits >> 24) & 0xFF);

            // Generate random mask bytes
            var rand = ComplexUtil.GetRandom();
            byte s2_b0 = (byte)rand.Next(256);
            byte s2_b1 = (byte)rand.Next(256);
            byte s2_b2 = (byte)rand.Next(256);
            byte s2_b3 = (byte)rand.Next(256);

            // Inverse permutation
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
        public static implicit operator float(CFloat c) => c.GetValue();
        public static implicit operator CFloat(float value) => new CFloat(value);

        // Equality (compare float values)
        public bool Equals(CFloat other) => GetValue() == other.GetValue();
        public override bool Equals(object? obj) => obj is CFloat c && Equals(c);
        public override int GetHashCode() => GetValue().GetHashCode();

        public static bool operator ==(CFloat left, CFloat right) => left.Equals(right);
        public static bool operator !=(CFloat left, CFloat right) => !left.Equals(right);

        public override string ToString() => GetValue().ToString();
    }
}
