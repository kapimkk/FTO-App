using System;
using System.Security.Cryptography;
using System.Text;

namespace FTO_App.Services
{
    /// <summary>
    /// Criptografia local com DPAPI (Windows) — dados só descriptografam no mesmo usuário/máquina.
    /// </summary>
    public static class SecretProtector
    {
        private const string Prefix = "enc:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FTO-App.v1.SecretProtector");

        public static bool IsProtected(string? value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

        public static string Protect(string? plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            if (IsProtected(plain)) return plain;

            byte[] bytes = Encoding.UTF8.GetBytes(plain);
            byte[] protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(protectedBytes);
        }

        public static string Unprotect(string? stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            if (!IsProtected(stored)) return stored; // legado em texto puro

            try
            {
                byte[] protectedBytes = Convert.FromBase64String(stored[Prefix.Length..]);
                byte[] bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }
    }
}
