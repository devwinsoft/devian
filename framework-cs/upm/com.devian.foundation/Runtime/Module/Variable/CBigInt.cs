// SSOT: skills/devian-core/35-variable-bigint/SKILL.md

using System;

namespace Devian
{
    /// <summary>
    /// Large number representation using scientific notation: mBase * 10^mPow.
    /// mBase is stored as CFloat, mPow as CInt (masked).
    /// </summary>
    [Serializable]
    public struct CBigInt : IComparable<CBigInt>
    {
        public CFloat mBase;
        public CInt mPow;

        public static CBigInt Zero => new CBigInt(0f, 0);
        public bool IsZero => (float)mBase == 0f;

        public CBigInt(float @base, int pow)
        {
            mBase = new CFloat(@base);
            mPow = new CInt(pow);
            Normalize(ref mBase, ref mPow);
        }

        public static CBigInt FromInt(int value) => new CBigInt(value, 0);
        public static CBigInt FromLong(long value) => FromDouble(value);

        // --- Compare ---

        public int CompareTo(CBigInt other)
        {
            float aBase = mBase;
            int aPow = mPow;
            float bBase = other.mBase;
            int bPow = other.mPow;

            // sign check
            int aSign = aBase > 0f ? 1 : (aBase < 0f ? -1 : 0);
            int bSign = bBase > 0f ? 1 : (bBase < 0f ? -1 : 0);

            if (aSign != bSign) return aSign.CompareTo(bSign);
            if (aSign == 0) return 0;

            // same sign: compare pow first
            if (aPow != bPow) return aSign > 0 ? aPow.CompareTo(bPow) : bPow.CompareTo(aPow);

            // same pow: compare base
            return aBase.CompareTo(bBase);
        }

        // --- Operators: CBigInt * CBigInt ---

        public static CBigInt operator +(CBigInt a, CBigInt b)
        {
            if (a.IsZero) return b;
            if (b.IsZero) return a;

            int targetPow = Math.Max((int)a.mPow, (int)b.mPow);
            double sumBase =
                (double)(float)a.mBase * Pow10((int)a.mPow - targetPow) +
                (double)(float)b.mBase * Pow10((int)b.mPow - targetPow);

            return new CBigInt((float)sumBase, targetPow);
        }

        public static CBigInt operator *(CBigInt a, CBigInt b)
        {
            float newBase = (float)a.mBase * (float)b.mBase;
            int newPow = (int)a.mPow + (int)b.mPow;
            return new CBigInt(newBase, newPow);
        }

        // --- Operators: CBigInt * float, float * CBigInt ---

        public static CBigInt operator *(CBigInt a, float b)
        {
            float newBase = (float)a.mBase * b;
            return new CBigInt(newBase, a.mPow);
        }

        public static CBigInt operator *(float a, CBigInt b)
        {
            return b * a;
        }

        // --- Operators: CBigInt / CBigInt ---

        public static CBigInt operator /(CBigInt a, CBigInt b)
        {
            float bVal = b.mBase;
            if (bVal == 0f) throw new DivideByZeroException("CBigInt division by zero");

            float newBase = (float)a.mBase / bVal;
            int newPow = (int)a.mPow - (int)b.mPow;
            return new CBigInt(newBase, newPow);
        }

        // --- Operators: CBigInt / float, float / CBigInt ---

        public static CBigInt operator /(CBigInt a, float b)
        {
            if (b == 0f) throw new DivideByZeroException("CBigInt division by zero");

            float newBase = (float)a.mBase / b;
            return new CBigInt(newBase, a.mPow);
        }

        public static CBigInt operator /(float a, CBigInt b)
        {
            float bVal = b.mBase;
            if (bVal == 0f) throw new DivideByZeroException("CBigInt division by zero");

            float newBase = a / bVal;
            int newPow = -(int)b.mPow;
            return new CBigInt(newBase, newPow);
        }

        // --- Operators: CBigInt + float (source-compatible only) ---

        public static CBigInt operator +(CBigInt a, float b)
        {
            return a + FromDouble(b);
        }

        // --- Operators: CBigInt - CBigInt ---

        public static CBigInt operator -(CBigInt a, CBigInt b)
        {
            return a + (b * -1f);
        }

        // --- Operators: CBigInt - float, float - CBigInt ---

        public static CBigInt operator -(CBigInt a, float b)
        {
            return a + (-b);
        }

        public static CBigInt operator -(float a, CBigInt b)
        {
            return FromDouble(a) - b;
        }

        public static CBigInt Max(CBigInt a, CBigInt b) => a >= b ? a : b;
        public static CBigInt Min(CBigInt a, CBigInt b) => a <= b ? a : b;

        // --- Comparison operators ---

        public static bool operator <(CBigInt a, CBigInt b) => a.CompareTo(b) < 0;
        public static bool operator >(CBigInt a, CBigInt b) => a.CompareTo(b) > 0;
        public static bool operator <=(CBigInt a, CBigInt b) => a.CompareTo(b) <= 0;
        public static bool operator >=(CBigInt a, CBigInt b) => a.CompareTo(b) >= 0;

        // --- RankKey (leaderboard order-preserving long encoding) ---
        // SSOT: skills/devian/10-module/20-core/36-variable-bigint-rank-key/SKILL.md

        private const int RankKeyMantissaPrecision = 1_000_000;
        private const long RankKeyMantissaScale = 10_000_000L;
        private const long RankKeyPowBias = 1_000_000L;

        /// <summary>
        /// Order-preserving long key for platform leaderboards.
        /// a.CompareTo(b) == a.RankKey.CompareTo(b.RankKey) is guaranteed.
        /// Not intended for value reconstruction.
        /// </summary>
        public long RankKey
        {
            get
            {
                float b = mBase;
                int p = mPow;

                if (b == 0f) return 0L;

                NormalizeRaw(ref b, ref p);

                int sign = b > 0f ? 1 : -1;
                float absBase = Math.Abs(b);

                long biasedPow = (long)p + RankKeyPowBias;

                if (biasedPow < 0L)
                    return sign > 0 ? 1L : long.MinValue;
                if (biasedPow > RankKeyPowBias * 2L)
                    return sign > 0 ? long.MaxValue : -1L;

                long mantissaBucket = (long)Math.Floor((absBase - 1f) * RankKeyMantissaPrecision);
                if (mantissaBucket < 0L) mantissaBucket = 0L;

                long magnitudeKey = biasedPow * RankKeyMantissaScale + mantissaBucket;

                return sign > 0 ? (1L + magnitudeKey) : -(1L + magnitudeKey);
            }
        }

        // --- Explicit conversions ---

        public static explicit operator float(CBigInt x)
        {
            double val = (double)(float)x.mBase * Pow10(x.mPow);
            if (val > float.MaxValue || val < float.MinValue)
                throw new OverflowException($"CBigInt value {val} overflows float");
            return (float)val;
        }

        public static explicit operator double(CBigInt x)
        {
            return (double)(float)x.mBase * Pow10(x.mPow);
        }

        public override string ToString()
        {
            float b = mBase;
            int p = mPow;

            if (b == 0f) return "0";

            if (p < 3)
            {
                double v = (double)b * Pow10(p);
                return ((long)Math.Round(v)).ToString();
            }

            int mode = p % 3;
            int group = p / 3;

            double display = (double)b * Pow10(mode);

            string sym = GetSymbol(group);

            // format: 0 / 0.0 / 0.00 (trim style like source)
            double rounded2 = Math.Round(display, 2, MidpointRounding.AwayFromZero);
            double frac = rounded2 - Math.Truncate(rounded2);

            if (Math.Abs(frac) < 1e-9) return $"{(long)rounded2}{sym}";

            double rounded1 = Math.Round(display, 1, MidpointRounding.AwayFromZero);
            double frac1 = rounded1 - Math.Truncate(rounded1);
            if (Math.Abs(frac1) < 1e-9) return $"{rounded1:0.0}{sym}";

            return $"{rounded2:0.00}{sym}";
        }

        private static void Normalize(ref CFloat @base, ref CInt pow)
        {
            float b = @base;
            int p = pow;

            NormalizeRaw(ref b, ref p);

            @base = new CFloat(b);
            pow = new CInt(p);
        }

        private static CBigInt FromDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "CBigInt cannot represent NaN or Infinity.");

            if (value == 0d)
                return Zero;

            int pow = 0;
            double normalized = value;
            double abs = Math.Abs(normalized);

            while (abs >= 10d)
            {
                normalized /= 10d;
                pow += 1;
                abs = Math.Abs(normalized);
            }

            while (abs > 0d && abs < 1d)
            {
                normalized *= 10d;
                pow -= 1;
                abs = Math.Abs(normalized);
            }

            return new CBigInt((float)normalized, pow);
        }

        private static void NormalizeRaw(ref float @base, ref int pow)
        {
            if (@base == 0f)
            {
                pow = 0;
                return;
            }

            float abs = Math.Abs(@base);

            while (abs >= 10f)
            {
                @base /= 10f;
                pow += 1;
                abs = Math.Abs(@base);
            }

            while (abs < 1f)
            {
                @base *= 10f;
                pow -= 1;
                abs = Math.Abs(@base);
            }
        }

        private static double Pow10(int exp)
        {
            return Math.Pow(10d, exp);
        }

        private static string GetSymbol(int group)
        {
            if (group <= 0) return "";

            // group=1 => "a", ..., group=26 => "z", group=27 => "aa"
            group -= 1;

            string result = "";
            while (group >= 0)
            {
                int rem = group % 26;
                result = (char)('a' + rem) + result;
                group = (group / 26) - 1;
            }
            return result;
        }
    }
}
