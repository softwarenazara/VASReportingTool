using System;
using System.Collections.Generic;
using VASReportingTool.Models;

namespace VASReportingTool.Repositories
{
    public interface IReportingRepository
    {
        User GetUserByUsername(string username);
        User GetUserById(int userId);
        IList<User> GetUsers();
        void SaveUser(UserEditViewModel model);
        void ResetPassword(int userId, string passwordHash, string passwordSalt);
        void SetUserStatus(int userId, bool isActive);
        IList<Region> GetAllRegions();
        IList<Region> GetRegionsByUser(int userId);
        IList<string> GetCountries(int userId, int regionId, bool isAdmin);
        IList<string> GetOperators(int userId, int regionId, string country, bool isAdmin);
        IList<string> GetServices(int userId, int regionId, string country, string operatorName, bool isAdmin);
        IList<RegionUrl> GetRegionUrlsForAdmin();
        IList<RegionUrl> GetRegionUrlsForUser(int userId);
        void SaveRegionUrl(RegionUrlViewModel model);
        void DeleteRegionUrl(int id);
        IList<ReportRow> GetReportRows(int userId, DashboardRequest request, bool isAdmin);
        void SaveLoginOtp(LoginOtp challenge);
        LoginOtp GetLatestActiveOtp(int userId);
        void MarkOtpUsed(int loginOtpId);
        void ExpireOtpChallenges(int userId);
        void CreatePasswordResetToken(int userId, string tokenHash, string tokenSalt, DateTime expiresOnUtc);
        PasswordResetToken GetLatestActivePasswordResetToken(int userId);
        PasswordResetToken GetPasswordResetTokenByRawToken(string rawToken);
        void MarkPasswordResetTokenUsed(int passwordResetTokenId);
        void LogUserActivity(UserActivityLog log);
        IList<UserActivityLog> GetRecentActivityLogs(int count);
        IList<UserActivityLog> SearchActivityLogs(ActivityLogFilterViewModel filter);
    }
}
