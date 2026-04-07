using System;
using System.Security.Cryptography;
using System.Text;

namespace VASReportingTool.Services
{
    public class PasswordHasher
    {
        public string GenerateSalt()
        {
            var bytes = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes);
        }

        public string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var input = string.Concat(password ?? string.Empty, "|", salt ?? string.Empty);
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(bytes);
            }
        }

        public bool VerifyPassword(string password, string salt, string expectedHash)
        {
            return string.Equals(HashPassword(password, salt), expectedHash, StringComparison.Ordinal);
        }
    }
}
