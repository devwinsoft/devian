using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Devian
{
    public static class Crypto
    {
        public static string EncryptAes(string plainText, byte[] key, byte[] iv)
        {
            if (plainText == null) plainText = string.Empty;

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream, Encoding.UTF8))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string DecryptAes(string cipherTextBase64, byte[] key, byte[] iv)
        {
            if (string.IsNullOrEmpty(cipherTextBase64)) return string.Empty;

            var cipherBytes = Convert.FromBase64String(cipherTextBase64);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipherBytes);
            using var cryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream, Encoding.UTF8);

            return reader.ReadToEnd();
        }

        public static byte[] GenerateKey()
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            return key;
        }

        public static byte[] GenerateIv()
        {
            var iv = new byte[16];
            RandomNumberGenerator.Fill(iv);
            return iv;
        }
    }
}
