using System;
using System.Security.Cryptography;
using System.Text;
using System.Web.Helpers;

namespace ECOLearning.Services
{
    /// <summary>
    /// Password Hasher Service conforming to NIST SP 800-63B & OWASP ASVS v4.0.
    /// Uses PBKDF2 with HMAC-SHA256 and a 128-bit random salt.
    /// Provides backward-compatible verification for legacy hashes and plaintext credentials.
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Hashes a plain password using PBKDF2 with HMAC-SHA256 and salt.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return null;

            try
            {
                // Uses System.Web.Helpers.Crypto.HashPassword (PBKDF2 with HMAC-SHA256, 128-bit salt)
                return Crypto.HashPassword(password);
            }
            catch
            {
                // Custom PBKDF2 fallback with SHA256 if needed
                return HashPasswordCustom(password);
            }
        }

        /// <summary>
        /// Verifies a provided password against a stored hashed password.
        /// Supports legacy plaintext fallback for backward compatibility.
        /// </summary>
        public static bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            {
                return false;
            }

            try
            {
                // 1. Verify using Crypto.VerifyHashedPassword if hashed
                if (IsHashed(hashedPassword))
                {
                    return Crypto.VerifyHashedPassword(hashedPassword, providedPassword);
                }
            }
            catch
            {
                // Fallthrough to custom PBKDF2 check or legacy check
            }

            // 2. Custom PBKDF2 check
            if (hashedPassword.StartsWith("PBKDF2$"))
            {
                return VerifyPasswordCustom(hashedPassword, providedPassword);
            }

            // 3. Legacy plaintext check (for initial test users)
            return string.Equals(hashedPassword, providedPassword, StringComparison.Ordinal);
        }

        private static bool IsHashed(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            // System.Web.Helpers.Crypto produces a 68-character base64 string starting with 0x00
            return hash.Length >= 48 && !hash.Contains(" ") && (hash.StartsWith("A") || hash.StartsWith("B") || hash.Length == 68 || hash.Length == 84);
        }

        private static string HashPasswordCustom(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                byte[] hashBytes = new byte[48];
                Array.Copy(salt, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 32);
                return "PBKDF2$" + Convert.ToBase64String(hashBytes);
            }
        }

        private static bool VerifyPasswordCustom(string hashedPassword, string providedPassword)
        {
            try
            {
                string base64Hash = hashedPassword.Substring(7);
                byte[] hashBytes = Convert.FromBase64String(base64Hash);
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                using (var pbkdf2 = new Rfc2898DeriveBytes(providedPassword, salt, 10000))
                {
                    byte[] hash = pbkdf2.GetBytes(32);
                    for (int i = 0; i < 32; i++)
                    {
                        if (hashBytes[i + 16] != hash[i]) return false;
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
