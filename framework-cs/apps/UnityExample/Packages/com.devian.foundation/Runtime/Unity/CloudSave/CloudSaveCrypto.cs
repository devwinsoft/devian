using System;
using System.Security.Cryptography;
using System.Text;

namespace Devian
{
    public static class CloudSaveCrypto
    {
        public static string ComputeSha256Base64(string input)
        {
            if (input == null) input = string.Empty;

            var sha = SHA256.Create();
            try
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
            finally
            {
                sha.Dispose();
            }
        }
    }
}
