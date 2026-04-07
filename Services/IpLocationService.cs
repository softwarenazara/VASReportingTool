using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using VASReportingTool.Models;

namespace VASReportingTool.Services
{
    public class IpLocationService
    {
        public IpLocationResult Resolve(string ipAddress)
        {
            var result = new IpLocationResult { IpAddress = ipAddress, Provider = "none" };
            if (string.IsNullOrWhiteSpace(ipAddress)) return result;
            if (IsPrivate(ipAddress))
            {
                result.City = "Private network";
                result.Provider = "local";
                return result;
            }

            var template = ConfigurationManager.AppSettings["GeoLocationApiUrl"];
            var token = ConfigurationManager.AppSettings["GeoLocationApiToken"];
            if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(token))
            {
                result.Provider = "unconfigured";
                return result;
            }

            try
            {
                var request = WebRequest.Create(string.Format(template, ipAddress, token));
                request.Method = "GET";
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var payload = reader.ReadToEnd();
                    var serializer = new JavaScriptSerializer();
                    var map = serializer.Deserialize<Dictionary<string, object>>(payload);
                    result.City = Read(map, "city");
                    result.Region = Read(map, "region");
                    result.Country = Read(map, "country");
                    result.Provider = "ipinfo-lite";
                }
            }
            catch
            {
                result.Provider = "lookup-failed";
            }

            return result;
        }

        private static string Read(IDictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null ? value.ToString() : string.Empty;
        }

        private static bool IsPrivate(string ipAddress)
        {
            return ipAddress.StartsWith("10.") || ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("127.") || ipAddress.StartsWith("::1") || ipAddress.StartsWith("172.16.") || ipAddress.StartsWith("172.17.") || ipAddress.StartsWith("172.18.") || ipAddress.StartsWith("172.19.") || ipAddress.StartsWith("172.2") || ipAddress.StartsWith("172.30.") || ipAddress.StartsWith("172.31.");
        }
    }
}
