using System;
using UnityEngine;

namespace Devian.Core
{
    [Serializable]
    public class CString
    {
        public static implicit operator string(CString obj)
        {
            return obj.ToString();
        }

        public static implicit operator CString(string value)
        {
            CString obj = new CString(value);
            return obj;
        }

        [SerializeField]
        public string data;

        public string Value
        {
            get { return ToString(); }
            set { data = ComplexUtil.Encrypt_Base64(value); }
        }

        public override string ToString()
        {
            try
            {
                return ComplexUtil.Decrypt_Base64(data);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.Message);
                return string.Empty;
            }
        }

        public CString()
        {
            data = string.Empty;
        }

        public CString(string value)
        {
            data = ComplexUtil.Encrypt_Base64(value);
        }
    }

}
