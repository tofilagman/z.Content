using System;
using System.Security.Cryptography;

namespace z.Content
{
    internal static class Utils
    {
        public static string CheckSum(byte[] data)
        {
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToLower();
        }

        public static string GenerateCode()
        {
            return Guid.NewGuid().ToString("N").Replace("-", "");
        }
    }
}
