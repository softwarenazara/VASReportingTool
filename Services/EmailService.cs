using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace VASReportingTool.Services
{
    public class EmailService
    {
        public void SendOtp(string recipientEmail, string username, string otpCode)
        {
            Send(recipientEmail, "VAS Reporting Tool OTP", string.Format("Hello {0}, your OTP for VAS Reporting Tool login is {1}. It will expire in 10 minutes.", username, otpCode));
        }

        public void SendPasswordReset(string recipientEmail, string username, string resetLink)
        {
            Send(recipientEmail, "VAS Reporting Tool Password Reset", string.Format("Hello {0}, use this link to reset your password: {1} . The link will expire in 30 minutes.", username, resetLink));
        }

        private void Send(string recipientEmail, string subject, string body)
        {
            var fromAddress = (ConfigurationManager.AppSettings["OtpFromEmail"] ?? string.Empty).Trim();
            recipientEmail = (recipientEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                throw new InvalidOperationException("OtpFromEmail is not configured.");
            }

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new InvalidOperationException("Recipient email is not configured.");
            }

            var message = new MailMessage(new MailAddress(fromAddress), new MailAddress(recipientEmail))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            using (var client = new SmtpClient())
            {
                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var port = ConfigurationManager.AppSettings["SmtpPort"];
                var usernameSetting = ConfigurationManager.AppSettings["SmtpUsername"];
                var passwordSetting = ConfigurationManager.AppSettings["SmtpPassword"];
                var enableSsl = ConfigurationManager.AppSettings["SmtpEnableSsl"];

                if (!string.IsNullOrWhiteSpace(host)) client.Host = host;
                if (!string.IsNullOrWhiteSpace(port)) client.Port = Convert.ToInt32(port);
                client.UseDefaultCredentials = false;
                if (!string.IsNullOrWhiteSpace(enableSsl)) client.EnableSsl = string.Equals(enableSsl, "true", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(usernameSetting)) client.Credentials = new NetworkCredential(usernameSetting, passwordSetting);

                try
                {
                    client.Send(message);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "SMTP send failed. Host={0}; Port={1}; SSL={2}; From={3}; To={4}; User={5}. Details: {6}",
                            host,
                            port,
                            enableSsl,
                            fromAddress,
                            recipientEmail,
                            usernameSetting,
                            FlattenException(ex)),
                        ex);
                }
            }
        }

        private static string FlattenException(Exception ex)
        {
            var builder = new StringBuilder();
            var current = ex;
            var depth = 0;
            while (current != null && depth < 10)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(current.GetType().FullName);
                builder.Append(": ");
                builder.Append(current.Message);
                current = current.InnerException;
                depth++;
            }

            return builder.ToString();
        }
    }
}
