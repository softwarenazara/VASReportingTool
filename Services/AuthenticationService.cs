using System;
using System.Web;
using System.Web.Security;
using VASReportingTool.Models;
using VASReportingTool.Repositories;

namespace VASReportingTool.Services
{
    public class AuthenticationService
    {
        private readonly IReportingRepository _repository;
        private readonly PasswordHasher _passwordHasher;

        public AuthenticationService()
            : this(new SqlReportingRepository(), new PasswordHasher())
        {
        }

        public AuthenticationService(IReportingRepository repository, PasswordHasher passwordHasher)
        {
            _repository = repository;
            _passwordHasher = passwordHasher;
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
                Secure = FormsAuthentication.RequireSSL
            };

            response.Cookies.Add(cookie);
        }

        public void SignOut()
        {
            FormsAuthentication.SignOut();
        }
    }
}
