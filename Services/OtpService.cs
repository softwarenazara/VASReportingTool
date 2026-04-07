using System;
using System.Security.Cryptography;
using VASReportingTool.Models;

namespace VASReportingTool.Services
{
    public class OtpService
    {
        private readonly PasswordHasher _passwordHasher;

        public OtpService()
        {
            _passwordHasher = new PasswordHasher();
        }

        public string GenerateOtpCode()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                var value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
                return value.ToString("D6");
            }
        }

        public LoginOtp CreateChallenge(int userId, string otpCode)
        {
            var salt = _passwordHasher.GenerateSalt();
            return new LoginOtp
            {
                UserId = userId,
                OtpSalt = salt,
                OtpHash = _passwordHasher.HashPassword(otpCode, salt),
                CreatedOnUtc = DateTime.UtcNow,
                ExpiresOnUtc = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false
            };
        }

        public bool Validate(string otpCode, LoginOtp storedOtp)
        {
            if (storedOtp == null || storedOtp.IsUsed || storedOtp.ExpiresOnUtc < DateTime.UtcNow)
            {
                return false;
            }

            return _passwordHasher.VerifyPassword(otpCode, storedOtp.OtpSalt, storedOtp.OtpHash);
        }
    }
}
