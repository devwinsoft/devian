// SSOT: skills/devian-unity/10-base-system/35-complex-bigint/SKILL.md

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

        public CBigInt(float @base, int pow)
        {
            mBase = new CFloat(@base);
            mPow = new CInt(pow);
            Normalize(ref mBase, ref mPow);
        }

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
            double aVal = (double)(float)a.mBase * Pow10(a.mPow);
            double result = aVal + (double)b;
            if (result == 0d) return new CBigInt(0f, 0);

            int pow = 0;
            double r = result;
            double abs = Math.Abs(r);
            while (abs >= 10d) { r /= 10d; pow++; abs = Math.Abs(r); }
            while (abs > 0d && abs < 1d) { r *= 10d; pow--; abs = Math.Abs(r); }

            return new CBigInt((float)r, pow);
        }

        // --- Operators: CBigInt - CBigInt ---

        public static CBigInt operator -(CBigInt a, CBigInt b)
        {
            double aVal = (double)(float)a.mBase * Pow10(a.mPow);
            double bVal = (double)(float)b.mBase * Pow10(b.mPow);
            double result = aVal - bVal;
            if (result == 0d) return new CBigInt(0f, 0);

            int pow = 0;
            double r = result;
            double abs = Math.Abs(r);
            while (abs >= 10d) { r /= 10d; pow++; abs = Math.Abs(r); }
            while (abs > 0d && abs < 1d) { r *= 10d; pow--; abs = Math.Abs(r); }

            return new CBigInt((float)r, pow);
        }

        // --- Operators: CBigInt - float, float - CBigInt ---

        public static CBigInt operator -(CBigInt a, float b)
        {
            double aVal = (double)(float)a.mBase * Pow10(a.mPow);
            double result = aVal - (double)b;
            if (result == 0d) return new CBigInt(0f, 0);

            int pow = 0;
            double r = result;
            double abs = Math.Abs(r);
            while (abs >= 10d) { r /= 10d; pow++; abs = Math.Abs(r); }
            while (abs > 0d && abs < 1d) { r *= 10d; pow--; abs = Math.Abs(r); }

            return new CBigInt((float)r, pow);
        }

        public static CBigInt operator -(float a, CBigInt b)
        {
            double bVal = (double)(float)b.mBase * Pow10(b.mPow);
            double result = (double)a - bVal;
            if (result == 0d) return new CBigInt(0f, 0);

            int pow = 0;
            double r = result;
            double abs = Math.Abs(r);
            while (abs >= 10d) { r /= 10d; pow++; abs = Math.Abs(r); }
            while (abs > 0d && abs < 1d) { r *= 10d; pow--; abs = Math.Abs(r); }

            return new CBigInt((float)r, pow);
        }

        // --- Comparison operators ---

        public static bool operator <(CBigInt a, CBigInt b) => a.CompareTo(b) < 0;
        public static bool operator >(CBigInt a, CBigInt b) => a.CompareTo(b) > 0;
        public static bool operator <=(CBigInt a, CBigInt b) => a.CompareTo(b) <= 0;
        public static bool operator >=(CBigInt a, CBigInt b) => a.CompareTo(b) >= 0;

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
