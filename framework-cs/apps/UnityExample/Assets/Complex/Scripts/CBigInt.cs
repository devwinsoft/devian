using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian.Core
{
    [Serializable]
    public struct CBigInt : IComparable, IComparable<CBigInt>
    {
        public static CBigInt Zero = new CBigInt(0f, 0);
        public static CBigInt Max = new CBigInt(1f, int.MaxValue);
        public static CBigInt Min = new CBigInt(-1f, int.MaxValue);
        public static CBigInt MaxDouble = new CBigInt(double.MaxValue);
        public static CBigInt MaxFloat = new CBigInt(float.MaxValue);

        static string[] symbol_0 = new string[] { "" };//{ "", "K", "M", "G", "T"};
        static string[] symbol_1 = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z" };

        [SerializeField] CFloat mBase; // < 10.0
        [SerializeField] CInt mPow;

        public CBigInt(float _base, int _pow)
        {
            var data = getData(_base, _pow);
            mBase = data.mBase;
            mPow = data.mPow;
        }

        public CBigInt(double value)
        {
            if (value == 0)
            {
                mBase = 0f;
                mPow = 0;
                return;
            }
            double tmpBase = value;
            int tmpPow = 0;
            while (abs(tmpBase) >= 10.0)
            {
                tmpBase /= 10f;
                tmpPow++;
            }
            while (abs(tmpBase) < 1f)
            {
                tmpBase *= 10f;
                tmpPow--;
            }
            mBase = (float)tmpBase;
            mPow = tmpPow;
        }

        public override string ToString()
        {
            if (mPow < 3)
            {
                var fValue = mBase.GetValue() * Mathf.Pow(10f, mPow.GetValue());
                var iValue = Mathf.RoundToInt(fValue);
                return iValue.ToString();
            }
            else
            {
                var mode = mPow % 3;
                var display = mBase.GetValue() * Mathf.Pow(10f, mode);
                var symbol = getSymbol();
                var remain = Mathf.RoundToInt(display * 100f) % 100;
                if (remain == 0)
                {
                    return string.Format("{0:N0} {1}", display, symbol);
                }
                else if ((remain % 10) == 0)
                {
                    return string.Format("{0:N1} {1}", display, symbol);
                }
                else
                {
                    return string.Format("{0:N2} {1}", display, symbol);
                }
            }
        }

        string getSymbol()
        {
            int i = Mathf.Max(0, mPow / 3);
            if (i < symbol_0.Length)
                return symbol_0[i];

            i = Mathf.Max(0, (mPow / 3) - symbol_0.Length);
            if (i < symbol_1.Length)
                return symbol_1[i];

            List<char> list = new List<char>(4);
            do
            {
                list.Insert(0, symbol_1[i % symbol_1.Length][0]);
                i /= symbol_1.Length;
                i--;
            } while (i >= 0);

            return new string(list.ToArray());
        }

        static double abs(double value)
        {
            return value >= 0 ? value : -value;
        }

        static (float mBase, int mPow) getData(float _base, int _pow)
        {
            if (_base == 0f)
            {
                return (0, 0);
            }

            double tmpBase = _base;
            int tmpPow = _pow;
            while (abs(tmpBase) >= 10.0)
            {
                tmpBase /= 10f;
                tmpPow++;
            }
            while (abs(tmpBase) < 1f)
            {
                tmpBase *= 10f;
                tmpPow--;
            }

            return ((float)tmpBase, tmpPow);
        }

        public int CompareTo(object obj)
        {
            if (obj.GetType() == typeof(int))
            {
                var value = (int)obj;
                return CompareTo(new CBigInt(value));
            }
            if (obj.GetType() == typeof(float))
            {
                var value = (float)obj;
                return CompareTo(new CBigInt(value));
            }
            if (obj.GetType() == typeof(double))
            {
                var value = (double)obj;
                return CompareTo(new CBigInt(value));
            }
            Debug.LogError($"[BigInt::CompareTo] Not implemented: type={obj.GetType()}");
            return 0;
        }

        public int CompareTo(CBigInt other)
        {
            if (this.mBase > 0)
            {
                if (other.mBase <= 0f)
                    return 1;
            }
            else if (this.mBase < 0f)
            {
                if (other.mBase >= 0f)
                    return -1;
            }
            else
            {
                if (other.mBase > 0f)
                    return 1;
                else if (other.mBase < 0f)
                    return -1;
                else
                    return 0;
            }
            int result = this.mBase > 0 ? 1 : -1;
            if (this.mPow > other.mPow)
                return result;
            if (this.mPow < other.mPow)
                return -result;
            if (this.mBase > other.mBase)
                return result;
            if (this.mBase < other.mBase)
                return -result;
            return 0;
        }

        public static implicit operator double(CBigInt obj)
        {
            if (obj > CBigInt.MaxDouble)
            {
                throw new OverflowException($"OverflowException: value={obj}");
            }
            double value = obj.mBase.GetValue() * UnityEngine.Mathf.Pow(10f, (float)obj.mPow.GetValue());
            return value;
        }

        public static implicit operator float(CBigInt obj)
        {
            if (obj > CBigInt.MaxFloat)
            {
                throw new OverflowException($"OverflowException: value={obj}");
            }
            float value = obj.mBase.GetValue() * UnityEngine.Mathf.Pow(10f, (float)obj.mPow.GetValue());
            return value;
        }

        public static CBigInt operator +(CBigInt p1, CBigInt p2)
        {
            float tmpBase = 0f;
            int tmpPow = Mathf.Max(p1.mPow, p2.mPow);
            tmpBase += Mathf.Pow(0.1f, tmpPow - p1.mPow) * p1.mBase;
            tmpBase += Mathf.Pow(0.1f, tmpPow - p2.mPow) * p2.mBase;
            return new CBigInt(tmpBase, tmpPow);
        }

        public static CBigInt operator +(float p1, CBigInt p2)
        {
            float tmpBase = 0f;
            int tmpPow = Mathf.Max(0, p2.mPow);
            tmpBase += Mathf.Pow(0.1f, tmpPow) * p1;
            tmpBase += Mathf.Pow(0.1f, tmpPow - p2.mPow) * p2.mBase;
            return new CBigInt(tmpBase, tmpPow);
        }

        public static CBigInt operator +(CBigInt p1, float p2)
        {
            float tmpBase = 0f;
            int tmpPow = Mathf.Max(p1.mPow, 0);
            tmpBase += Mathf.Pow(0.1f, tmpPow - p1.mPow) * p1.mBase;
            tmpBase += Mathf.Pow(0.1f, tmpPow) * p2;
            return new CBigInt(tmpBase, tmpPow);
        }

        public static CBigInt operator -(CBigInt p1, CBigInt p2)
        {
            float tmpBase = 0f;
            int tmpPow = Mathf.Max(p1.mPow, p2.mPow);
            tmpBase += Mathf.Pow(0.1f, tmpPow - p1.mPow) * p1.mBase;
            tmpBase -= Mathf.Pow(0.1f, tmpPow - p2.mPow) * p2.mBase;
            return new CBigInt(tmpBase, tmpPow);
        }

        public static CBigInt operator -(float p1, CBigInt p2)
        {
            float tmpBase = 0f;
            int tmpPow = Mathf.Max(0, p2.mPow);
            tmpBase += Mathf.Pow(0.1f, tmpPow) * p1;
            tmpBase -= Mathf.Pow(0.1f, tmpPow - p2.mPow) * p2.mBase;
            return new CBigInt(tmpBase, tmpPow);
        }

        public static CBigInt operator -(CBigInt p1, float p2)
        {
            float tmpBase = 0f;
            int tmpPow = Mathf.Max(p1.mPow, 0);
            tmpBase += Mathf.Pow(0.1f, tmpPow - p1.mPow) * p1.mBase;
            tmpBase -= Mathf.Pow(0.1f, tmpPow) * p2;
            return new CBigInt(tmpBase, tmpPow);
        }

        public static CBigInt operator *(CBigInt p1, CBigInt p2)
        {
            return new CBigInt(p1.mBase * p2.mBase, p1.mPow + p2.mPow);
        }

        public static CBigInt operator *(float p1, CBigInt p2)
        {
            return new CBigInt(p1 * p2.mBase, p2.mPow);
        }

        public static CBigInt operator *(CBigInt p1, float p2)
        {
            return new CBigInt(p1.mBase * p2, p1.mPow);
        }

        public static CBigInt operator /(CBigInt p1, CBigInt p2)
        {
            if (p2.mBase == 0f)
            {
                throw new DivideByZeroException();
            }
            return new CBigInt(p1.mBase / p2.mBase, p1.mPow - p2.mPow);
        }

        public static CBigInt operator /(float p1, CBigInt p2)
        {
            if (p2.mBase == 0f)
            {
                throw new DivideByZeroException();
            }
            return new CBigInt(p1 / p2.mBase, -p2.mPow);
        }

        public static CBigInt operator /(CBigInt p1, float p2)
        {
            if (p2 == 0f)
            {
                throw new DivideByZeroException();
            }
            return new CBigInt(p1.mBase / p2, p1.mPow);
        }

        public static bool operator <(CBigInt p1, CBigInt p2)
        {
            return p1.CompareTo(p2) < 0;
        }
        public static bool operator >(CBigInt p1, CBigInt p2)
        {
            return p1.CompareTo(p2) > 0;
        }

        public static bool operator <=(CBigInt p1, CBigInt p2)
        {
            return p1.CompareTo(p2) <= 0;
        }
        public static bool operator >=(CBigInt p1, CBigInt p2)
        {
            return p1.CompareTo(p2) >= 0;
        }
    }
}
