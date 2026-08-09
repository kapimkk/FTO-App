using System;
using System.Security.Cryptography;
using System.Text;

namespace FTO_App.Services
{
    /// <summary>
    /// Hash de senha com PBKDF2 (SHA-256). Formato: pbkdf2$iter$saltB64$hashB64
    /// Aceita senha em texto puro legado e faz upgrade no login.
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const string Prefix = "pbkdf2$";

        public static bool IsHashed(string? stored) =>
            !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix, StringComparison.Ordinal);

        public static string Hash(string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;

            if (!IsHashed(stored))
                return string.Equals(password, stored, StringComparison.Ordinal);

            string[] parts = stored.Split('$');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[1], out int iter) || iter < 1) return false;

            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iter,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>True se a senha legada precisa ser regravada como hash.</summary>
        public static bool NeedsUpgrade(string stored) => !IsHashed(stored);
    }
}
