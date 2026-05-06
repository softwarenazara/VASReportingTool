using System;
using System.Web;
using System.Web.Security;
using System.Web.Script.Serialization;
using VASReportingTool.Models;
using VASReportingTool.Repositories;

namespace VASReportingTool.Services
{
    public class AuthenticationService
    {
        private readonly IReportingRepository _repository;
        private readonly PasswordHasher _passwordHasher;
        private readonly JavaScriptSerializer _serializer;

        public AuthenticationService()
            : this(new SqlReportingRepository(), new PasswordHasher())
        {
        }

        public AuthenticationService(IReportingRepository repository, PasswordHasher passwordHasher)
        {
            _repository = repository;
            _passwordHasher = passwordHasher;
            _serializer = new JavaScriptSerializer();
        }

        public User Validate(string username, string password)
        {
            var user = _repository.GetUserByUsername(username);
            if (user == null || !user.IsActive)
            {
                return null;
            }

            return _passwordHasher.VerifyPassword(password, user.PasswordSalt, user.PasswordHash)
                ? user
                : null;
        }

        public void SignIn(HttpResponseBase response, User user, bool persistent)
        {
            var authTicket = new FormsAuthenticationTicket(1, user.Username, DateTime.Now, DateTime.Now.AddMinutes(30), persistent, user.Role);
            var encryptedTicket = FormsAuthentication.Encrypt(authTicket);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };

            if (persistent)
            {
                cookie.Expires = authTicket.Expiration;
            }

            response.Cookies.Add(cookie);
        }

        public void SignIn(HttpResponseBase response, User user, DateTime expiresOnLocal, string sessionKey, string ipAddress, string locationText)
        {
            var payload = new AuthTicketPayload
            {
                UserId = user.UserId,
                Role = user.Role ?? string.Empty,
                SessionKey = sessionKey ?? string.Empty,
                IpAddress = ipAddress ?? string.Empty,
                LocationText = locationText ?? string.Empty
            };

            var authTicket = new FormsAuthenticationTicket(
                1,
                user.Username,
                DateTime.Now,
                expiresOnLocal,
                true,
                _serializer.Serialize(payload));
            var encryptedTicket = FormsAuthentication.Encrypt(authTicket);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath,
                Expires = authTicket.Expiration
            };

            response.Cookies.Add(cookie);
        }

        public void SignOut()
        {
            FormsAuthentication.SignOut();
        }

        public bool TryRestoreSession(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException("httpContext");
            }

            var session = httpContext.Session;
            if (session == null)
            {
                return false;
            }

            if (session["UserId"] != null && httpContext.Request.IsAuthenticated)
            {
                return true;
            }

            var authCookie = httpContext.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
            {
                return false;
            }

            FormsAuthenticationTicket ticket;
            try
            {
                ticket = FormsAuthentication.Decrypt(authCookie.Value);
            }
            catch
            {
                return false;
            }

            if (ticket == null || ticket.Expiration <= DateTime.Now)
            {
                return false;
            }

            var payload = ReadPayload(ticket.UserData);
            if (payload == null || payload.UserId <= 0)
            {
                return false;
            }

            var user = _repository.GetUserById(payload.UserId);
            if (user == null || !user.IsActive)
            {
                return false;
            }

            session["UserId"] = user.UserId;
            session["Username"] = user.Username;
            session["Role"] = string.IsNullOrWhiteSpace(payload.Role) ? user.Role : payload.Role;
            session["SessionKey"] = string.IsNullOrWhiteSpace(payload.SessionKey) ? Guid.NewGuid().ToString("N") : payload.SessionKey;
            session["UserLocation"] = payload.LocationText ?? string.Empty;
            session["UserIpAddress"] = payload.IpAddress ?? string.Empty;
            session["AuthExpiresOnUtc"] = ticket.Expiration.ToUniversalTime().ToString("o");
            session.Timeout = CalculateRemainingSessionMinutes(ticket.Expiration);
            return true;
        }

        private AuthTicketPayload ReadPayload(string userData)
        {
            if (string.IsNullOrWhiteSpace(userData))
            {
                return null;
            }

            try
            {
                return _serializer.Deserialize<AuthTicketPayload>(userData);
            }
            catch
            {
                return null;
            }
        }

        private static int CalculateRemainingSessionMinutes(DateTime expiresOnLocal)
        {
            var remainingMinutes = (int)Math.Ceiling((expiresOnLocal - DateTime.Now).TotalMinutes);
            if (remainingMinutes < 1)
            {
                return 1;
            }

            return remainingMinutes;
        }

        private class AuthTicketPayload
        {
            public int UserId { get; set; }
            public string Role { get; set; }
            public string SessionKey { get; set; }
            public string IpAddress { get; set; }
            public string LocationText { get; set; }
        }
    }
}
