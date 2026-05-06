# VAS Reporting Tool — Dev Session Notes
**Date:** 5 May 2026  
**Project:** VASReportingTool (ASP.NET MVC 4 / .NET Framework 4.0)  
**Developer:** Samadhan Jadhav  

---

## 📁 Project Overview

| Property | Value |
|---|---|
| **Type** | ASP.NET MVC 4 Web Application |
| **Framework** | .NET Framework 4.0 |
| **Solution File** | `VASReportingTool.sln` |
| **Project File** | `VASReportingTool.csproj` |
| **Database** | SQL Server — `VASReportingToolDb` (remote: `54.208.56.55`) |
| **Local Run** | IIS Express on `http://localhost:8080` |
| **Build Tool** | MSBuild VS 2017 |

---

## 🔐 Login Credentials

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `Admin@123` |
| User | `report.user` | `User@123` |

---

## 🛠️ Build & Run Commands

### Build
```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" `
  VASReportingTool.sln /p:Configuration=Debug /p:Platform="Any CPU" /v:m
```

### Restore NuGet Packages
```powershell
.\nuget.exe restore VASReportingTool.sln -PackagesDirectory packages
```

### Start IIS Express
```powershell
$appHostConfig = "$env:USERPROFILE\Documents\IISExpress\config\applicationhost.config"
Start-Process -FilePath "C:\Program Files (x86)\IIS Express\iisexpress.exe" `
    -ArgumentList "/config:`"$appHostConfig`" /site:VASReportingTool /trace:error" `
    -WindowStyle Normal
```

### Open in Browser
```powershell
Start-Process "http://localhost:8080/Account/Login"
```

### Stop IIS Express
```powershell
Stop-Process -Name "iisexpress" -Force
```

---

## ⚙️ IIS Express Setup (First-Time)

IIS Express requires a proper `applicationhost.config` with site definition.  
This was created once and lives at:
```
C:\Users\HP\Documents\IISExpress\config\applicationhost.config
```

Steps taken to set it up:
```powershell
# 1. Create config directory
$configDir = "$env:USERPROFILE\Documents\IISExpress\config"
New-Item -ItemType Directory -Force -Path $configDir

# 2. Copy default template
Copy-Item "C:\Program Files (x86)\IIS Express\config\templates\PersonalWebServer\applicationhost.config" `
          "$configDir\applicationhost.config" -Force

# 3. Inject VASReportingTool site via PowerShell XML manipulation
#    - Site name: VASReportingTool
#    - Port: 8080
#    - Binding: *:8080:localhost
#    - Physical path: <project root>
```

---

## 🗄️ Database

### Connection (Web.config — current)
```xml
<add name="ReportingDb"
     connectionString="Data Source=54.208.56.55;Initial Catalog=VASReportingToolDb;User ID=sa;pwd=Nazara@123"
     providerName="System.Data.SqlClient" />
```

### LocalDB Alternative (if remote is unavailable)
```xml
<add name="ReportingDb"
     connectionString="Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=VASReportingToolDb;Integrated Security=True"
     providerName="System.Data.SqlClient" />
```

### Start LocalDB
```powershell
& "C:\Program Files\Microsoft SQL Server\130\Tools\Binn\SqlLocalDB.exe" start MSSQLLocalDB
```

### Create DB & Run Scripts (LocalDB)
```powershell
$sqlcmd = "C:\Program Files\Microsoft SQL Server\110\Tools\Binn\SQLCMD.EXE"
& $sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "CREATE DATABASE VASReportingToolDb"
& $sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d VASReportingToolDb -i "Sql\schema.sql"
& $sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d VASReportingToolDb -i "Sql\seed.sql"
```

---

## 🔧 Changes Made This Session

---

### 1. Missing Reports — `hasData` Logic Fix
**File:** `Controllers/ToolSummaryController.cs`

**Old logic** (marked day as present if ANY metric > 0):
```csharp
var hasData = row.TotalRevenue    > 0
           || row.ActivationCount > 0
           || row.RenewalCount    > 0
           || row.UserChurn       > 0
           || row.SystemChurn     > 0;
if (!hasData) continue;
```

**New logic** (a row existing at all = day is present, regardless of values):
```csharp
// hasData check removed entirely
// Any row returned from API for that service/day = Green
// No row at all = Red
```

**Rule now:**
| Scenario | Result |
|---|---|
| API returns **no row** for that service/day | 🔴 Red — Missing |
| API returns a row (any values, even all zeros) | ✅ Green — Present |

---

### 2. Missing Reports — Performance Fix
**File:** `Controllers/ToolSummaryController.cs`

**Problem:** Old code made nested sequential HTTP calls for every Region → Country → Operator → Service combination via filter API endpoints (`GetCountries` → `GetOperators` → `GetServices`). This caused N×M×O extra HTTP calls on every page load — very slow.

**Fix:** Derive the master combination list **directly from `monthRows`** (already fetched in one bulk API call), eliminating all extra filter API calls.

**Old flow:**
```
1 bulk API call (month data)
+ GetCountries()  per region          ← extra HTTP call
  + GetOperators()  per country       ← extra HTTP call × N
    + GetServices() per operator      ← extra HTTP call × N×M
```

**New flow:**
```
1 bulk API call (month data)
+ .GroupBy() on already-fetched rows  ← pure in-memory, zero extra HTTP
```

**Key code change:**
```csharp
// OLD — nested loops with API calls
foreach (var region in regions)
{
    var countries = _repository.GetCountries(...);       // HTTP call
    foreach (var country in countries)
    {
        var operators = _repository.GetOperators(...);   // HTTP call
        foreach (var op in operators)
        {
            var services = _repository.GetServices(...); // HTTP call
            // ...
        }
    }
}

// NEW — in-memory groupBy from already-fetched monthRows
var combinations = monthRows
    .GroupBy(r => Key(r.RegionName, r.Country, r.OperatorName, r.ServiceName))
    .Select(g => g.First())
    .OrderBy(r => r.RegionName).ThenBy(r => r.Country)
    .ThenBy(r => r.OperatorName).ThenBy(r => r.ServiceName)
    .ToList();

foreach (var combo in combinations)
{
    // check presence map — no HTTP calls needed
}
```

---

### 3. Dashboard Table — Excel Freeze Panes
**File:** `Content/site.css`

Froze the first 4 columns (DATE, COUNTRY, OPERATOR, SERVICE) using CSS `position: sticky` with cumulative `left` offsets — identical behaviour to Excel freeze panes.

**Column layout:**
| Column | Fixed Width | `left` Offset |
|---|---|---|
| DATE (col 1) | 84px | `0` |
| COUNTRY (col 2) | 104px | `84px` |
| OPERATOR (col 3) | 104px | `188px` |
| SERVICE (col 4) | 124px | `292px` ← freeze boundary |

**CSS added:**
```css
/* Fix widths so cumulative left offsets are exact */
.report-table th:nth-child(1), .report-table td:nth-child(1) { min-width: 84px;  width: 84px; }
.report-table th:nth-child(2), .report-table td:nth-child(2) { min-width: 104px; width: 104px; }
.report-table th:nth-child(3), .report-table td:nth-child(3) { min-width: 104px; width: 104px; }
.report-table th:nth-child(4), .report-table td:nth-child(4) { min-width: 124px; width: 124px; }

/* Sticky positions */
.report-table th:nth-child(1), .report-table td:nth-child(1) { position: sticky; left: 0;     z-index: 3; background: #fff; }
.report-table th:nth-child(2), .report-table td:nth-child(2) { position: sticky; left: 84px;  z-index: 3; background: #fff; }
.report-table th:nth-child(3), .report-table td:nth-child(3) { position: sticky; left: 188px; z-index: 3; background: #fff; }
.report-table th:nth-child(4), .report-table td:nth-child(4) {
    position: sticky; left: 292px; z-index: 3; background: #fff;
    border-right: 2px solid var(--border);
    box-shadow: 3px 0 8px rgba(15, 23, 42, 0.09); /* freeze boundary shadow */
}

/* Header frozen cells — highest z-index */
.report-table thead th:nth-child(1),
.report-table thead th:nth-child(2),
.report-table thead th:nth-child(3),
.report-table thead th:nth-child(4) { z-index: 5; background: #fff; }

/* Frozen cells stay white on row hover */
.report-table tbody tr:hover td:nth-child(1),
.report-table tbody tr:hover td:nth-child(2),
.report-table tbody tr:hover td:nth-child(3),
.report-table tbody tr:hover td:nth-child(4) { background: #fff; }
```

---

## 📦 NuGet Packages

| Package | Version | Target |
|---|---|---|
| Microsoft.AspNet.Mvc | 4.0.40804.0 | net40 |
| Microsoft.AspNet.Razor | 2.0.30506.0 | net40 |
| Microsoft.AspNet.WebPages | 2.0.30506.0 | net40 |
| Microsoft.Web.Infrastructure | 1.0.0 | net40 |

---

## 📂 Key File Locations

| File | Purpose |
|---|---|
| `Controllers/ToolSummaryController.cs` | Missing Reports logic (Data(), HourlyData()) |
| `Controllers/DashboardController.cs` | Main dashboard |
| `Repositories/SqlReportingRepository.cs` | All DB + API data access |
| `Content/site.css` | All UI styles including freeze panes |
| `Scripts/dashboard.js` | Frontend table rendering, charts, filters |
| `Views/Dashboard/Index.cshtml` | Dashboard page layout |
| `Views/ToolSummary/Index.cshtml` | Missing Reports page |
| `Sql/schema.sql` | DB table definitions |
| `Sql/seed.sql` | Seed users, regions, region URLs |
| `Web.config` | Local config (DB, SMTP, API keys) — not tracked in Git |
| `Web.config.example` | Safe template tracked in Git |

---

## 🌐 App Routes

| URL | Page |
|---|---|
| `/Account/Login` | Login page |
| `/Dashboard/Index` | Main reports dashboard |
| `/ToolSummary/Index` | Missing Reports |
| `/ToolSummary/Hourly` | Hourly data view |
| `/Admin/Index` | Admin panel (users, region URLs, activity logs) |

---

## 📝 Notes & Gotchas

- `Web.config` is **gitignored** intentionally — contains real DB/SMTP credentials. Use `Web.config.example` as the template.
- IIS Express `applicationhost.config` must be set up once per machine (see setup steps above).
- The **Missing Reports** page only shows services that appear in the current month's bulk API response. Services with **zero rows for the entire month** will not appear (no filter API calls are made).
- The **freeze panes** use CSS `position: sticky` — requires the `table-wrap` div to have `overflow: auto` (already set in site.css).
- Build output goes to `bin\VASReportingTool.dll`.
- MSBuild path: `C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe`
- sqlcmd path: `C:\Program Files\Microsoft SQL Server\110\Tools\Binn\SQLCMD.EXE`
- LocalDB path: `C:\Program Files\Microsoft SQL Server\130\Tools\Binn\SqlLocalDB.exe`
