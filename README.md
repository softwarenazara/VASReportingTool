# VAS Reporting Tool

ASP.NET MVC 4 / .NET Framework 4.0 secure reporting portal with:

- username/password plus email OTP login
- OTP resend with configurable cooldown
- password reset by email
- admin and user role separation
- region-restricted reporting with admin all-region access
- region APIs stored in `RegionUrls` and proxied by the secured backend
- IP and location capture on login
- activity logs from login through logout with admin filters

## Setup

1. Create SQL Server database `VASReportingToolDb`.
2. Run `Sql/schema.sql`.
3. Run `Sql/seed.sql`.
4. Copy `Web.config.example` to `Web.config`.
5. Update `ReportingDb` in your local `Web.config`.
6. Configure SMTP keys in your local `Web.config`.
7. Set `OtpResendCooldownSeconds` if you want a different resend window.
8. Create a free IPinfo token and place it in `GeoLocationApiToken`.
9. Keep region backend base URLs in the `RegionUrls` table.
10. Restore MVC 4 NuGet packages and host in IIS or Visual Studio on .NET Framework 4.0.

## GitHub Safety

- `Web.config` is intentionally treated as a local-only file because it can contain environment-specific secrets.
- Use `Web.config.example` as the tracked template for new environments.
- Before publishing this project, replace any real SMTP, database, or API credentials in your local configuration.

## Seed credentials

- Admin: `admin` / `Admin@123`
- User: `report.user` / `User@123`

## Notes

- The old `DashboardTemplate.html` hardcoded API map has been sanitized.
- `ReportingData` was removed from the schema because report data is expected to come from region APIs, not local SQL storage.
- OTP verification is stored in `UserLoginOtp`.
- Password reset links are stored in `PasswordResetTokens`.
- Activity logs are stored in `UserActivityLogs`.
