namespace VASReportingTool.Models
{
    public class PasswordResetToken
    {
        public int PasswordResetTokenId { get; set; }
        public int UserId { get; set; }
        public string TokenHash { get; set; }
        public string TokenSalt { get; set; }
        public System.DateTime ExpiresOnUtc { get; set; }
        public bool IsUsed { get; set; }
        public System.DateTime CreatedOnUtc { get; set; }
    }
}
