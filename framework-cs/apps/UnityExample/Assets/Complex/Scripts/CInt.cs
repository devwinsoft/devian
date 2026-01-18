using System;
using UnityEngine;

namespace Devian.Core
{
    [Serializable]
    public class CInt
    {
        [SerializeField]
        public int save1 = 0;

        [SerializeField]
        public int save2 = 0;

        byte[] data1 = new byte[4];
        byte[] data2 = new byte[4];
        public CInt()
        {
            seed();
        }

        public CInt(int _value)
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

        public int GetValue() => get();

        public void SetValue(int value) => set(value);

        int get()
        {
            // No heap allocations (previously: new byte[4])
            int b0 = (data1[0] ^ data2[0]) & 0xFF;
            int b1 = (data1[2] ^ data2[2]) & 0xFF;
            int b2 = (data1[1] ^ data2[1]) & 0xFF;
            int b3 = (data1[3] ^ data2[3]) & 0xFF;
            return unchecked(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
        }

        void set(int value)
        {
            // Avoid allocations from BitConverter.GetBytes
            byte src0 = (byte)(value & 0xFF);
            byte src1 = (byte)((value >> 8) & 0xFF);
            byte src2 = (byte)((value >> 16) & 0xFF);
            byte src3 = (byte)((value >> 24) & 0xFF);

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

        public static implicit operator int(CInt obj)
        {
            if (obj == null)
                return 0;
            return obj.get();
        }

        public static implicit operator ulong(CInt obj)
        {
            if (obj == null)
                return 0;
            return (ulong)obj.get();
        }

        public static implicit operator CInt(int _value)
        {
            return new CInt(_value);
        }

    }
}

