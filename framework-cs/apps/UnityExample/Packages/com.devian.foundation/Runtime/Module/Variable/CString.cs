// SSOT: skills/devian-core/31-variable-complex/SKILL.md

using System;

namespace Devian
{
    /// <summary>
    /// Lightweight masked string (masking only, not a security feature).
    /// State is fully represented by data (base64 encoded) - serialization safe.
    /// </summary>
    [Serializable]
    public struct CString : IEquatable<CString>
    {
        /// <summary>
        /// The masked base64 encoded string data.
        /// This is the ONLY state field - serialization safe.
        /// </summary>
        public string data;

        /// <summary>
        /// Create CString with initial plain text value.
        /// </summary>
        public CString(string plainValue)
        {
            data = string.Empty;
            SetValue(plainValue);
        }

        /// <summary>
        /// Get the decrypted plain text value.
        /// Returns empty string on failure.
        /// </summary>
        public string GetValue()
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            try
            {
                return ComplexUtil.Decrypt_Base64(data);
            }
            catch (Exception ex)
            {
                Log.Error($"CString.GetValue failed to decrypt. {ex}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Set the value by encrypting the plain text.
        /// </summary>
        public void SetValue(string plainValue)
        {
            if (string.IsNullOrEmpty(plainValue))
            {
                data = string.Empty;
                return;
            }

            data = ComplexUtil.Encrypt_Base64(plainValue);
        }

        /// <summary>
        /// Set raw data directly (for deserialization).
        /// </summary>
        public void SetRaw(string encryptedData)
        {
            data = encryptedData ?? string.Empty;
        }

        /// <summary>
        /// Get or set the plain text value.
        /// </summary>
        public string Value
        {
            get => GetValue();
            set => SetValue(value);
        }

        // Implicit conversions
        public static implicit operator string(CString c) => c.GetValue();
        public static implicit operator CString(string value) => new CString(value);

        // Equality (compare decrypted values)
        public bool Equals(CString other) => GetValue() == other.GetValue();
        public override bool Equals(object? obj) => obj is CString c && Equals(c);
        public override int GetHashCode() => GetValue().GetHashCode();

        public static bool operator ==(CString left, CString right) => left.Equals(right);
        public static bool operator !=(CString left, CString right) => !left.Equals(right);

        public override string ToString() => GetValue();
    }
}
