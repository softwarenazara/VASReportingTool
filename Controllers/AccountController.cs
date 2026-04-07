using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;
using VASReportingTool.Services;

namespace VASReportingTool.Controllers{
    public class AccountController : Controller
    {
        private readonly AuthenticationService _authenticationService;
        private readonly IReportingRepository _repository;
        private readonly OtpService _otpService;
        private readonly EmailService _emailService;
        private readonly IpLocationService _ipLocationService;

        public AccountController()
            : this(new AuthenticationService(), new SqlReportingRepository(), new OtpService(), new EmailService(), new IpLocationService())
        {
        }

        public AccountController(AuthenticationService authenticationService, IReportingRepository repository, OtpService otpService, EmailService emailService, IpLocationService ipLocationService)
        {
            _authenticationService = authenticationService;
            _repository = repository;
            _otpService = otpService;
            _emailService = emailService;
            _ipLocationService = ipLocationService;
        }

        [AllowAnonymous]
        public ActionResult Login()
        {
            PreventPageCaching();
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (Request.IsAuthenticated)
            {
                // A stale auth cookie with an expired session can otherwise bounce
                // between Login and Dashboard forever after an app recycle.
                Session.Clear();
                Session.Abandon();
                _authenticationService.SignOut();
                ExpireAuthCookie();
            }

            ClearPendingSession();
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ErrorMessage = "Username and password are required.";
                return View(model);
            }

            var user = _authenticationService.Validate(model.Username, model.Password);
            if (user == null)
            {
                model.ErrorMessage = "Invalid credentials or inactive account.";
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                model.ErrorMessage = "User email is not configured. Please contact admin.";
                return View(model);
            }

            var ipAddress = GetClientIpAddress();
            var location = _ipLocationService.Resolve(ipAddress);
            var sessionKey = Guid.NewGuid().ToString("N");
            var sendResult = SendOtpChallenge(user, sessionKey, ipAddress, location.ToDisplayText(), true);
            if (!sendResult.Success)
            {
                model.ErrorMessage = sendResult.Message;
                return View(model);
            }

            Session["PendingUserId"] = user.UserId;
            Session["PendingUsername"] = user.Username;
            Session["PendingRole"] = user.Role;
            Session["PendingSessionKey"] = sessionKey;
            Session["PendingLocation"] = location.ToDisplayText();
            Session["PendingIpAddress"] = ipAddress;
            Session["PendingEmailHint"] = sendResult.MaskedEmail;
            _repository.LogUserActivity(BuildActivity(user, sessionKey, "PasswordValidated", "Primary credentials validated.", ipAddress, location.ToDisplayText()));
            return RedirectToAction("VerifyOtp", new { username = user.Username, info = sendResult.Message, v = DateTime.UtcNow.Ticks });
        }

        [AllowAnonymous]
        public ActionResult VerifyOtp(string username, string info, string v)
        {
            PreventPageCaching();
            if (Session["PendingUserId"] == null)
            {
                return RedirectToAction("Login");
            }

            var userId = (int)Session["PendingUserId"];
            var latestOtp = _repository.GetLatestActiveOtp(userId);
            return View(new OtpVerificationViewModel
            {
                Username = username ?? Convert.ToString(Session["PendingUsername"]),
                InfoMessage = info,
                MaskedEmail = Convert.ToString(Session["PendingEmailHint"]),
                ResendCooldownSeconds = CalculateCooldownSeconds(latestOtp)
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOtp(OtpVerificationViewModel model)
        {
            if (Session["PendingUserId"] == null)
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                model.ErrorMessage = "A valid 6 digit OTP is required.";
                model.ResendCooldownSeconds = CalculateCooldownSeconds(_repository.GetLatestActiveOtp((int)Session["PendingUserId"]));
                return View(model);
            }

            var userId = (int)Session["PendingUserId"];
            var role = Convert.ToString(Session["PendingRole"]);
            var sessionKey = Convert.ToString(Session["PendingSessionKey"]);
            var ipAddress = Convert.ToString(Session["PendingIpAddress"]);
            var locationText = Convert.ToString(Session["PendingLocation"]);
            var user = _repository.GetUserById(userId);
            var challenge = _repository.GetLatestActiveOtp(userId);

            if (!_otpService.Validate(model.OtpCode, challenge))
            {
                _repository.LogUserActivity(BuildActivity(user, sessionKey, "OtpVerificationFailed", "Invalid or expired OTP.", ipAddress, locationText));
                model.ErrorMessage = "Invalid or expired OTP.";
                model.MaskedEmail = Convert.ToString(Session["PendingEmailHint"]);
                model.ResendCooldownSeconds = CalculateCooldownSeconds(challenge);
                return View(model);
            }

            _repository.MarkOtpUsed(challenge.LoginOtpId);
            _authenticationService.SignIn(Response, user, false);
            Session["UserId"] = user.UserId;
            Session["Username"] = user.Username;
            Session["Role"] = role;
            Session["SessionKey"] = sessionKey;
            Session["UserLocation"] = locationText;
            Session["UserIpAddress"] = ipAddress;
            Session.Timeout = 30;
            ClearPendingSession();
            _repository.LogUserActivity(BuildActivity(user, sessionKey, "LoginSuccessful", "OTP verified and user session started.", ipAddress, locationText));
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ResendOtp(string username)
        {
            if (Session["PendingUserId"] == null)
            {
                return RedirectToAction("Login");
            }

            var userId = (int)Session["PendingUserId"];
            var user = _repository.GetUserById(userId);
            var sessionKey = Convert.ToString(Session["PendingSessionKey"]);
            var ipAddress = Convert.ToString(Session["PendingIpAddress"]);
            var locationText = Convert.ToString(Session["PendingLocation"]);
            var sendResult = SendOtpChallenge(user, sessionKey, ipAddress, locationText, false);
            return RedirectToAction("VerifyOtp", new { username = username, info = sendResult.Message, v = DateTime.UtcNow.Ticks });
        }

        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            PreventPageCaching();
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ErrorMessage = "Username or email is required.";
                return View(model);
            }

            var user = _repository.GetUserByUsername(model.UsernameOrEmail);
            model.InfoMessage = "If the account exists, a password reset email has been sent.";
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return View(model);
            }

            var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var hasher = new PasswordHasher();
            var salt = hasher.GenerateSalt();
            var hash = hasher.HashPassword(rawToken, salt);
            _repository.CreatePasswordResetToken(user.UserId, hash, salt, DateTime.UtcNow.AddMinutes(30));
            var resetUrl = Url.Action("ResetPassword", "Account", new { token = rawToken }, Request.Url.Scheme);
            _emailService.SendPasswordReset(user.Email, user.Username, resetUrl);
            _repository.LogUserActivity(new UserActivityLog
            {
                UserId = user.UserId,
                Username = user.Username,
                SessionKey = string.Empty,
                ActionName = "PasswordResetRequested",
                Details = "Password reset email sent.",
                IpAddress = GetClientIpAddress(),
                LocationText = _ipLocationService.Resolve(GetClientIpAddress()).ToDisplayText(),
                UserAgent = Request.UserAgent,
                CreatedOnUtc = DateTime.UtcNow
            });
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult ResetPassword(string token)
        {
            PreventPageCaching();
            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid || !string.Equals(model.NewPassword, model.ConfirmPassword, StringComparison.Ordinal))
            {
                model.ErrorMessage = "Enter and confirm a valid new password.";
                return View(model);
            }

            var token = _repository.GetPasswordResetTokenByRawToken(model.Token);
            if (token == null || token.IsUsed || token.ExpiresOnUtc < DateTime.UtcNow)
            {
                model.ErrorMessage = "Password reset link is invalid or expired.";
                return View(model);
            }

            var user = _repository.GetUserById(token.UserId);
            var hasher = new PasswordHasher();
            var salt = hasher.GenerateSalt();
            var hash = hasher.HashPassword(model.NewPassword, salt);
            _repository.ResetPassword(user.UserId, hash, salt);
            _repository.MarkPasswordResetTokenUsed(token.PasswordResetTokenId);
            _repository.LogUserActivity(new UserActivityLog
            {
                UserId = user.UserId,
                Username = user.Username,
                SessionKey = string.Empty,
                ActionName = "PasswordResetCompleted",
                Details = "Password reset completed by email link.",
                IpAddress = GetClientIpAddress(),
                LocationText = _ipLocationService.Resolve(GetClientIpAddress()).ToDisplayText(),
                UserAgent = Request.UserAgent,
                CreatedOnUtc = DateTime.UtcNow
            });
            return RedirectToAction("Login");
        }

        [HttpPost]
        [SessionAuthorize]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            var userId = Session["UserId"] == null ? 0 : (int)Session["UserId"];
            if (userId > 0)
            {
                var user = _repository.GetUserById(userId);
                _repository.LogUserActivity(BuildActivity(user, Convert.ToString(Session["SessionKey"]), "Logout", "User logged out.", Convert.ToString(Session["UserIpAddress"]), Convert.ToString(Session["UserLocation"])));
            }

            Session.Clear();
            Session.Abandon();
            _authenticationService.SignOut();
            return RedirectToAction("Login");
        }

        [HttpPost]
        [AllowAnonymous]
        public JsonResult ApiLogin(LoginViewModel model)
        {
            var user = _authenticationService.Validate(model.Username, model.Password);
            if (user == null)
            {
                Response.StatusCode = 401;
                return Json(new { success = false, message = "Invalid credentials." });
            }

            var ipAddress = GetClientIpAddress();
            var location = _ipLocationService.Resolve(ipAddress);
            var sessionKey = Guid.NewGuid().ToString("N");
            var sendResult = SendOtpChallenge(user, sessionKey, ipAddress, location.ToDisplayText(), true);
            if (!sendResult.Success)
            {
                Response.StatusCode = 429;
                return Json(new { success = false, message = sendResult.Message });
            }

            Session["PendingUserId"] = user.UserId;
            Session["PendingUsername"] = user.Username;
            Session["PendingRole"] = user.Role;
            Session["PendingSessionKey"] = sessionKey;
            Session["PendingLocation"] = location.ToDisplayText();
            Session["PendingIpAddress"] = ipAddress;
            Session["PendingEmailHint"] = sendResult.MaskedEmail;
            return Json(new { success = true, requiresOtp = true, username = user.Username, maskedEmail = sendResult.MaskedEmail });
        }

        private OtpSendResult SendOtpChallenge(User user, string sessionKey, string ipAddress, string locationText, bool expireExisting)
        {
            var latestOtp = _repository.GetLatestActiveOtp(user.UserId);
            var cooldownSeconds = CalculateCooldownSeconds(latestOtp);
            if (cooldownSeconds > 0)
            {
                var message = "Please wait " + cooldownSeconds + " seconds before requesting a new OTP.";
                _repository.LogUserActivity(BuildActivity(user, sessionKey, "OtpResendBlocked", message, ipAddress, locationText));
                return new OtpSendResult { Success = false, Message = message };
            }

            var otpCode = _otpService.GenerateOtpCode();
            var challenge = _otpService.CreateChallenge(user.UserId, otpCode);
            try
            {
                if (expireExisting) _repository.ExpireOtpChallenges(user.UserId);
                _repository.SaveLoginOtp(challenge);

                // Send OTP by email in the background so the login flow remains responsive.
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        _emailService.SendOtp(user.Email, user.Username, otpCode);
                        _repository.LogUserActivity(BuildActivity(user, sessionKey, "OtpSent", "OTP sent to registered email.", ipAddress, locationText));
                    }
                    catch (Exception ex)
                    {
                        _repository.LogUserActivity(BuildActivity(user, sessionKey, "OtpSendFailed", ex.ToString(), ipAddress, locationText));
                    }
                });

                return new OtpSendResult
                {
                    Success = true,
                    MaskedEmail = MaskEmail(user.Email),
                    Message = "OTP request processed. Check your email (may take a few seconds)."
                };
            }
            catch (Exception ex)
            {
                _repository.LogUserActivity(BuildActivity(user, sessionKey, "OtpSendFailed", ex.ToString(), ipAddress, locationText));
                return new OtpSendResult { Success = false, Message = "Unable to start OTP delivery. Check configuration and try again." };
            }
        }

        private int CalculateCooldownSeconds(LoginOtp latestOtp)
        {
            var cooldownSetting = ConfigurationManager.AppSettings["OtpResendCooldownSeconds"];
            var cooldown = 60;
            int parsed;
            if (int.TryParse(cooldownSetting, out parsed) && parsed > 0) cooldown = parsed;
            if (latestOtp == null) return 0;
            var nextAllowedUtc = latestOtp.CreatedOnUtc.AddSeconds(cooldown);
            var remaining = (int)Math.Ceiling((nextAllowedUtc - DateTime.UtcNow).TotalSeconds);
            return remaining > 0 ? remaining : 0;
        }

        private string GetClientIpAddress()
        {
            var forwarded = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',').First().Trim();
            return Request.UserHostAddress;
        }

        private UserActivityLog BuildActivity(User user, string sessionKey, string action, string details, string ipAddress, string locationText)
        {
            return new UserActivityLog
            {
                UserId = user.UserId,
                Username = user.Username,
                SessionKey = sessionKey,
                ActionName = action,
                Details = details,
                IpAddress = ipAddress,
                LocationText = locationText,
                UserAgent = Request.UserAgent,
                CreatedOnUtc = DateTime.UtcNow
            };
        }

        private void ClearPendingSession()
        {
            Session.Remove("PendingUserId");
            Session.Remove("PendingUsername");
            Session.Remove("PendingRole");
            Session.Remove("PendingSessionKey");
            Session.Remove("PendingLocation");
            Session.Remove("PendingIpAddress");
            Session.Remove("PendingEmailHint");
        }

        private void ExpireAuthCookie()
        {
            var expiredCookie = new HttpCookie(FormsAuthentication.FormsCookieName, string.Empty)
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Path = FormsAuthentication.FormsCookiePath
            };

            Response.Cookies.Add(expiredCookie);
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "registered email";
            }

            var parts = email.Split('@');
            if (parts.Length != 2)
            {
                return email;
            }

            var local = parts[0];
            var domain = parts[1];
            if (local.Length <= 1)
            {
                return "*" + "@" + domain;
            }

            return local.Substring(0, 1) + new string('*', Math.Max(3, local.Length - 1)) + "@" + domain;
        }

        private void PreventPageCaching()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
        }

        private class OtpSendResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string MaskedEmail { get; set; }
        }
    }
}


