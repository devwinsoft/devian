using System;
using UnityEngine;

namespace Devian.Core
{
    [Serializable]
    public class CFloat
    {
        [SerializeField]
        public int save1 = 0;

        [SerializeField]
        public int save2 = 0;

        byte[] data1 = new byte[4];
        byte[] data2 = new byte[4];

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct IntFloatUnion
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public int I;
            [System.Runtime.InteropServices.FieldOffset(0)] public float F;
        }

        public CFloat()
        {
            seed();
        }

        public CFloat(float _value)
        {
            seed();
            set(_value);
        }

        public void LoadData(int v1, int v2)
        {
            // Avoid allocations from BitConverter.GetBytes
            data1[0] = (byte)(v1 & 0xFF);
            data1[1] = (byte)((v1 >> 8) & 0xFF);
            data1[2] = (byte)((v1 >> 16) & 0xFF);
            data1[3] = (byte)((v1 >> 24) & 0xFF);

            data2[0] = (byte)(v2 & 0xFF);
            data2[1] = (byte)((v2 >> 8) & 0xFF);
            data2[2] = (byte)((v2 >> 16) & 0xFF);
            data2[3] = (byte)((v2 >> 24) & 0xFF);
            save1 = v1;
            save2 = v2;
        }

        public float GetValue() => get();

        public void SetValue(float value) => set(value);

        // Backward compatibility (previous signature was int by mistake)
        [Obsolete("Use SetValue(float) instead.")]
        public void SetValue(int value) => set(value);

        float get()
        {
            // No heap allocations (previously: new byte[4])
            int b0 = (data1[0] ^ data2[0]) & 0xFF;
            int b1 = (data1[2] ^ data2[2]) & 0xFF;
            int b2 = (data1[1] ^ data2[1]) & 0xFF;
            int b3 = (data1[3] ^ data2[3]) & 0xFF;

            var u = new IntFloatUnion
            {
                I = unchecked(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24))
            };
            return u.F;
        }

        void set(float value)
        {
            // Avoid allocations from BitConverter.GetBytes
            var u = new IntFloatUnion { F = value };
            int bits = u.I;
            byte src0 = (byte)(bits & 0xFF);
            byte src1 = (byte)((bits >> 8) & 0xFF);
            byte src2 = (byte)((bits >> 16) & 0xFF);
            byte src3 = (byte)((bits >> 24) & 0xFF);

            data1[0] = (byte)(src0 ^ data2[0]);
            data1[1] = (byte)(src2 ^ data2[1]);
            data1[2] = (byte)(src1 ^ data2[2]);
            data1[3] = (byte)(src3 ^ data2[3]);

            save1 = unchecked(
                (data1[0] & 0xFF)
                | ((data1[1] & 0xFF) << 8)
                | ((data1[2] & 0xFF) << 16)
                | ((data1[3] & 0xFF) << 24));
            save2 = unchecked(
                (data2[0] & 0xFF)
                | ((data2[1] & 0xFF) << 8)
                | ((data2[2] & 0xFF) << 16)
                | ((data2[3] & 0xFF) << 24));
        }

        void seed()
        {
            var random = new System.Random();
            for (int i = 0; i < data2.Length; i++)
            {
                data2[i] = (byte)(0xff & random.Next());
            }
        }

        public static implicit operator float(CFloat obj)
        {
            if (obj == null)
                return 0f;
            return obj.get();
        }

        public static implicit operator CFloat(float _value)
        {
            return new CFloat(_value);
        }
    }
}

