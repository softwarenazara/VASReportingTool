using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Script.Serialization;
using VASReportingTool.Models;
using VASReportingTool.Services;

namespace VASReportingTool.Repositories
{
    public class SqlReportingRepository : IReportingRepository
    {
        private const int DefaultDownstreamTimeoutMilliseconds = 30000;
        private const int FilterDownstreamTimeoutMilliseconds = 8000;
        private const int FilterPayloadCacheMinutes = 10;
        private readonly string _connectionString;
        private readonly JavaScriptSerializer _serializer;

        public SqlReportingRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["ReportingDb"].ConnectionString;
            _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        public User GetUserByUsername(string username)
        {
            const string sql = @"SELECT TOP 1 UserId, Username, Email, PasswordHash, PasswordSalt, Role, IsActive FROM Users WHERE Username = @Username OR Email = @Username";
            return ExecuteSingle(sql, cmd => cmd.Parameters.AddWithValue("@Username", username), ReadUser);
        }

        public User GetUserById(int userId)
        {
            const string sql = @"SELECT TOP 1 UserId, Username, Email, PasswordHash, PasswordSalt, Role, IsActive FROM Users WHERE UserId = @UserId";
            return ExecuteSingle(sql, cmd => cmd.Parameters.AddWithValue("@UserId", userId), ReadUser);
        }

        public IList<User> GetUsers()
        {
            return ExecuteList("SELECT UserId, Username, Email, PasswordHash, PasswordSalt, Role, IsActive FROM Users ORDER BY Username", null, ReadUser);
        }

        public void SaveUser(UserEditViewModel model)
        {
            using (var conn = OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                if (model.UserId > 0)
                {
                    var update = new SqlCommand(@"UPDATE Users SET Username = @Username, Email = @Email, Role = @Role, IsActive = @IsActive WHERE UserId = @UserId", conn, tx);
                    update.Parameters.AddWithValue("@UserId", model.UserId);
                    update.Parameters.AddWithValue("@Username", model.Username);
                    update.Parameters.AddWithValue("@Email", model.Email);
                    update.Parameters.AddWithValue("@Role", model.Role);
                    update.Parameters.AddWithValue("@IsActive", model.IsActive);
                    update.ExecuteNonQuery();

                    if (!string.IsNullOrWhiteSpace(model.Password))
                    {
                        var hasher = new PasswordHasher();
                        var salt = hasher.GenerateSalt();
                        var hash = hasher.HashPassword(model.Password, salt);
                        var passwordUpdate = new SqlCommand("UPDATE Users SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt WHERE UserId = @UserId", conn, tx);
                        passwordUpdate.Parameters.AddWithValue("@UserId", model.UserId);
                        passwordUpdate.Parameters.AddWithValue("@PasswordHash", hash);
                        passwordUpdate.Parameters.AddWithValue("@PasswordSalt", salt);
                        passwordUpdate.ExecuteNonQuery();
                    }
                }
                else
                {
                    var hasher = new PasswordHasher();
                    var salt = hasher.GenerateSalt();
                    var hash = hasher.HashPassword(model.Password ?? "ChangeMe123!", salt);
                    var insert = new SqlCommand(@"INSERT INTO Users (Username, Email, PasswordHash, PasswordSalt, Role, IsActive) VALUES (@Username, @Email, @PasswordHash, @PasswordSalt, @Role, @IsActive); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx);
                    insert.Parameters.AddWithValue("@Username", model.Username);
                    insert.Parameters.AddWithValue("@Email", model.Email);
                    insert.Parameters.AddWithValue("@PasswordHash", hash);
                    insert.Parameters.AddWithValue("@PasswordSalt", salt);
                    insert.Parameters.AddWithValue("@Role", model.Role);
                    insert.Parameters.AddWithValue("@IsActive", model.IsActive);
                    model.UserId = (int)insert.ExecuteScalar();
                }

                var deleteRegions = new SqlCommand("DELETE FROM UserRegions WHERE UserId = @UserId", conn, tx);
                deleteRegions.Parameters.AddWithValue("@UserId", model.UserId);
                deleteRegions.ExecuteNonQuery();

                if (model.RegionIds != null)
                {
                    foreach (var regionId in model.RegionIds)
                    {
                        var insertRegion = new SqlCommand("INSERT INTO UserRegions (UserId, RegionId) VALUES (@UserId, @RegionId)", conn, tx);
                        insertRegion.Parameters.AddWithValue("@UserId", model.UserId);
                        insertRegion.Parameters.AddWithValue("@RegionId", regionId);
                        insertRegion.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
        }

        public void ResetPassword(int userId, string passwordHash, string passwordSalt)
        {
            ExecuteNonQuery("UPDATE Users SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt WHERE UserId = @UserId", cmd =>
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@PasswordSalt", passwordSalt);
            });
        }

        public void SetUserStatus(int userId, bool isActive)
        {
            ExecuteNonQuery("UPDATE Users SET IsActive = @IsActive WHERE UserId = @UserId", cmd =>
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@IsActive", isActive);
            });
        }

        public IList<Region> GetAllRegions()
        {
            return ExecuteList("SELECT RegionId, Name, IsActive FROM Regions ORDER BY Name", null, ReadRegion);
        }

        public IList<Region> GetRegionsByUser(int userId)
        {
            const string sql = @"SELECT r.RegionId, r.Name, r.IsActive FROM Regions r INNER JOIN UserRegions ur ON ur.RegionId = r.RegionId WHERE ur.UserId = @UserId AND r.IsActive = 1 ORDER BY r.Name";
            return ExecuteList(sql, cmd => cmd.Parameters.AddWithValue("@UserId", userId), ReadRegion);
        }

        public IList<string> GetCountries(int userId, int regionId, bool isAdmin)
        {
            var regionUrls = GetAccessibleRegionUrls(userId, regionId, isAdmin);

            try
            {
                return ApplyCountryOverrides(
                    regionUrls,
                    GetDistinctFilterValues(
                    regionId,
                    regionUrls,
                    "countries",
                    regionUrl => BuildRegionApiUrl(regionUrl.Url, "filters/countries"),
                    entry =>
                    {
                        var value = entry as IDictionary<string, object>;
                        return value != null ? ReadString(value, "country", "Country", "name", "Name") : Convert.ToString(entry);
                    }));
            }
            catch (Exception ex)
            {
                if (!ShouldFallbackToReportValues(ex))
                {
                    throw;
                }

                try
                {
                    return ApplyCountryOverrides(
                        regionUrls,
                        GetDistinctFilterValues(
                        regionId,
                        regionUrls,
                        "countries",
                        regionUrl => BuildRegionApiUrl(regionUrl.Url, "filters/operators"),
                        entry =>
                        {
                            var value = entry as IDictionary<string, object>;
                            return value != null ? ReadString(value, "country", "Country") : string.Empty;
                        }));
                }
                catch (Exception operatorFallbackEx)
                {
                    if (!ShouldFallbackToReportValues(operatorFallbackEx))
                    {
                        throw;
                    }
                }

                return ApplyCountryOverrides(
                    regionUrls,
                    GetDistinctReportValues(
                        userId,
                        new DashboardRequest
                        {
                            RegionId = regionId
                        },
                        isAdmin,
                        row => row.Country));
            }
        }

        public IList<string> GetOperators(int userId, int regionId, string country, bool isAdmin)
        {
            var regionUrls = GetAccessibleRegionUrls(userId, regionId, isAdmin);
            var regionName = GetPrimaryRegionName(regionUrls);

            try
            {
                if (string.IsNullOrWhiteSpace(country))
                {
                    return GetCountryScopedFilterValues(
                        regionId,
                        regionUrls,
                        country,
                        "operators",
                        regionUrl => BuildRegionApiUrl(regionUrl.Url, "filters/operators"),
                        value => ReadString(value, "operator", "Operator"),
                        null);
                }

                var values = new List<string>();
                foreach (var requestedCountry in GetRequestedCountryValues(regionName, country))
                {
                    foreach (var item in GetCountryScopedFilterValues(
                        regionId,
                        regionUrls,
                        requestedCountry,
                        "operators",
                        regionUrl => BuildRegionApiUrl(regionUrl.Url, "filters/operators"),
                        value => ReadString(value, "operator", "Operator"),
                        null))
                    {
                        AddDistinctText(values, item);
                    }
                }

                return values.OrderBy(value => value).ToList();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(country) && ShouldFallbackToReportValues(ex))
                {
                    return GetDistinctReportValues(
                        userId,
                        new DashboardRequest
                        {
                            RegionId = regionId,
                            Country = country
                        },
                        isAdmin,
                        row => row.OperatorName);
                }

                throw;
            }
        }

        public IList<string> GetServices(int userId, int regionId, string country, string operatorName, bool isAdmin)
        {
            var regionUrls = GetAccessibleRegionUrls(userId, regionId, isAdmin);
            var regionName = GetPrimaryRegionName(regionUrls);

            try
            {
                if (string.IsNullOrWhiteSpace(country))
                {
                    return GetCountryScopedFilterValues(
                        regionId,
                        regionUrls,
                        country,
                        "services",
                        regionUrl => BuildRegionApiUrl(regionUrl.Url, "filters/services?operatorName=" + Uri.EscapeDataString(operatorName ?? string.Empty)),
                        value => ReadString(value, "service", "Service"),
                        operatorName);
                }

                var values = new List<string>();
                foreach (var requestedCountry in GetRequestedCountryValues(regionName, country))
                {
                    foreach (var item in GetCountryScopedFilterValues(
                        regionId,
                        regionUrls,
                        requestedCountry,
                        "services",
                        regionUrl => BuildRegionApiUrl(regionUrl.Url, "filters/services?operatorName=" + Uri.EscapeDataString(operatorName ?? string.Empty)),
                        value => ReadString(value, "service", "Service"),
                        operatorName))
                    {
                        AddDistinctText(values, item);
                    }
                }

                return values.OrderBy(value => value).ToList();
            }
            catch (Exception ex)
            {
                if ((!string.IsNullOrWhiteSpace(country) || !string.IsNullOrWhiteSpace(operatorName)) && ShouldFallbackToReportValues(ex))
                {
                    return GetDistinctReportValues(
                        userId,
                        new DashboardRequest
                        {
                            RegionId = regionId,
                            Country = country,
                            OperatorName = operatorName
                        },
                        isAdmin,
                        row => row.ServiceName);
                }

                throw;
            }
        }

        public IList<RegionUrl> GetRegionUrlsForAdmin()
        {
            const string sql = @"
                WITH RankedRegionUrls AS (
                    SELECT
                        Id,
                        RegionId,
                        Url,
                        IsActive,
                        ROW_NUMBER() OVER (PARTITION BY RegionId ORDER BY Id DESC) AS RowNum
                    FROM RegionUrls
                )
                SELECT Id, RegionId, Url, IsActive
                FROM RankedRegionUrls
                WHERE RowNum = 1
                ORDER BY RegionId";
            return ExecuteList(sql, null, ReadRegionUrl);
        }

        public IList<RegionUrl> GetRegionUrlsForUser(int userId)
        {
            const string sql = @"
                WITH RankedRegionUrls AS (
                    SELECT
                        ru.Id,
                        ru.RegionId,
                        ru.Url,
                        ru.IsActive,
                        ROW_NUMBER() OVER (PARTITION BY ru.RegionId ORDER BY ru.Id DESC) AS RowNum
                    FROM RegionUrls ru
                    WHERE ru.IsActive = 1
                )
                SELECT rru.Id, rru.RegionId, rru.Url, rru.IsActive
                FROM RankedRegionUrls rru
                INNER JOIN UserRegions ur ON ur.RegionId = rru.RegionId
                WHERE ur.UserId = @UserId
                  AND rru.RowNum = 1
                ORDER BY rru.RegionId";
            return ExecuteList(sql, cmd => cmd.Parameters.AddWithValue("@UserId", userId), ReadRegionUrl);
        }

        public void SaveRegionUrl(RegionUrlViewModel model)
        {
            using (var conn = OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                model.Url = NormalizeRegionApiBaseUrl(model.Url);
                var targetId = 0;

                if (model.Id > 0)
                {
                    var findById = new SqlCommand("SELECT TOP 1 Id FROM RegionUrls WHERE Id = @Id", conn, tx);
                    findById.Parameters.AddWithValue("@Id", model.Id);
                    var existingById = findById.ExecuteScalar();
                    if (existingById != null && existingById != DBNull.Value)
                    {
                        targetId = Convert.ToInt32(existingById);
                    }
                }

                if (targetId <= 0)
                {
                    var findByRegion = new SqlCommand("SELECT TOP 1 Id FROM RegionUrls WHERE RegionId = @RegionId ORDER BY Id DESC", conn, tx);
                    findByRegion.Parameters.AddWithValue("@RegionId", model.RegionId);
                    var existingByRegion = findByRegion.ExecuteScalar();
                    if (existingByRegion != null && existingByRegion != DBNull.Value)
                    {
                        targetId = Convert.ToInt32(existingByRegion);
                    }
                }

                if (targetId > 0)
                {
                    var update = new SqlCommand(@"UPDATE RegionUrls SET RegionId = @RegionId, Url = @Url, IsActive = @IsActive WHERE Id = @Id", conn, tx);
                    update.Parameters.AddWithValue("@Id", targetId);
                    update.Parameters.AddWithValue("@RegionId", model.RegionId);
                    update.Parameters.AddWithValue("@Url", model.Url);
                    update.Parameters.AddWithValue("@IsActive", model.IsActive);
                    update.ExecuteNonQuery();
                }
                else
                {
                    var insert = new SqlCommand(@"INSERT INTO RegionUrls (RegionId, Url, IsActive) VALUES (@RegionId, @Url, @IsActive); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx);
                    insert.Parameters.AddWithValue("@RegionId", model.RegionId);
                    insert.Parameters.AddWithValue("@Url", model.Url);
                    insert.Parameters.AddWithValue("@IsActive", model.IsActive);
                    targetId = (int)insert.ExecuteScalar();
                }

                var deleteDuplicates = new SqlCommand("DELETE FROM RegionUrls WHERE RegionId = @RegionId AND Id <> @Id", conn, tx);
                deleteDuplicates.Parameters.AddWithValue("@RegionId", model.RegionId);
                deleteDuplicates.Parameters.AddWithValue("@Id", targetId);
                deleteDuplicates.ExecuteNonQuery();

                tx.Commit();
            }
        }

        public void DeleteRegionUrl(int id)
        {
            ExecuteNonQuery("DELETE FROM RegionUrls WHERE Id = @Id", cmd => cmd.Parameters.AddWithValue("@Id", id));
        }

        public IList<ReportRow> GetReportRows(int userId, DashboardRequest request, bool isAdmin)
        {
            request = request ?? new DashboardRequest();
            var rows = new List<ReportRow>();
            var regionUrls = GetAccessibleRegionUrls(userId, request.RegionId, isAdmin);
            if (string.IsNullOrWhiteSpace(request.Country))
            {
                var overrideTargets = GetCountryOverrideTargets(regionUrls, null);

                if (regionUrls.Count > 0)
                {
                    rows.AddRange(FetchReportRows(regionUrls, request, true).Rows);
                }

                if (overrideTargets.Count > 0)
                {
                    var overrideResult = FetchReportRows(overrideTargets, request, false);
                    if (overrideResult.HasSuccessfulResponse)
                    {
                        rows = ExcludeReportRows(rows, overrideTargets).ToList();
                        rows.AddRange(overrideResult.Rows);
                    }
                }
            }
            else
            {
                rows.AddRange(GetCountryScopedReportRows(regionUrls, request));
            }

            rows = NormalizeReportCountries(rows).ToList();

            return rows
                .Where(row =>
                    MatchesCountryFilter(row.RegionName, row.Country, request.Country) &&
                    MatchesTextFilter(row.OperatorName, request.OperatorName) &&
                    MatchesTextFilter(row.ServiceName, request.ServiceName))
                .OrderByDescending(x => x.ReportDate)
                .ToList();
        }

        private IList<string> GetDistinctFilterValues(int userId, int regionId, bool isAdmin, string operationLabel, Func<RegionApiTarget, string> requestUrlFactory, Func<object, string> valueSelector)
        {
            return GetDistinctFilterValues(
                regionId,
                GetAccessibleRegionUrls(userId, regionId, isAdmin),
                operationLabel,
                requestUrlFactory,
                valueSelector);
        }

        private IList<string> GetCountryScopedFilterValues(int regionId, IList<RegionApiTarget> regionUrls, string country, string operationLabel, Func<RegionApiTarget, string> requestUrlFactory, Func<IDictionary<string, object>, string> valueSelector, string operatorName)
        {
            var filterTargets = GetCountryOverrideTargets(regionUrls, country);
            if (filterTargets.Count == 0)
            {
                filterTargets = regionUrls;
            }

            return GetDistinctFilterValues(
                regionId,
                filterTargets,
                operationLabel,
                requestUrlFactory,
                entry =>
                {
                    var value = entry as IDictionary<string, object>;
                    if (value == null)
                    {
                        return string.IsNullOrWhiteSpace(country) ? Convert.ToString(entry) : string.Empty;
                    }

                    if (!string.IsNullOrWhiteSpace(country) && !MatchesTextFilter(ReadString(value, "country", "Country"), country))
                    {
                        return string.Empty;
                    }

                    if (!string.IsNullOrWhiteSpace(operatorName) && !MatchesTextFilter(ReadString(value, "operator", "Operator"), operatorName))
                    {
                        return string.Empty;
                    }

                    return valueSelector(value);
                });
        }

        private IList<ReportRow> GetCountryScopedReportRows(IList<RegionApiTarget> regionUrls, DashboardRequest request)
        {
            var rows = new List<ReportRow>();
            var regionName = GetPrimaryRegionName(regionUrls);

            foreach (var requestedCountry in GetRequestedCountryValues(regionName, request.Country))
            {
                var filterTargets = GetCountryOverrideTargets(regionUrls, requestedCountry);
                if (filterTargets.Count == 0)
                {
                    filterTargets = regionUrls;
                }

                if (filterTargets.Count == 0)
                {
                    continue;
                }

                var countryRequest = CloneDashboardRequest(request);
                countryRequest.Country = requestedCountry;
                rows.AddRange(
                    FetchReportRows(filterTargets, countryRequest, true)
                        .Rows
                        .Where(row => MatchesTextFilter(row.Country, requestedCountry)));
            }

            return rows;
        }

        private IList<string> GetDistinctFilterValues(int regionId, IList<RegionApiTarget> regionUrls, string operationLabel, Func<RegionApiTarget, string> requestUrlFactory, Func<object, string> valueSelector)
        {
            var values = new List<string>();
            var failures = new List<string>();
            var hasSuccessfulResponse = false;

            foreach (var regionUrl in regionUrls)
            {
                var requestUrl = requestUrlFactory(regionUrl);
                try
                {
                    var payload = GetCachedFilterPayload(requestUrl);
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        payload = GetJson(requestUrl, FilterDownstreamTimeoutMilliseconds);
                        CacheFilterPayload(requestUrl, payload);
                    }

                    var entries = ParseJsonArray(payload);
                    hasSuccessfulResponse = true;

                    foreach (var entry in entries)
                    {
                        AddDistinctText(values, valueSelector(entry));
                    }
                }
                catch (Exception ex)
                {
                    TrackRegionApiFailure(operationLabel, regionUrl, requestUrl, ex, failures);
                }
            }

            EnsureRegionApiAvailability(operationLabel, regionId, regionUrls, failures, hasSuccessfulResponse);
            return values.OrderBy(x => x).ToList();
        }

        private IList<string> GetDistinctReportValues(int userId, DashboardRequest request, bool isAdmin, Func<ReportRow, string> valueSelector)
        {
            return GetReportRows(userId, request, isAdmin)
                .Select(valueSelector)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();
        }

        private IList<string> ApplyCountryOverrides(IList<RegionApiTarget> regionUrls, IList<string> countries)
        {
            var values = new List<string>();
            var regionName = GetPrimaryRegionName(regionUrls);
            foreach (var country in countries ?? new List<string>())
            {
                AddDistinctText(values, NormalizeCountry(regionName, country));
            }

            foreach (var overrideTarget in GetCountryOverrideTargets(regionUrls, null))
            {
                AddDistinctText(values, NormalizeCountry(overrideTarget.RegionName, overrideTarget.OverrideCountry));
            }

            return values.OrderBy(value => value).ToList();
        }

        private static DashboardRequest CloneDashboardRequest(DashboardRequest request)
        {
            request = request ?? new DashboardRequest();
            return new DashboardRequest
            {
                RegionId = request.RegionId,
                Country = request.Country,
                OperatorName = request.OperatorName,
                ServiceName = request.ServiceName,
                FromDate = request.FromDate,
                ToDate = request.ToDate
            };
        }

        private static string GetPrimaryRegionName(IList<RegionApiTarget> regionUrls)
        {
            if (regionUrls == null)
            {
                return string.Empty;
            }

            return regionUrls
                .Select(item => item.RegionName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;
        }

        private static IList<ReportRow> NormalizeReportCountries(IList<ReportRow> rows)
        {
            if (rows == null)
            {
                return new List<ReportRow>();
            }

            foreach (var row in rows)
            {
                row.Country = NormalizeCountry(row.RegionName, row.Country);
            }

            return rows;
        }

        private static bool MatchesCountryFilter(string regionName, string value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return string.Equals(
                NormalizeCountry(regionName, value) ?? string.Empty,
                NormalizeCountry(regionName, filter) ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeCountry(string regionName, string country)
        {
            if (string.IsNullOrWhiteSpace(country))
            {
                return country;
            }

            var aliasGroup = GetCountryAliasGroup(regionName, country);
            return aliasGroup != null ? aliasGroup.DisplayCountry : country;
        }

        private static IList<string> GetRequestedCountryValues(string regionName, string country)
        {
            var values = new List<string>();
            if (string.IsNullOrWhiteSpace(country))
            {
                return values;
            }

            var aliasGroup = GetCountryAliasGroup(regionName, country);
            if (aliasGroup != null)
            {
                foreach (var aliasCountry in aliasGroup.Countries)
                {
                    AddDistinctText(values, aliasCountry);
                }

                return values;
            }

            AddDistinctText(values, country);
            return values;
        }

        private static CountryAliasGroup GetCountryAliasGroup(string regionName, string country)
        {
            if (string.IsNullOrWhiteSpace(regionName) || string.IsNullOrWhiteSpace(country))
            {
                return null;
            }

            return GetConfiguredCountryAliases().FirstOrDefault(item =>
                string.Equals(item.RegionName, regionName, StringComparison.OrdinalIgnoreCase) &&
                (MatchesTextFilter(item.DisplayCountry, country) ||
                 item.Countries.Any(aliasCountry => MatchesTextFilter(aliasCountry, country))));
        }

        private IList<RegionApiTarget> GetCountryOverrideTargets(IList<RegionApiTarget> regionUrls, string countryFilter)
        {
            var targets = new List<RegionApiTarget>();
            if (regionUrls == null || regionUrls.Count == 0)
            {
                return targets;
            }

            foreach (var regionUrl in regionUrls)
            {
                foreach (var countryOverride in GetConfiguredCountryBackendOverrides()
                    .Where(item => string.Equals(item.RegionName, regionUrl.RegionName, StringComparison.OrdinalIgnoreCase))
                    .Where(item => string.IsNullOrWhiteSpace(countryFilter) || MatchesTextFilter(item.Country, countryFilter)))
                {
                    if (targets.Any(existing =>
                        existing.RegionId == regionUrl.RegionId &&
                        string.Equals(existing.Url, countryOverride.Url, StringComparison.OrdinalIgnoreCase) &&
                        MatchesTextFilter(existing.OverrideCountry, countryOverride.Country)))
                    {
                        continue;
                    }

                    targets.Add(new RegionApiTarget
                    {
                        RegionId = regionUrl.RegionId,
                        RegionName = regionUrl.RegionName,
                        Url = countryOverride.Url,
                        OverrideCountry = countryOverride.Country
                    });
                }
            }

            return targets;
        }

        private ReportFetchResult FetchReportRows(IList<RegionApiTarget> regionUrls, DashboardRequest request, bool requireAvailability)
        {
            var rows = new List<ReportRow>();
            var failures = new List<string>();
            var hasSuccessfulResponse = false;

            foreach (var regionUrl in regionUrls)
            {
                var requestUrl = BuildDashboardRequestUrl(regionUrl.Url, request);
                try
                {
                    var payload = GetJson(requestUrl);
                    var envelope = _serializer.DeserializeObject(payload) as IDictionary<string, object>;
                    var dailyData = ReadJsonArray(envelope, "dailyData", "rows", "tableData");
                    hasSuccessfulResponse = true;
                    if (dailyData.Count == 0)
                    {
                        continue;
                    }

                    foreach (var item in dailyData)
                    {
                        var map = item as IDictionary<string, object>;
                        if (map == null)
                        {
                            continue;
                        }

                        var country = ReadString(map, "country", "Country");
                        if (!string.IsNullOrWhiteSpace(regionUrl.OverrideCountry) &&
                            !MatchesTextFilter(country, regionUrl.OverrideCountry))
                        {
                            continue;
                        }

                        rows.Add(new ReportRow
                        {
                            ReportDate = ReadDate(map, "date", "Date"),
                            RegionId = regionUrl.RegionId,
                            RegionName = regionUrl.RegionName,
                            OperatorName = ReadString(map, "operator", "Operator"),
                            ServiceName = ReadString(map, "service", "Service"),
                            Country = country,
                            ActivationSource = ReadString(map, "activationSource", "ActivationSource", "source", "Source", "activationChannel", "ActivationChannel"),
                            ActivationCategory = ReadString(map, "activationCategory", "ActivationCategory", "activationCtg", "ActivationCtg", "ctg", "CTG", "category", "Category"),
                            TotalVisitors = ReadInt(map, "totalVisitors", "TOTAL VISITORS"),
                            UniqueVisitors = ReadInt(map, "uniqueVisitors", "UNIQUE VISITORS"),
                            ActivationAttempts = ReadInt(map, "activationAttempts", "ACTIVATION ATTEMPTS"),
                            FreeTrials = ReadInt(map, "freeTrials", "FREE TRIALS"),
                            ActivationCount = ReadInt(map, "activationCount", "ACTIVATION COUNT", "ACTIVATION", "Activations", "activations"),
                            ActivationRevenue = ReadDecimal(map, "activationRevenue", "ACTIVATION REVENUE"),
                            RenewalCount = ReadInt(map, "renewalCount", "RENEWAL COUNT"),
                            RenewalRevenue = ReadDecimal(map, "renewalRevenue", "RENEWAL REVENUE"),
                            TotalRevenue = ReadDecimal(map, "totalRevenue", "TOTAL REVENUE"),
                            Churn = ReadInt(map, "churn", "CHURN"),
                            UserChurn = ReadInt(map, "userChurn", "UserChurn", "USER CHURN", "USER_INIT", "user_init", "userInit"),
                            SystemChurn = ReadInt(map, "systemChurn", "SystemChurn", "SYSTEM CHURN", "CP_INIT", "cp_init", "cpInit"),
                            Deactivation = ReadInt(map, "deactivation", "Deactivation", "DEACTIVATION", "PROVISION", "provision", "Provision"),
                            GrossBase = ReadInt(map, "grossBase", "GROSS BASE"),
                            ActiveBase = ReadInt(map, "activeBase", "ACTIVE BASE")
                        });
                    }
                }
                catch (Exception ex)
                {
                    TrackRegionApiFailure("dashboard data", regionUrl, requestUrl, ex, failures);
                }
            }

            if (requireAvailability)
            {
                EnsureRegionApiAvailability("dashboard data", request.RegionId, regionUrls, failures, hasSuccessfulResponse);
            }

            return new ReportFetchResult
            {
                Rows = rows,
                HasSuccessfulResponse = hasSuccessfulResponse
            };
        }

        private static IList<ReportRow> ExcludeReportRows(IList<ReportRow> rows, IList<RegionApiTarget> overrideTargets)
        {
            if (rows == null || rows.Count == 0 || overrideTargets == null || overrideTargets.Count == 0)
            {
                return rows == null ? new List<ReportRow>() : rows.ToList();
            }

            return rows
                .Where(row => !overrideTargets.Any(target =>
                    target.RegionId == row.RegionId &&
                    MatchesTextFilter(target.OverrideCountry, row.Country)))
                .ToList();
        }

        private static string BuildDashboardRequestUrl(string baseUrl, DashboardRequest request)
        {
            var builder = new StringBuilder(BuildRegionApiUrl(baseUrl, "report/dashboard"));
            builder.Append("?");
            var parameters = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Country))
            {
                parameters.Add("country=" + Uri.EscapeDataString(request.Country));
                parameters.Add("countryName=" + Uri.EscapeDataString(request.Country));
            }
            if (!string.IsNullOrWhiteSpace(request.OperatorName))
            {
                parameters.Add("operatorName=" + Uri.EscapeDataString(request.OperatorName));
            }
            if (!string.IsNullOrWhiteSpace(request.ServiceName))
            {
                parameters.Add("service=" + Uri.EscapeDataString(request.ServiceName));
            }
            if (request.FromDate.HasValue)
            {
                parameters.Add("fromDate=" + request.FromDate.Value.ToString("yyyy-MM-dd"));
            }
            if (request.ToDate.HasValue)
            {
                parameters.Add("toDate=" + request.ToDate.Value.ToString("yyyy-MM-dd"));
            }

            builder.Append(string.Join("&", parameters.ToArray()));
            return builder.ToString();
        }

        private static IList<CountryBackendOverride> GetConfiguredCountryBackendOverrides()
        {
            var overrides = new List<CountryBackendOverride>();
            var rawValue = ConfigurationManager.AppSettings["RegionCountryBackendOverrides"];
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return overrides;
            }

            foreach (var entry in rawValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(new[] { '|' }, 3);
                if (parts.Length != 3)
                {
                    continue;
                }

                var regionName = (parts[0] ?? string.Empty).Trim();
                var country = (parts[1] ?? string.Empty).Trim();
                var url = NormalizeRegionApiBaseUrl(parts[2]);
                if (string.IsNullOrWhiteSpace(regionName) ||
                    string.IsNullOrWhiteSpace(country) ||
                    string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                overrides.Add(new CountryBackendOverride
                {
                    RegionName = regionName,
                    Country = country,
                    Url = url
                });
            }

            return overrides;
        }

        private static IList<CountryAliasGroup> GetConfiguredCountryAliases()
        {
            var aliases = new List<CountryAliasGroup>();
            var rawValue = ConfigurationManager.AppSettings["RegionCountryAliases"];
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return aliases;
            }

            foreach (var entry in rawValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(new[] { '|' }, 3);
                if (parts.Length != 3)
                {
                    continue;
                }

                var regionName = (parts[0] ?? string.Empty).Trim();
                var displayCountry = (parts[1] ?? string.Empty).Trim();
                var countries = (parts[2] ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => (value ?? string.Empty).Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (string.IsNullOrWhiteSpace(regionName) ||
                    string.IsNullOrWhiteSpace(displayCountry))
                {
                    continue;
                }

                if (!countries.Any(value => string.Equals(value, displayCountry, StringComparison.OrdinalIgnoreCase)))
                {
                    countries.Insert(0, displayCountry);
                }

                aliases.Add(new CountryAliasGroup
                {
                    RegionName = regionName,
                    DisplayCountry = displayCountry,
                    Countries = countries
                });
            }

            return aliases;
        }

        private static void AddDistinctText(ICollection<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || values == null)
            {
                return;
            }

            if (values.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            values.Add(value);
        }

        private static bool MatchesTextFilter(string value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return string.Equals(value ?? string.Empty, filter, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCachedFilterPayload(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl))
            {
                return null;
            }

            return HttpRuntime.Cache[BuildFilterCacheKey(requestUrl)] as string;
        }

        private static void CacheFilterPayload(string requestUrl, string payload)
        {
            if (string.IsNullOrWhiteSpace(requestUrl) || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            HttpRuntime.Cache.Insert(
                BuildFilterCacheKey(requestUrl),
                payload,
                null,
                DateTime.Now.AddMinutes(FilterPayloadCacheMinutes),
                Cache.NoSlidingExpiration);
        }

        private static string BuildFilterCacheKey(string requestUrl)
        {
            return "RegionFilterPayload::" + requestUrl.Trim();
        }

        public void SaveLoginOtp(LoginOtp challenge)
        {
            ExecuteNonQuery(@"INSERT INTO UserLoginOtp (UserId, OtpHash, OtpSalt, ExpiresOnUtc, IsUsed, CreatedOnUtc) VALUES (@UserId, @OtpHash, @OtpSalt, @ExpiresOnUtc, @IsUsed, @CreatedOnUtc)", cmd =>
            {
                cmd.Parameters.AddWithValue("@UserId", challenge.UserId);
                cmd.Parameters.AddWithValue("@OtpHash", challenge.OtpHash);
                cmd.Parameters.AddWithValue("@OtpSalt", challenge.OtpSalt);
                cmd.Parameters.AddWithValue("@ExpiresOnUtc", challenge.ExpiresOnUtc);
                cmd.Parameters.AddWithValue("@IsUsed", challenge.IsUsed);
                cmd.Parameters.AddWithValue("@CreatedOnUtc", challenge.CreatedOnUtc);
            });
        }

        public LoginOtp GetLatestActiveOtp(int userId)
        {
            const string sql = @"SELECT TOP 1 LoginOtpId, UserId, OtpHash, OtpSalt, ExpiresOnUtc, IsUsed, CreatedOnUtc FROM UserLoginOtp WHERE UserId = @UserId AND IsUsed = 0 ORDER BY CreatedOnUtc DESC";
            return ExecuteSingle(sql, cmd => cmd.Parameters.AddWithValue("@UserId", userId), reader => new LoginOtp
            {
                LoginOtpId = Convert.ToInt32(reader["LoginOtpId"]),
                UserId = Convert.ToInt32(reader["UserId"]),
                OtpHash = reader["OtpHash"].ToString(),
                OtpSalt = reader["OtpSalt"].ToString(),
                ExpiresOnUtc = Convert.ToDateTime(reader["ExpiresOnUtc"]),
                IsUsed = Convert.ToBoolean(reader["IsUsed"]),
                CreatedOnUtc = Convert.ToDateTime(reader["CreatedOnUtc"])
            });
        }

        public void MarkOtpUsed(int loginOtpId)
        {
            ExecuteNonQuery("UPDATE UserLoginOtp SET IsUsed = 1 WHERE LoginOtpId = @LoginOtpId", cmd => cmd.Parameters.AddWithValue("@LoginOtpId", loginOtpId));
        }

        public void ExpireOtpChallenges(int userId)
        {
            ExecuteNonQuery("UPDATE UserLoginOtp SET IsUsed = 1 WHERE UserId = @UserId AND IsUsed = 0", cmd => cmd.Parameters.AddWithValue("@UserId", userId));
        }

        public void CreatePasswordResetToken(int userId, string tokenHash, string tokenSalt, DateTime expiresOnUtc)
        {
            ExecuteNonQuery("UPDATE PasswordResetTokens SET IsUsed = 1 WHERE UserId = @UserId AND IsUsed = 0", cmd => cmd.Parameters.AddWithValue("@UserId", userId));
            ExecuteNonQuery(@"INSERT INTO PasswordResetTokens (UserId, TokenHash, TokenSalt, ExpiresOnUtc, IsUsed, CreatedOnUtc) VALUES (@UserId, @TokenHash, @TokenSalt, @ExpiresOnUtc, 0, @CreatedOnUtc)", cmd =>
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@TokenHash", tokenHash);
                cmd.Parameters.AddWithValue("@TokenSalt", tokenSalt);
                cmd.Parameters.AddWithValue("@ExpiresOnUtc", expiresOnUtc);
                cmd.Parameters.AddWithValue("@CreatedOnUtc", DateTime.UtcNow);
            });
        }

        public PasswordResetToken GetLatestActivePasswordResetToken(int userId)
        {
            const string sql = @"SELECT TOP 1 PasswordResetTokenId, UserId, TokenHash, TokenSalt, ExpiresOnUtc, IsUsed, CreatedOnUtc FROM PasswordResetTokens WHERE UserId = @UserId AND IsUsed = 0 ORDER BY CreatedOnUtc DESC";
            return ExecuteSingle(sql, cmd => cmd.Parameters.AddWithValue("@UserId", userId), ReadPasswordResetToken);
        }

        public PasswordResetToken GetPasswordResetTokenByRawToken(string rawToken)
        {
            var candidates = ExecuteList("SELECT PasswordResetTokenId, UserId, TokenHash, TokenSalt, ExpiresOnUtc, IsUsed, CreatedOnUtc FROM PasswordResetTokens WHERE IsUsed = 0 ORDER BY CreatedOnUtc DESC", null, ReadPasswordResetToken);
            var hasher = new PasswordHasher();
            foreach (var token in candidates)
            {
                if (hasher.VerifyPassword(rawToken, token.TokenSalt, token.TokenHash))
                {
                    return token;
                }
            }
            return null;
        }

        public void MarkPasswordResetTokenUsed(int passwordResetTokenId)
        {
            ExecuteNonQuery("UPDATE PasswordResetTokens SET IsUsed = 1 WHERE PasswordResetTokenId = @PasswordResetTokenId", cmd => cmd.Parameters.AddWithValue("@PasswordResetTokenId", passwordResetTokenId));
        }

        public void LogUserActivity(UserActivityLog log)
        {
            ExecuteNonQuery(@"INSERT INTO UserActivityLogs (UserId, Username, SessionKey, ActionName, Details, IpAddress, LocationText, UserAgent, CreatedOnUtc) VALUES (@UserId, @Username, @SessionKey, @ActionName, @Details, @IpAddress, @LocationText, @UserAgent, @CreatedOnUtc)", cmd =>
            {
                cmd.Parameters.AddWithValue("@UserId", log.UserId);
                cmd.Parameters.AddWithValue("@Username", log.Username ?? string.Empty);
                cmd.Parameters.AddWithValue("@SessionKey", log.SessionKey ?? string.Empty);
                cmd.Parameters.AddWithValue("@ActionName", log.ActionName ?? string.Empty);
                var details = log.Details ?? string.Empty; if (details.Length > 2000) details = details.Substring(0, 2000); cmd.Parameters.AddWithValue("@Details", (object)details);
                cmd.Parameters.AddWithValue("@IpAddress", (object)(log.IpAddress ?? string.Empty));
                var locationText = log.LocationText ?? string.Empty; if (locationText.Length > 256) locationText = locationText.Substring(0, 256); cmd.Parameters.AddWithValue("@LocationText", (object)locationText);
                var userAgent = log.UserAgent ?? string.Empty; if (userAgent.Length > 512) userAgent = userAgent.Substring(0, 512); cmd.Parameters.AddWithValue("@UserAgent", (object)userAgent);
                cmd.Parameters.AddWithValue("@CreatedOnUtc", log.CreatedOnUtc);
            });
        }

        public IList<UserActivityLog> GetRecentActivityLogs(int count)
        {
            const string sql = @"SELECT TOP (@Count) ActivityLogId, UserId, Username, SessionKey, ActionName, Details, IpAddress, LocationText, UserAgent, CreatedOnUtc FROM UserActivityLogs ORDER BY CreatedOnUtc DESC";
            return ExecuteList(sql, cmd => cmd.Parameters.AddWithValue("@Count", count), ReadActivityLog);
        }

        public IList<UserActivityLog> SearchActivityLogs(ActivityLogFilterViewModel filter)
        {
            const string sql = @"SELECT TOP (@Top) ActivityLogId, UserId, Username, SessionKey, ActionName, Details, IpAddress, LocationText, UserAgent, CreatedOnUtc FROM UserActivityLogs WHERE (@Username = '' OR Username LIKE '%' + @Username + '%') AND (@ActionName = '' OR ActionName = @ActionName) AND (@IpAddress = '' OR IpAddress LIKE '%' + @IpAddress + '%') AND (@FromUtc IS NULL OR CreatedOnUtc >= @FromUtc) AND (@ToUtc IS NULL OR CreatedOnUtc <= @ToUtc) ORDER BY CreatedOnUtc DESC";
            return ExecuteList(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@Top", filter.Top <= 0 ? 100 : filter.Top);
                cmd.Parameters.AddWithValue("@Username", filter.Username ?? string.Empty);
                cmd.Parameters.AddWithValue("@ActionName", filter.ActionName ?? string.Empty);
                cmd.Parameters.AddWithValue("@IpAddress", filter.IpAddress ?? string.Empty);
                cmd.Parameters.Add("@FromUtc", SqlDbType.DateTime).Value = filter.FromUtc.HasValue ? (object)filter.FromUtc.Value : DBNull.Value;
                cmd.Parameters.Add("@ToUtc", SqlDbType.DateTime).Value = filter.ToUtc.HasValue ? (object)filter.ToUtc.Value : DBNull.Value;
            }, ReadActivityLog);
        }

        private IList<RegionApiTarget> GetAccessibleRegionUrls(int userId, int regionId, bool isAdmin)
        {
            const string adminSql = @"
                WITH RankedRegionUrls AS (
                    SELECT
                        ru.RegionId,
                        ru.Url,
                        ROW_NUMBER() OVER (PARTITION BY ru.RegionId ORDER BY ru.Id DESC) AS RowNum
                    FROM RegionUrls ru
                    WHERE ru.IsActive = 1
                )
                SELECT rru.RegionId, rru.Url, r.Name
                FROM RankedRegionUrls rru
                INNER JOIN Regions r ON r.RegionId = rru.RegionId
                WHERE rru.RowNum = 1
                  AND r.IsActive = 1
                  AND (@RegionId = 0 OR rru.RegionId = @RegionId)";
            const string userSql = @"
                WITH RankedRegionUrls AS (
                    SELECT
                        ru.RegionId,
                        ru.Url,
                        ROW_NUMBER() OVER (PARTITION BY ru.RegionId ORDER BY ru.Id DESC) AS RowNum
                    FROM RegionUrls ru
                    WHERE ru.IsActive = 1
                )
                SELECT rru.RegionId, rru.Url, r.Name
                FROM RankedRegionUrls rru
                INNER JOIN Regions r ON r.RegionId = rru.RegionId
                INNER JOIN UserRegions ur ON ur.RegionId = rru.RegionId
                WHERE rru.RowNum = 1
                  AND r.IsActive = 1
                  AND ur.UserId = @UserId
                  AND (@RegionId = 0 OR rru.RegionId = @RegionId)";
            return ExecuteList(isAdmin ? adminSql : userSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@RegionId", regionId);
                if (!isAdmin) cmd.Parameters.AddWithValue("@UserId", userId);
            }, reader => new RegionApiTarget
            {
                RegionId = Convert.ToInt32(reader["RegionId"]),
                Url = NormalizeRegionApiBaseUrl(reader["Url"].ToString()),
                RegionName = reader["Name"].ToString()
            });
        }

        private static void EnsureRegionApiAvailability(string operationLabel, int regionId, IList<RegionApiTarget> regionUrls, IList<string> failures, bool hasSuccessfulResponse)
        {
            if (regionUrls.Count == 0)
            {
                if (regionId > 0)
                {
                    throw new InvalidOperationException("No active backend URL is configured for the selected region. Please update Admin > Region URL Configuration.");
                }

                return;
            }

            if (hasSuccessfulResponse || failures.Count == 0)
            {
                return;
            }

            var regionName = regionUrls.Select(item => item.RegionName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "selected";
            throw new InvalidOperationException(string.Format(
                "{0} could not be loaded because the {1} backend is unreachable. {2} Please update Admin > Region URL Configuration or verify network access to that backend.",
                ToSentenceCase(operationLabel),
                regionName,
                failures[0]));
        }

        private static void TrackRegionApiFailure(string operationLabel, RegionApiTarget regionUrl, string requestUrl, Exception ex, IList<string> failures)
        {
            var summary = string.Format("Backend URL {0} failed: {1}", requestUrl, GetErrorSummary(ex));
            failures.Add(summary);
            LocalDiagnostics.Log("RegionApiFailure", string.Format(
                "Operation: {0}{4}Region: {1} (#{2}){4}URL: {3}{4}{5}",
                operationLabel,
                regionUrl.RegionName ?? string.Empty,
                regionUrl.RegionId,
                requestUrl,
                Environment.NewLine,
                ex));
        }

        private static string ToSentenceCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Data";
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private string GetJson(string url)
        {
            return GetJson(url, DefaultDownstreamTimeoutMilliseconds);
        }

        private string GetJson(string url, int timeoutMilliseconds)
        {
            try
            {
                var request = WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = timeoutMilliseconds;
                var httpRequest = request as HttpWebRequest;
                if (httpRequest != null)
                {
                    httpRequest.ReadWriteTimeout = timeoutMilliseconds;
                }
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                var statusMessage = response == null ? string.Empty : string.Format("HTTP {0} {1}. ", (int)response.StatusCode, response.StatusDescription);
                throw new DownstreamRequestException("Downstream request failed. " + statusMessage + GetErrorSummary(ex), ex, response == null ? (HttpStatusCode?)null : response.StatusCode, ex.Status);
            }
            catch (Exception ex)
            {
                throw new DownstreamRequestException("Downstream request failed. " + GetErrorSummary(ex), ex, null, WebExceptionStatus.UnknownError);
            }
        }

        private static bool ShouldFallbackToReportValues(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            var downstreamException = ex as DownstreamRequestException;
            if (downstreamException != null && downstreamException.StatusCode.HasValue)
            {
                return downstreamException.StatusCode.Value == HttpStatusCode.NotFound ||
                       downstreamException.StatusCode.Value == HttpStatusCode.MethodNotAllowed ||
                       downstreamException.StatusCode.Value == HttpStatusCode.NotImplemented;
            }

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("(404) Not Found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("404 Not Found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("(405) Method Not Allowed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("(501) Not Implemented", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildRegionApiUrl(string baseUrl, string relativePath)
        {
            var normalizedBaseUrl = NormalizeRegionApiBaseUrl(baseUrl);
            var normalizedRelativePath = (relativePath ?? string.Empty).TrimStart('/');
            return string.IsNullOrWhiteSpace(normalizedRelativePath)
                ? normalizedBaseUrl
                : normalizedBaseUrl + "/" + normalizedRelativePath;
        }

        private static string NormalizeRegionApiBaseUrl(string url)
        {
            url = (url ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            Uri parsedUrl;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsedUrl))
            {
                return url.TrimEnd('/');
            }

            var builder = new UriBuilder(parsedUrl);
            var path = builder.Path ?? string.Empty;
            while (path.Contains("//"))
            {
                path = path.Replace("//", "/");
            }

            builder.Path = path.TrimEnd('/');
            var normalizedUrl = builder.Uri.GetLeftPart(UriPartial.Authority) + builder.Path;
            return normalizedUrl.TrimEnd('/');
        }

        private static string GetErrorSummary(Exception ex)
        {
            var current = ex;
            while (current.InnerException != null && !string.IsNullOrWhiteSpace(current.InnerException.Message))
            {
                current = current.InnerException;
            }

            return current.Message;
        }

        private IList<object> ParseJsonArray(string payload)
        {
            return CoerceObjectList(_serializer.DeserializeObject(payload));
        }

        private IList<object> ReadJsonArray(IDictionary<string, object> map, params string[] keys)
        {
            object value;
            return TryGetValue(map, out value, keys) ? CoerceObjectList(value) : new List<object>();
        }

        private IList<object> CoerceObjectList(object value)
        {
            var arrayList = value as ArrayList;
            if (arrayList != null)
            {
                return arrayList.Cast<object>().ToList();
            }

            var objectArray = value as object[];
            if (objectArray != null)
            {
                return objectArray.ToList();
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                return enumerable.Cast<object>().ToList();
            }

            return value == null ? new List<object>() : new List<object> { value };
        }

        private static bool TryGetValue(IDictionary<string, object> map, out object value, params string[] keys)
        {
            value = null;
            if (map == null || keys == null)
            {
                return false;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (map.TryGetValue(key, out value) && value != null)
                {
                    return true;
                }
            }

            var normalizedKeys = map.Keys.ToDictionary(NormalizeKey, key => key, StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                var normalizedKey = NormalizeKey(key);
                string actualKey;
                if (!normalizedKeys.TryGetValue(normalizedKey, out actualKey))
                {
                    continue;
                }

                if (map.TryGetValue(actualKey, out value) && value != null)
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return new string(key.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private static string ReadString(IDictionary<string, object> map, params string[] keys)
        {
            object value;
            return TryGetValue(map, out value, keys) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static int ReadInt(IDictionary<string, object> map, params string[] keys)
        {
            object value;
            if (!TryGetValue(map, out value, keys) || value == null)
            {
                return 0;
            }

            if (value is int) return (int)value;
            if (value is long) return Convert.ToInt32(value);
            if (value is decimal) return Convert.ToInt32(value);
            if (value is double) return Convert.ToInt32(Math.Round((double)value));

            int intValue;
            if (int.TryParse(Convert.ToString(value), out intValue))
            {
                return intValue;
            }

            decimal decimalValue;
            return decimal.TryParse(Convert.ToString(value), out decimalValue) ? Convert.ToInt32(decimalValue) : 0;
        }

        private static decimal ReadDecimal(IDictionary<string, object> map, params string[] keys)
        {
            object value;
            if (!TryGetValue(map, out value, keys) || value == null)
            {
                return 0m;
            }

            if (value is decimal) return (decimal)value;
            if (value is double) return Convert.ToDecimal(value);
            if (value is int || value is long) return Convert.ToDecimal(value);

            decimal decimalValue;
            return decimal.TryParse(Convert.ToString(value), out decimalValue) ? decimalValue : 0m;
        }

        private static DateTime ReadDate(IDictionary<string, object> map, params string[] keys)
        {
            var raw = ReadString(map, keys);
            DateTime value;
            return DateTime.TryParse(raw, out value) ? value : DateTime.MinValue;
        }

        private SqlConnection OpenConnection()
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private void ExecuteNonQuery(string sql, Action<SqlCommand> parameterize)
        {
            using (var conn = OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                parameterize(cmd);
                cmd.ExecuteNonQuery();
            }
        }

        private T ExecuteSingle<T>(string sql, Action<SqlCommand> parameterize, Func<IDataRecord, T> mapper) where T : class
        {
            using (var conn = OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (parameterize != null) parameterize(cmd);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read() ? mapper(reader) : null;
                }
            }
        }

        private IList<T> ExecuteList<T>(string sql, Action<SqlCommand> parameterize, Func<IDataRecord, T> mapper)
        {
            var list = new List<T>();
            using (var conn = OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (parameterize != null) parameterize(cmd);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) list.Add(mapper(reader));
                }
            }
            return list;
        }

        private static User ReadUser(IDataRecord reader)
        {
            return new User
            {
                UserId = Convert.ToInt32(reader["UserId"]),
                Username = reader["Username"].ToString(),
                Email = reader["Email"].ToString(),
                PasswordHash = reader["PasswordHash"].ToString(),
                PasswordSalt = reader["PasswordSalt"].ToString(),
                Role = reader["Role"].ToString(),
                IsActive = Convert.ToBoolean(reader["IsActive"])
            };
        }

        private static Region ReadRegion(IDataRecord reader)
        {
            return new Region
            {
                RegionId = Convert.ToInt32(reader["RegionId"]),
                Name = reader["Name"].ToString(),
                IsActive = Convert.ToBoolean(reader["IsActive"])
            };
        }

        private static RegionUrl ReadRegionUrl(IDataRecord reader)
        {
            return new RegionUrl
            {
                Id = Convert.ToInt32(reader["Id"]),
                RegionId = Convert.ToInt32(reader["RegionId"]),
                Url = reader["Url"].ToString(),
                IsActive = Convert.ToBoolean(reader["IsActive"])
            };
        }

        private static UserActivityLog ReadActivityLog(IDataRecord reader)
        {
            return new UserActivityLog
            {
                ActivityLogId = Convert.ToInt32(reader["ActivityLogId"]),
                UserId = Convert.ToInt32(reader["UserId"]),
                Username = reader["Username"].ToString(),
                SessionKey = reader["SessionKey"].ToString(),
                ActionName = reader["ActionName"].ToString(),
                Details = reader["Details"].ToString(),
                IpAddress = reader["IpAddress"].ToString(),
                LocationText = reader["LocationText"].ToString(),
                UserAgent = reader["UserAgent"].ToString(),
                CreatedOnUtc = Convert.ToDateTime(reader["CreatedOnUtc"])
            };
        }

        private static PasswordResetToken ReadPasswordResetToken(IDataRecord reader)
        {
            return new PasswordResetToken
            {
                PasswordResetTokenId = Convert.ToInt32(reader["PasswordResetTokenId"]),
                UserId = Convert.ToInt32(reader["UserId"]),
                TokenHash = reader["TokenHash"].ToString(),
                TokenSalt = reader["TokenSalt"].ToString(),
                ExpiresOnUtc = Convert.ToDateTime(reader["ExpiresOnUtc"]),
                IsUsed = Convert.ToBoolean(reader["IsUsed"]),
                CreatedOnUtc = Convert.ToDateTime(reader["CreatedOnUtc"])
            };
        }

        private class RegionApiTarget
        {
            public int RegionId { get; set; }
            public string Url { get; set; }
            public string RegionName { get; set; }
            public string OverrideCountry { get; set; }
        }

        private class CountryBackendOverride
        {
            public string RegionName { get; set; }
            public string Country { get; set; }
            public string Url { get; set; }
        }

        private class CountryAliasGroup
        {
            public string RegionName { get; set; }
            public string DisplayCountry { get; set; }
            public IList<string> Countries { get; set; }
        }

        private class ReportFetchResult
        {
            public IList<ReportRow> Rows { get; set; }
            public bool HasSuccessfulResponse { get; set; }
        }

        private class DownstreamRequestException : InvalidOperationException
        {
            public DownstreamRequestException(string message, Exception innerException, HttpStatusCode? statusCode, WebExceptionStatus webExceptionStatus)
                : base(message, innerException)
            {
                StatusCode = statusCode;
                WebExceptionStatus = webExceptionStatus;
            }

            public HttpStatusCode? StatusCode { get; private set; }
            public WebExceptionStatus WebExceptionStatus { get; private set; }
        }
    }
}



