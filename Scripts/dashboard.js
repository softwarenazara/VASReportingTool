(function () {
    function byId(id) {
        return document.getElementById(id);
    }

    var root = document.querySelector(".dashboard-app");
    if (!root) {
        return;
    }

    var ChartLibrary = window.Chart;

    var state = {
        selectedRegionId: root.getAttribute("data-default-region-id") || "",
        api: {
            reporting: root.getAttribute("data-reporting-url") || "/api/reporting",
            countries: root.getAttribute("data-countries-url") || "/api/regions/countries",
            operators: root.getAttribute("data-operators-url") || "/api/regions/operators",
            services: root.getAttribute("data-services-url") || "/api/regions/services"
        },
        rawRows: [],
        previousRawRows: [],
        periodRows: [],
        charts: {},
        activeChartTab: "",
        viewMode: "daily",
        activeCurrency: "local",
        exchangeRates: {},
        ratesLoaded: false,
        ratesPromise: null,
        previousComparisonNote: "",
        availableOperators: []
    };

    var metricColumns = [
        { key: "TotalVisitors", label: "Visitors", accent: "blue", format: "number" },
        { key: "UniqueVisitors", label: "Unique Visitors", accent: "cyan", format: "number" },
        { key: "ActivationAttempts", label: "Attempts", accent: "violet", format: "number" },
        { key: "FreeTrials", label: "Free Trials", accent: "amber", format: "number" },
        { key: "ActivationCount", label: "Activations", accent: "emerald", format: "number" },
        { key: "ActivationRevenue", label: "Activation Revenue", accent: "teal", format: "currency" },
        { key: "RenewalCount", label: "Renewals", accent: "cyan", format: "number" },
        { key: "RenewalRevenue", label: "Renewal Revenue", accent: "amber", format: "currency" },
        { key: "TotalRevenue", label: "Total Revenue", accent: "blue", format: "currency" },
        { key: "GrossBase", label: "Gross Base", accent: "violet", format: "number" },
        { key: "ActiveBase", label: "Active Base", accent: "teal", format: "number" },
        { key: "SystemChurn", label: "System Churn", accent: "cyan", format: "number" },
        { key: "UserChurn", label: "User Churn", accent: "amber", format: "number" },
        { key: "Churn", label: "Churn", accent: "rose", format: "number" },
        { key: "ActivationRate", label: "Activation Rate", accent: "emerald", format: "percent" },
        { key: "RevenuePerActiveBase", label: "Revenue / Active Base", accent: "blue", format: "currency" },
        { key: "ActivationUnitRevenue", label: "Activation Revenue / Activation", accent: "emerald", format: "currency" },
        { key: "RenewalUnitRevenue", label: "Renewal Revenue / Renewal", accent: "amber", format: "currency" },
        { key: "RevenueShare", label: "Revenue Share", accent: "blue", format: "percent" },
        { key: "ActivationShare", label: "Activation Share", accent: "emerald", format: "percent" },
        { key: "RenewalCountShare", label: "Renewal Share", accent: "amber", format: "percent" },
        { key: "ChurnShare", label: "Churn Share", accent: "rose", format: "percent" }
    ];

    var chartColors = {
        blue: { stroke: "#2563eb", fill: "rgba(37,99,235,.18)" },
        cyan: { stroke: "#0891b2", fill: "rgba(8,145,178,.18)" },
        emerald: { stroke: "#059669", fill: "rgba(5,150,105,.18)" },
        amber: { stroke: "#d97706", fill: "rgba(217,119,6,.18)" },
        teal: { stroke: "#0d9488", fill: "rgba(13,148,136,.18)" },
        rose: { stroke: "#e11d48", fill: "rgba(225,29,72,.18)" },
        violet: { stroke: "#7c3aed", fill: "rgba(124,58,237,.18)" }
    };

    var accentCycle = ["blue", "emerald", "amber", "violet", "rose", "teal", "cyan"];

    var revenueColumnKeys = ["ActivationRevenue", "RenewalRevenue", "TotalRevenue"];

    var countryCurrency = {
        Nigeria: "NGN", Kenya: "KES", Ghana: "GHS", "South Africa": "ZAR",
        Tanzania: "TZS", Uganda: "UGX", Rwanda: "RWF", Ethiopia: "ETB",
        Senegal: "XOF", "Ivory Coast": "XOF", "Cote d'Ivoire": "XOF",
        Cameroon: "XAF", Congo: "XAF", "Congo B": "XAF", "Congo DRC": "CDF",
        "DR Congo": "CDF", DRC: "CDF", Gabon: "XAF", Chad: "XAF",
        Mali: "XOF", "Burkina Faso": "XOF", Benin: "XOF", Togo: "XOF",
        Niger: "XOF", Guinea: "GNF", "Sierra Leone": "SLE",
        Mozambique: "MZN", Zambia: "ZMW", Zimbabwe: "ZWL", Malawi: "MWK",
        Botswana: "BWP", Namibia: "NAD", Mauritius: "MUR", Madagascar: "MGA",
        Angola: "AOA", Liberia: "LRD", Gambia: "GMD",
        Morocco: "MAD", Tunisia: "TND", Algeria: "DZD", Egypt: "EGP",
        Jordan: "JOD", Iraq: "IQD", "Saudi Arabia": "SAR", UAE: "AED",
        Kuwait: "KWD", Bahrain: "BHD", Qatar: "QAR", Oman: "OMR",
        Lebanon: "LBP", Libya: "LYD", Sudan: "SDG", Yemen: "YER",
        Palestine: "ILS", Syria: "SYP",
        India: "INR", Pakistan: "PKR", Bangladesh: "BDT", "Sri Lanka": "LKR",
        Nepal: "NPR", Myanmar: "MMK", Thailand: "THB", Vietnam: "VND",
        Indonesia: "IDR", Philippines: "PHP", Malaysia: "MYR",
        Cambodia: "KHR", Laos: "LAK"
    };

    var currencySymbols = {
        USD: "$",
        INR: "\u20b9"
    };

    var dailyTableState = {
        rows: [],
        rawRows: [],
        columns: [],
        sortKey: "SortValue",
        ascending: false
    };

    var comparisonTableState = {
        rows: [],
        columns: [],
        sortKey: "TotalRevenue",
        ascending: false,
        showCountry: false
    };

    function getMetricDefinition(columnKey) {
        for (var index = 0; index < metricColumns.length; index++) {
            if (metricColumns[index].key === columnKey) {
                return metricColumns[index];
            }
        }

        return null;
    }

    function formatTooltipMetric(columnKey, value) {
        var definition = getMetricDefinition(columnKey);
        return formatMetric(value, definition ? definition.format : "number");
    }

    function getSelectedOperator() {
        return byId("operatorName").value || "";
    }

    function getSelectedCountry() {
        return byId("countryName").value || "";
    }

    function getSelectedService() {
        return byId("serviceName").value || "";
    }

    function hasSelectedService() {
        return !!getSelectedService();
    }

    function hasSelectedOperatorOnly() {
        return !!getSelectedOperator() && !getSelectedService();
    }

    function getCurrencySymbol(currencyCode) {
        return currencySymbols[currencyCode] || "";
    }

    function isRevenueColumn(columnKey) {
        return revenueColumnKeys.indexOf(columnKey) >= 0;
    }

    function getCurrencyForCountry(country) {
        if (!country) {
            return "USD";
        }

        if (countryCurrency[country]) {
            return countryCurrency[country];
        }

        var normalized = normalizeText(country).toLowerCase();
        var keys = Object.keys(countryCurrency);
        for (var index = 0; index < keys.length; index++) {
            if (keys[index].toLowerCase() === normalized) {
                return countryCurrency[keys[index]];
            }
        }

        for (var matchIndex = 0; matchIndex < keys.length; matchIndex++) {
            var key = keys[matchIndex].toLowerCase();
            if (normalized.indexOf(key) >= 0 || key.indexOf(normalized) >= 0) {
                return countryCurrency[keys[matchIndex]];
            }
        }

        return "USD";
    }

    function getStorage() {
        try {
            return window.localStorage;
        } catch (error) {
            try {
                return window.sessionStorage;
            } catch (fallbackError) {
                return null;
            }
        }
    }

    function getRatesCacheKey() {
        return "vas_fx_rates_" + new Date().toISOString().split("T")[0];
    }

    function clearOldRateCache(store, activeKey) {
        if (!store) {
            return;
        }

        var keysToDelete = [];
        for (var index = 0; index < store.length; index++) {
            var key = store.key(index);
            if (key && key.indexOf("vas_fx_rates_") === 0 && key !== activeKey) {
                keysToDelete.push(key);
            }
        }

        keysToDelete.forEach(function (key) {
            store.removeItem(key);
        });
    }

    function fetchExchangeRates() {
        if (state.ratesLoaded) {
            return Promise.resolve(true);
        }

        if (state.ratesPromise) {
            return state.ratesPromise;
        }

        var store = getStorage();
        var cacheKey = getRatesCacheKey();

        if (store) {
            try {
                var cached = store.getItem(cacheKey);
                if (cached) {
                    var parsed = JSON.parse(cached);
                    if (parsed && parsed.rates && parsed.rates.USD && parsed.rates.INR) {
                        state.exchangeRates = parsed.rates;
                        state.ratesLoaded = true;
                        return Promise.resolve(true);
                    }
                }
            } catch (error) {
            }
        }

        state.ratesPromise = Promise.all([
            fetch("https://open.er-api.com/v6/latest/USD", { cache: "no-store" }),
            fetch("https://open.er-api.com/v6/latest/INR", { cache: "no-store" })
        ]).then(function (responses) {
            if (!responses[0].ok || !responses[1].ok) {
                throw new Error("Rate API returned an error.");
            }

            return Promise.all([responses[0].json(), responses[1].json()]);
        }).then(function (payloads) {
            var usdData = payloads[0];
            var inrData = payloads[1];

            if (usdData.result !== "success" || inrData.result !== "success") {
                throw new Error("Rate API returned an invalid payload.");
            }

            state.exchangeRates = {
                USD: usdData.rates,
                INR: inrData.rates
            };
            state.ratesLoaded = true;

            if (store) {
                try {
                    store.setItem(cacheKey, JSON.stringify({
                        rates: state.exchangeRates,
                        date: new Date().toISOString().split("T")[0]
                    }));
                    clearOldRateCache(store, cacheKey);
                } catch (error) {
                }
            }

            return true;
        }).catch(function () {
            state.ratesLoaded = false;
            return false;
        }).then(function (success) {
            state.ratesPromise = null;
            return success;
        });

        return state.ratesPromise;
    }

    function convertRevenueValue(localAmount, countryCode, targetCurrency) {
        if (!localAmount || targetCurrency === "local" || !state.ratesLoaded) {
            return localAmount;
        }

        var rates = state.exchangeRates[targetCurrency];
        if (!rates) {
            return localAmount;
        }

        var rate = rates[countryCode];
        if (!rate) {
            return localAmount;
        }

        return localAmount / rate;
    }

    function getRenderableRows(rows) {
        var sourceRows = rows || state.rawRows;

        // When "All operators" is selected but we know the populated operators for this
        // country, restrict to only those operators so DB-only (non-live) operators are excluded.
        var selectedOperator = byId("operatorName").value || "";
        if (!selectedOperator && state.availableOperators.length > 0) {
            sourceRows = sourceRows.filter(function (row) {
                return state.availableOperators.indexOf((row.OperatorName || "").toLowerCase()) >= 0;
            });
        }

        if (state.activeCurrency === "local" || !state.ratesLoaded) {
            return sourceRows.slice();
        }

        return sourceRows.map(function (row) {
            var converted = {};
            Object.keys(row).forEach(function (key) {
                converted[key] = row[key];
            });

            var countryCode = getCurrencyForCountry(row.Country);
            revenueColumnKeys.forEach(function (columnKey) {
                converted[columnKey] = convertRevenueValue(Number(row[columnKey]) || 0, countryCode, state.activeCurrency);
            });

            return converted;
        });
    }

    function setCurrencyTabState(currencyCode) {
        document.querySelectorAll("#currencyTabs .cur-tab").forEach(function (tab) {
            tab.classList.toggle("on", tab.getAttribute("data-currency") === currencyCode);
        });
    }

    function hideRateInfo() {
        var rateInfo = byId("rateInfo");
        if (rateInfo) {
            rateInfo.classList.add("hidden");
        }
    }

    function updateRateInfoBar() {
        var rateInfo = byId("rateInfo");
        if (!rateInfo) {
            return;
        }

        if (state.activeCurrency === "local") {
            hideRateInfo();
            return;
        }

        var rates = state.exchangeRates[state.activeCurrency];
        if (!rates) {
            hideRateInfo();
            return;
        }

        var countries = state.rawRows.map(function (row) {
            return normalizeText(row.Country);
        }).filter(function (country) {
            return !!country;
        }).filter(function (country, index, values) {
            return values.indexOf(country) === index;
        }).sort();

        var chips = countries.map(function (country) {
            var currencyCode = getCurrencyForCountry(country);
            var rate = rates[currencyCode];
            if (!rate) {
                return "";
            }

            return "<span class=\"rate-chip\"><strong>" + country + "</strong> | 1 " + state.activeCurrency +
                " = " + rate.toLocaleString(undefined, { maximumFractionDigits: 2 }) + " " + currencyCode + "</span>";
        }).filter(function (chip) {
            return !!chip;
        });

        if (!chips.length) {
            hideRateInfo();
            return;
        }

        byId("rateInfoHeader").innerHTML = "Revenue converted to " + state.activeCurrency + " (" + getCurrencySymbol(state.activeCurrency) + ") | Exchange rates for " +
            new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
        byId("rateInfoBody").innerHTML = chips.join("");
        rateInfo.classList.remove("hidden");
    }

    function resetCurrencyUi(showTabs) {
        state.activeCurrency = "local";
        setCurrencyTabState("local");
        hideRateInfo();
        byId("currencyTabs").classList.toggle("hidden", !showTabs);
    }

    function changeCurrency(currencyCode) {
        if (!state.rawRows.length) {
            resetCurrencyUi(false);
            return;
        }

        if (currencyCode === "local") {
            state.activeCurrency = "local";
            setCurrencyTabState("local");
            renderDashboard();
            return;
        }

        setLoading(true);
        fetchExchangeRates().then(function (success) {
            if (!success) {
                state.activeCurrency = "local";
                setCurrencyTabState("local");
                hideRateInfo();
                setError("Live exchange rates could not be loaded right now. Local currency view is still available.");
                return;
            }

            state.activeCurrency = currencyCode;
            setCurrencyTabState(currencyCode);
            setError("");
            renderDashboard();
        }).finally(function () {
            setLoading(false);
        });
    }

    function destroyCharts() {
        Object.keys(state.charts).forEach(function (key) {
            if (state.charts[key]) {
                state.charts[key].destroy();
            }
        });
        state.charts = {};
    }

    function parseReportDate(value) {
        if (!value) {
            return null;
        }

        if (Object.prototype.toString.call(value) === "[object Date]") {
            return isNaN(value.getTime()) ? null : value;
        }

        if (typeof value === "string") {
            var match = /\/Date\((\d+)(?:[-+]\d+)?\)\//.exec(value);
            if (match) {
                return new Date(parseInt(match[1], 10));
            }

            if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
                return new Date(value + "T00:00:00");
            }

            var parsed = new Date(value);
            return isNaN(parsed.getTime()) ? null : parsed;
        }

        if (typeof value === "number") {
            return new Date(value);
        }

        return null;
    }

    function pad(value) {
        return value < 10 ? "0" + value : String(value);
    }

    function toDateKey(date) {
        return date
            ? date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate())
            : "";
    }

    function startOfWeek(date) {
        var copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        var day = copy.getDay();
        var diff = day === 0 ? -6 : 1 - day;
        copy.setDate(copy.getDate() + diff);
        copy.setHours(0, 0, 0, 0);
        return copy;
    }

    function getWeekLabel(date) {
        var start = startOfWeek(date);
        var end = new Date(start.getTime());
        end.setDate(start.getDate() + 6);
        return formatDisplayDate(start, false) + " - " + formatDisplayDate(end, false);
    }

    function formatDisplayDate(date, includeYear) {
        if (!date) {
            return "";
        }

        return date.toLocaleDateString("en-GB", {
            day: "2-digit",
            month: "short",
            year: includeYear === false ? undefined : "numeric"
        });
    }

    function formatDayMonth(date) {
        if (!date) {
            return "";
        }

        return date.toLocaleDateString("en-GB", {
            day: "numeric",
            month: "short"
        });
    }

    function formatNumber(value) {
        return (Number(value) || 0).toLocaleString();
    }

    function formatCompactNumber(value) {
        var number = Number(value) || 0;
        var absolute = Math.abs(number);

        if (absolute >= 1000000000) {
            return formatCompactUnit(number / 1000000000, "B");
        }

        if (absolute >= 1000000) {
            return formatCompactUnit(number / 1000000, "M");
        }

        if (absolute >= 1000) {
            return formatCompactUnit(number / 1000, "K");
        }

        return number.toLocaleString();
    }

    function formatCompactUnit(value, suffix) {
        var absolute = Math.abs(value);
        var digits = absolute >= 100 ? 0 : absolute >= 10 ? 1 : 2;
        var text = value.toFixed(digits)
            .replace(/(\.\d*?[1-9])0+$/, "$1")
            .replace(/\.0+$/, "");
        return text + suffix;
    }

    function formatCurrency(value) {
        var formatted = (Number(value) || 0).toLocaleString(undefined, {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
        var symbol = state.activeCurrency === "local" ? "" : getCurrencySymbol(state.activeCurrency);
        return symbol ? symbol + formatted : formatted;
    }

    function formatPercent(value) {
        return (Number(value) || 0).toFixed(1) + "%";
    }

    function formatMetric(value, format) {
        if (format === "text") {
            return normalizeText(value);
        }

        if (format === "currency") {
            return formatCurrency(value);
        }

        if (format === "percent") {
            return formatPercent(value);
        }

        return formatNumber(value);
    }

    function formatAxisTick(value, axisFormat) {
        if (axisFormat === "percent") {
            return value + "%";
        }

        return formatCompactNumber(value);
    }

    function normalizeText(value) {
        return value == null ? "" : String(value);
    }

    function normalizeRow(row) {
        var reportDate = parseReportDate(row.ReportDateText || row.ReportDate || row.Date || row.date);
        var totalVisitors    = Number(row.TotalVisitors) || 0;
        var activationCount  = Number(row.ActivationCount) || 0;
        var freeTrials       = Number(row.FreeTrials) || 0;
        var renewalRevenue   = Number(row.RenewalRevenue) || 0;
        var totalRevenue     = Number(row.TotalRevenue) || 0;
        var churn            = Number(row.Churn) || 0;
        var activeBase       = Number(row.ActiveBase) || 0;
        return {
            ReportDate: reportDate,
            DateKey: toDateKey(reportDate),
            RegionId: Number(row.RegionId) || 0,
            RegionName: normalizeText(row.RegionName),
            OperatorName: normalizeText(row.OperatorName),
            ServiceName: normalizeText(row.ServiceName),
            Country: normalizeText(row.Country),
            ActivationSource: normalizeText(row.ActivationSource || row.Source || row.source),
            ActivationCategory: normalizeText(row.ActivationCategory || row.ActivationCtg || row.CTG || row.ctg || row.Category || row.category),
            TotalVisitors: totalVisitors,
            UniqueVisitors: Number(row.UniqueVisitors) || 0,
            ActivationAttempts: Number(row.ActivationAttempts) || 0,
            FreeTrials: freeTrials,
            ActivationCount: activationCount,
            TotalActivations: freeTrials + activationCount,
            ActivationRevenue: Number(row.ActivationRevenue) || 0,
            RenewalCount: Number(row.RenewalCount) || 0,
            RenewalRevenue: renewalRevenue,
            TotalRevenue: totalRevenue,
            Churn: churn,
            UserChurn: Number(row.UserChurn || row.userChurn || row.USER_INIT || row.user_init || row.UserInit) || 0,
            SystemChurn: Number(row.SystemChurn || row.systemChurn || row.CP_INIT || row.cp_init || row.CpInit) || 0,
            Deactivation: Number(row.Deactivation || row.deactivation || row.PROVISION || row.provision) || 0,
            GrossBase: Number(row.GrossBase) || 0,
            ActiveBase: activeBase,
            ActivationRate: totalVisitors > 0 ? (activationCount / totalVisitors) * 100 : 0,
            RenewalShare:   totalRevenue > 0  ? (renewalRevenue / totalRevenue) * 100    : 0,
            ChurnRate:      activeBase > 0    ? (churn / activeBase) * 100               : 0
        };
    }

    function safeFetchJson(url) {
        return fetch(url, {
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json"
            }
        }).then(function (response) {
            if (response.status === 401 || response.status === 403) {
                // Session timeout or unauthorized: redirect to login.
                window.location.href = "/Account/Login";
                return Promise.reject(new Error("Unauthorized: session expired."));
            }

            if (!response.ok) {
                var contentType = response.headers.get("Content-Type") || "";

                if (contentType.indexOf("application/json") >= 0) {
                    return response.json().then(function (payload) {
                        var message = payload && payload.message ? payload.message : "Request failed.";
                        if (payload && payload.detail) {
                            message += " " + payload.detail;
                        }

                        throw new Error(message);
                    });
                }

                // If we got HTML (e.g. an unexpected login page), give a clear error instead of JSON parse failure.
                return response.text().then(function (text) {
                    var errorMessage = "Request failed with status " + response.status + ".";
                    if (text && text.trim().startsWith("<")) {
                        errorMessage += " Server returned HTML (possibly session timeout redirect).";
                    }
                    throw new Error(errorMessage);
                });
            }

            var contentType = response.headers.get("Content-Type") || "";
            if (contentType.indexOf("text/html") >= 0) {
                return response.text().then(function (text) {
                    if (text && text.trim().startsWith("<")) {
                        window.location.href = "/Account/Login";
                        throw new Error("Unauthorized: session expired.");
                    }

                    throw new Error("Request returned HTML instead of JSON.");
                });
            }

            return response.json();
        });
    }

    function parseInputDate(value) {
        if (!value) {
            return null;
        }

        if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
            var parts = value.split("-");
            return new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
        }

        return parseReportDate(value);
    }

    function addDays(date, days) {
        var next = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        next.setDate(next.getDate() + days);
        return next;
    }

    function formatRangeLabel(fromDate, toDate) {
        if (!fromDate || !toDate) {
            return "Range unavailable";
        }

        return formatDisplayDate(fromDate, true) + " -> " + formatDisplayDate(toDate, true);
    }

    function getSelectedDateRange() {
        var fromDate = parseInputDate(byId("fromDate").value);
        var toDate = parseInputDate(byId("toDate").value);
        if (!fromDate || !toDate || toDate < fromDate) {
            return null;
        }

        return {
            fromDate: fromDate,
            toDate: toDate
        };
    }

    function getPreviousDateRange() {
        var range = getSelectedDateRange();
        if (!range) {
            return null;
        }

        var span = Math.round((range.toDate.getTime() - range.fromDate.getTime()) / 86400000) + 1;
        var previousToDate = addDays(range.fromDate, -1);
        return {
            fromDate: addDays(previousToDate, -(span - 1)),
            toDate: previousToDate
        };
    }

    function buildReportingParams(dateRange) {
        var range = dateRange || getSelectedDateRange() || {};
        return [
            "regionId=" + encodeURIComponent(state.selectedRegionId),
            "country=" + encodeURIComponent(getSelectedCountry()),
            "operatorName=" + encodeURIComponent(byId("operatorName").value || ""),
            "serviceName=" + encodeURIComponent(byId("serviceName").value || ""),
            "fromDate=" + encodeURIComponent(range.fromDate ? toDateKey(range.fromDate) : (byId("fromDate").value || "")),
            "toDate=" + encodeURIComponent(range.toDate ? toDateKey(range.toDate) : (byId("toDate").value || "")),
            "viewMode=" + encodeURIComponent(state.viewMode)
        ].join("&");
    }

    function buildUrl(baseUrl, query) {
        return query ? baseUrl + "?" + query : baseUrl;
    }

    function fetchReportRows(dateRange) {
        return safeFetchJson(buildUrl(state.api.reporting, buildReportingParams(dateRange)))
            .then(function (payload) {
                return (payload.Rows || payload.rows || []).map(normalizeRow).filter(function (row) {
                    return row.ReportDate;
                });
            });
    }

    function getPaletteColor(index) {
        var accent = accentCycle[index % accentCycle.length];
        return chartColors[accent] || chartColors.blue;
    }

    function setLoading(isLoading) {
        byId("dashboardLoader").classList.toggle("hidden", !isLoading);
    }

    function setError(message) {
        var errorBox = byId("dashboardError");
        if (!message) {
            errorBox.textContent = "";
            errorBox.classList.add("hidden");
            return;
        }

        errorBox.textContent = message;
        errorBox.classList.remove("hidden");
    }

    function showEmpty(message) {
        byId("dashboardContent").classList.add("hidden");
        byId("dashboardEmpty").classList.remove("hidden");
        if (message) {
            byId("dashboardEmpty").querySelector("p").textContent = message;
        }
    }

    function hideEmpty() {
        byId("dashboardEmpty").classList.add("hidden");
        byId("dashboardContent").classList.remove("hidden");
    }

    function setActiveRegion(regionId) {
        state.selectedRegionId = String(regionId || "");
        document.querySelectorAll(".region-chip").forEach(function (chip) {
            chip.classList.toggle("active", chip.getAttribute("data-region-id") === state.selectedRegionId);
        });
    }

    function getActiveRegionText() {
        var activeChip = document.querySelector(".region-chip.active");
        return activeChip ? activeChip.textContent.trim() : "Selected Region";
    }

    function setSelectOptions(selectId, placeholder, values) {
        var select = byId(selectId);
        select.innerHTML = "";

        var defaultOption = document.createElement("option");
        defaultOption.value = "";
        defaultOption.textContent = placeholder;
        select.appendChild(defaultOption);

        (values || []).forEach(function (value) {
            var option = document.createElement("option");
            option.value = value;
            option.textContent = value;
            select.appendChild(option);
        });
    }

    function clearSelect(selectId, placeholder) {
        setSelectOptions(selectId, placeholder, []);
    }

    function loadCountries() {
        if (!state.selectedRegionId) {
            clearSelect("countryName", "All countries");
            clearSelect("operatorName", "All operators");
            clearSelect("serviceName", "All services");
            return Promise.resolve();
        }

        clearSelect("countryName", "Loading countries...");
        clearSelect("operatorName", "All operators");
        clearSelect("serviceName", "All services");

        return safeFetchJson(buildUrl(state.api.countries, "regionId=" + encodeURIComponent(state.selectedRegionId)))
            .then(function (countries) {
                setSelectOptions("countryName", "All countries", countries || []);
            });
    }

    function loadOperators() {
        if (!state.selectedRegionId) {
            clearSelect("countryName", "All countries");
            clearSelect("operatorName", "All operators");
            clearSelect("serviceName", "All services");
            state.availableOperators = [];
            return Promise.resolve();
        }

        var country = getSelectedCountry();
        if (!country) {
            clearSelect("operatorName", "All operators");
            clearSelect("serviceName", "All services");
            state.availableOperators = [];
            return Promise.resolve();
        }

        clearSelect("operatorName", "Loading operators...");
        clearSelect("serviceName", "All services");
        state.availableOperators = [];

        return safeFetchJson(
            buildUrl(
                state.api.operators,
                "regionId=" + encodeURIComponent(state.selectedRegionId) +
                "&country=" + encodeURIComponent(country)
            )
        )
            .then(function (operators) {
                state.availableOperators = (operators || []).map(function (op) { return String(op).toLowerCase(); });
                setSelectOptions("operatorName", "All operators", operators || []);
                clearSelect("serviceName", "All services");
            });
    }

    function loadServices() {
        if (!state.selectedRegionId) {
            clearSelect("serviceName", "All services");
            return Promise.resolve();
        }

        var country = getSelectedCountry();
        var operatorName = byId("operatorName").value || "";
        if (!operatorName) {
            clearSelect("serviceName", "All services");
            return Promise.resolve();
        }

        clearSelect("serviceName", "Loading services...");

        return safeFetchJson(
            buildUrl(
                state.api.services,
                "regionId=" + encodeURIComponent(state.selectedRegionId) +
                "&country=" + encodeURIComponent(country) +
                "&operatorName=" + encodeURIComponent(operatorName)
            )
        ).then(function (services) {
            setSelectOptions("serviceName", "All services", services || []);
        });
    }

    function loadRegionFilters() {
        return loadCountries()
            .then(function () {
                clearSelect("operatorName", "All operators");
                clearSelect("serviceName", "All services");
                setError("");
            })
            .catch(function (error) {
                setError((error && error.message) || "Unable to load countries.");
            });
    }

    function toggleSidebar(forceOpen) {
        var sidebar = byId("dashboardSidebar");
        var overlay = byId("sidebarOverlay");
        var isOpen = typeof forceOpen === "boolean" ? forceOpen : !sidebar.classList.contains("open");
        sidebar.classList.toggle("open", isOpen);
        overlay.classList.toggle("open", isOpen);
    }

    function closeSidebarOnMobile() {
        if (window.innerWidth <= 960) {
            toggleSidebar(false);
        }
    }

    function setDefaultDates() {
        var today = new Date();
        var toDate = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
        var fromDate = new Date(today.getFullYear(), today.getMonth(), 1);
        byId("fromDate").value = toDateKey(fromDate);
        byId("toDate").value = toDateKey(toDate);
    }

    function buildMetaText() {
        var country = getSelectedCountry() || "All Countries";
        var operator = byId("operatorName").value || "All Operators";
        var service = byId("serviceName").value || "All Services";
        var fromDate = byId("fromDate").value || "Start";
        var toDate = byId("toDate").value || "End";
        var currencyLabel = state.activeCurrency === "local" ? "Local currency" : state.activeCurrency + " conversion";
        return getActiveRegionText() + " | " + country + " | " + operator + " | " + service + " | " + fromDate + " -> " + toDate + " | " + currencyLabel + " | " + state.viewMode + " view";
    }

    function updateHeader() {
        var headline = byId("serviceName").value || byId("operatorName").value || getSelectedCountry() || getActiveRegionText();
        byId("dashTitle").textContent = headline + " - VAS Daily Report";
        byId("dashMeta").textContent = buildMetaText();
    }

    function getPeriodBucket(date) {
        if (!date) {
            return null;
        }

        if (state.viewMode === "weekly") {
            var weekStart = startOfWeek(date);
            return {
                key: toDateKey(weekStart),
                label: getWeekLabel(date),
                shortLabel: "Week of " + formatDisplayDate(weekStart, false),
                sortValue: weekStart.getTime()
            };
        }

        return {
            key: toDateKey(date),
            label: formatDisplayDate(date, true),
            shortLabel: formatDisplayDate(date, false),
            sortValue: date.getTime()
        };
    }

    function createAggregateRow(label, shortLabel, sortValue) {
        return {
            DisplayLabel: label,
            ShortLabel: shortLabel,
            SortValue: sortValue,
            TotalVisitors: 0,
            UniqueVisitors: 0,
            ActivationAttempts: 0,
            FreeTrials: 0,
            ActivationCount: 0,
            TotalActivations: 0,
            ActivationRevenue: 0,
            RenewalCount: 0,
            RenewalRevenue: 0,
            TotalRevenue: 0,
            Churn: 0,
            UserChurn: 0,
            SystemChurn: 0,
            Deactivation: 0,
            GrossBase: 0,
            ActiveBase: 0,
            ActivationRate: 0,
            RenewalShare: 0,
            ChurnRate: 0,
            RevenuePerActiveBase: 0,
            ActivationUnitRevenue: 0,
            RenewalUnitRevenue: 0,
            RevenueShare: 0,
            ActivationShare: 0,
            RenewalCountShare: 0,
            ChurnShare: 0,
            OpeningGrossBase: 0,
            OpeningActiveBase: 0,
            ClosingGrossBase: 0,
            ClosingActiveBase: 0,
            _earliestSnapshotTime: null,
            _latestSnapshotTime: null,
            _openingGrossBase: 0,
            _openingActiveBase: 0,
            _closingGrossBase: 0,
            _closingActiveBase: 0
        };
    }

    function updateSnapshotMetrics(target, row) {
        if (!row || !row.ReportDate) {
            return;
        }

        var timestamp = row.ReportDate.getTime();
        if (target._earliestSnapshotTime === null || timestamp < target._earliestSnapshotTime) {
            target._earliestSnapshotTime = timestamp;
            target._openingGrossBase = row.GrossBase;
            target._openingActiveBase = row.ActiveBase;
        } else if (timestamp === target._earliestSnapshotTime) {
            target._openingGrossBase += row.GrossBase;
            target._openingActiveBase += row.ActiveBase;
        }

        if (target._latestSnapshotTime === null || timestamp > target._latestSnapshotTime) {
            target._latestSnapshotTime = timestamp;
            target._closingGrossBase = row.GrossBase;
            target._closingActiveBase = row.ActiveBase;
        } else if (timestamp === target._latestSnapshotTime) {
            target._closingGrossBase += row.GrossBase;
            target._closingActiveBase += row.ActiveBase;
        }

        target.OpeningGrossBase = target._openingGrossBase;
        target.OpeningActiveBase = target._openingActiveBase;
        target.ClosingGrossBase = target._closingGrossBase;
        target.ClosingActiveBase = target._closingActiveBase;
        target.GrossBase = target.ClosingGrossBase;
        target.ActiveBase = target.ClosingActiveBase;
    }

    function accumulateMetrics(target, row) {
        target.TotalVisitors += row.TotalVisitors;
        target.UniqueVisitors += row.UniqueVisitors;
        target.ActivationAttempts += row.ActivationAttempts;
        target.FreeTrials += row.FreeTrials;
        target.ActivationCount += row.ActivationCount;
        target.TotalActivations += row.FreeTrials + row.ActivationCount;
        target.ActivationRevenue += row.ActivationRevenue;
        target.RenewalCount += row.RenewalCount;
        target.RenewalRevenue += row.RenewalRevenue;
        target.TotalRevenue += row.TotalRevenue;
        target.Churn += row.Churn;
        target.UserChurn += row.UserChurn;
        target.SystemChurn += row.SystemChurn;
        target.Deactivation += row.Deactivation;
        updateSnapshotMetrics(target, row);
    }

    function applyDerivedMetrics(target) {
        target.ActivationRate = target.TotalVisitors > 0 ? (target.ActivationCount / target.TotalVisitors) * 100 : 0;
        target.RenewalShare = target.TotalRevenue > 0 ? (target.RenewalRevenue / target.TotalRevenue) * 100 : 0;
        target.ChurnRate = target.ActiveBase > 0 ? (target.Churn / target.ActiveBase) * 100 : 0;
        target.RevenuePerActiveBase = target.ActiveBase > 0 ? target.TotalRevenue / target.ActiveBase : 0;
        target.ActivationUnitRevenue = target.ActivationCount > 0 ? target.ActivationRevenue / target.ActivationCount : 0;
        target.RenewalUnitRevenue = target.RenewalCount > 0 ? target.RenewalRevenue / target.RenewalCount : 0;
    }

    function buildPeriodRows(rows) {
        var grouped = {};
        var sortedRows = rows.slice().sort(function (left, right) {
            return left.ReportDate - right.ReportDate;
        });

        sortedRows.forEach(function (row) {
            if (!row.ReportDate) {
                return;
            }

            var bucket = getPeriodBucket(row.ReportDate);
            if (!bucket) {
                return;
            }

            if (!grouped[bucket.key]) {
                grouped[bucket.key] = createAggregateRow(bucket.label, bucket.shortLabel, bucket.sortValue);
            }

            accumulateMetrics(grouped[bucket.key], row);
        });

        return Object.keys(grouped)
            .map(function (key) {
                var entry = grouped[key];
                applyDerivedMetrics(entry);
                return entry;
            })
            .sort(function (left, right) {
                return left.SortValue - right.SortValue;
            });
    }

    function buildDimensionRows(rows, dimensionKey, emptyLabel, sortMetricKey) {
        var grouped = {};

        rows.forEach(function (row) {
            var label = normalizeText(row[dimensionKey]) || emptyLabel;
            if (!grouped[label]) {
                grouped[label] = createAggregateRow(label, label, 0);
            }

            accumulateMetrics(grouped[label], row);
        });

        var sortKey = sortMetricKey || "TotalRevenue";
        return Object.keys(grouped)
            .map(function (key) {
                var entry = grouped[key];
                applyDerivedMetrics(entry);
                return entry;
            })
            .sort(function (left, right) {
                return (Number(right[sortKey]) || 0) - (Number(left[sortKey]) || 0);
            });
    }

    function summarizeRows(rows) {
        var summary = createAggregateRow("", "", 0);
        rows.forEach(function (row) {
            accumulateMetrics(summary, row);
        });
        applyDerivedMetrics(summary);
        return summary;
    }

    function buildWeekdayRows(rows) {
        var weekdays = [
            { key: 0, label: "Mon" },
            { key: 1, label: "Tue" },
            { key: 2, label: "Wed" },
            { key: 3, label: "Thu" },
            { key: 4, label: "Fri" },
            { key: 5, label: "Sat" },
            { key: 6, label: "Sun" }
        ];
        var grouped = {};

        weekdays.forEach(function (weekday) {
            grouped[weekday.key] = createAggregateRow(weekday.label, weekday.label, weekday.key);
        });

        rows.forEach(function (row) {
            if (!row.ReportDate) {
                return;
            }

            var dayIndex = row.ReportDate.getDay() === 0 ? 6 : row.ReportDate.getDay() - 1;
            accumulateMetrics(grouped[dayIndex], row);
        });

        return weekdays.map(function (weekday) {
            var entry = grouped[weekday.key];
            applyDerivedMetrics(entry);
            return entry;
        });
    }

    function buildContributionRows(rows, dimensionKey, emptyLabel) {
        var dimensionRows = buildDimensionRows(rows, dimensionKey, emptyLabel, "TotalRevenue");
        if (dimensionRows.length <= 1) {
            return [];
        }

        var totals = summarizeRows(rows);
        dimensionRows.forEach(function (row) {
            row.RevenueShare = totals.TotalRevenue > 0 ? (row.TotalRevenue / totals.TotalRevenue) * 100 : 0;
            row.ActivationShare = totals.ActivationCount > 0 ? (row.ActivationCount / totals.ActivationCount) * 100 : 0;
            row.RenewalCountShare = totals.RenewalCount > 0 ? (row.RenewalCount / totals.RenewalCount) * 100 : 0;
            row.ChurnShare = totals.Churn > 0 ? (row.Churn / totals.Churn) * 100 : 0;
        });

        return dimensionRows;
    }

    function buildContributionDatasets(rows, metrics) {
        var topRows = rows.slice(0, 5);
        if (!topRows.length) {
            return [];
        }

        var datasets = topRows.map(function (row, index) {
            return {
                label: row.ShortLabel,
                data: metrics.map(function (metric) {
                    return Number(row[metric.key]) || 0;
                }),
                backgroundColor: getPaletteColor(index).stroke,
                borderColor: getPaletteColor(index).stroke,
                borderWidth: 0
            };
        });

        var otherData = metrics.map(function (metric) {
            var used = topRows.reduce(function (sum, row) {
                return sum + (Number(row[metric.key]) || 0);
            }, 0);
            return Math.max(0, 100 - used);
        });

        if (otherData.some(function (value) { return value > 0.05; })) {
            datasets.push({
                label: "Other",
                data: otherData,
                backgroundColor: "rgba(148,163,184,.8)",
                borderColor: "rgba(148,163,184,.8)",
                borderWidth: 0
            });
        }

        return datasets;
    }

    function buildContributionDoughnutData(rows, metricColumnKey) {
        var topRows = rows.slice(0, 5);
        var labels = topRows.map(function (row) { return row.ShortLabel; });
        var data = topRows.map(function (row) { return Number(row[metricColumnKey]) || 0; });
        var colors = topRows.map(function (row, index) {
            return getPaletteColor(index).stroke;
        });
        var usedValue = data.reduce(function (sum, value) {
            return sum + value;
        }, 0);
        var otherValue = Math.max(0, 100 - usedValue);

        if (otherValue > 0.05) {
            labels.push("Other");
            data.push(otherValue);
            colors.push("rgba(148,163,184,.82)");
        }

        return {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: colors,
                borderWidth: 0
            }]
        };
    }

    function getCompactSliceLabel(label, maxLength) {
        var cleanLabel = normalizeText(label).trim();
        var limit = maxLength || 12;
        if (!cleanLabel) {
            return "";
        }

        if (cleanLabel.length <= limit) {
            return cleanLabel;
        }

        var words = cleanLabel.split(/\s+/);
        if (words.length > 1) {
            var firstTwo = words[0] + " " + words[1];
            if (firstTwo.length <= limit) {
                return firstTwo;
            }

            if (words[0].length <= limit) {
                return words[0];
            }
        }

        return cleanLabel.substring(0, Math.max(1, limit - 1)) + "...";
    }

    function buildContributionCharts(rows) {
        var dimensions = [
            { key: "Country", emptyLabel: "Unspecified Country", title: "Country" },
            { key: "OperatorName", emptyLabel: "Unspecified Operator", title: "Operator" },
            { key: "ServiceName", emptyLabel: "Unspecified Service", title: "Service" }
        ];
        var rankingMetrics = [
            { title: "Revenue Share", columnKey: "RevenueShare", accent: "blue" },
            { title: "Activation Share", columnKey: "ActivationShare", accent: "emerald" },
            { title: "Renewal Share", columnKey: "RenewalCountShare", accent: "amber" },
            { title: "Churn Share", columnKey: "ChurnShare", accent: "rose" }
        ];
        var charts = [];

        dimensions.forEach(function (dimension, index) {
            var contributionRows = buildContributionRows(rows, dimension.key, dimension.emptyLabel);
            if (contributionRows.length <= 1) {
                return;
            }

            var groupedCharts = rankingMetrics.map(function (metric) {
                var rankedRows = contributionRows.slice().sort(function (left, right) {
                    return (Number(right[metric.columnKey]) || 0) - (Number(left[metric.columnKey]) || 0);
                });

                if (rankedRows.length <= 1) {
                    return null;
                }

                return {
                    id: dimension.title.toLowerCase() + metric.columnKey + "Rank",
                    title: metric.title,
                    type: "doughnut",
                    aspectRatio: 1,
                    heightPx: 190,
                    customData: buildContributionDoughnutData(rankedRows, metric.columnKey),
                    showLegend: false,
                    renderCustomLegend: true,
                    valueFormat: "percent",
                    sliceLabelOptions: {
                        enabled: true,
                        minPercent: 14,
                        maxChars: 12,
                        showOtherLabel: false
                    }
                };
            }).filter(function (groupedChart) {
                return !!groupedChart;
            });

            if (!groupedCharts.length) {
                return;
            }

            charts.push({
                id: dimension.title.toLowerCase() + "ContributionRank",
                title: dimension.title + " Contribution Ranking",
                subtitle: "Revenue, activation, renewal, and churn share grouped together in doughnut views by " + dimension.title.toLowerCase(),
                badge: "Contribution",
                badgeAccent: "blue",
                tab: "contributions",
                row: 6 + index,
                cardClass: "ranking-card",
                groupedLayout: "grid",
                groupedCharts: groupedCharts
            });
        });

        return charts;
    }

    function buildDimensionTrendChart(rows, dimensionKey, emptyLabel, label, idPrefix, rowNumber) {
        var rankedRows = buildDimensionRows(rows, dimensionKey, emptyLabel, "ActivationCount").slice(0, 5);
        if (!rankedRows.length) {
            return [];
        }

        var selectedLabels = rankedRows.map(function (row) {
            return row.DisplayLabel;
        });
        var grouped = {};

        rows.forEach(function (row) {
            var dimensionValue = normalizeText(row[dimensionKey]) || emptyLabel;
            if (selectedLabels.indexOf(dimensionValue) < 0) {
                return;
            }

            var bucket = getPeriodBucket(row.ReportDate);
            if (!bucket) {
                return;
            }

            if (!grouped[bucket.key]) {
                grouped[bucket.key] = {
                    DisplayLabel: bucket.label,
                    ShortLabel: bucket.shortLabel,
                    SortValue: bucket.sortValue
                };
            }

            grouped[bucket.key][dimensionValue] = (Number(grouped[bucket.key][dimensionValue]) || 0) + row.ActivationCount;
        });

        var orderedBuckets = Object.keys(grouped).map(function (key) {
            return grouped[key];
        }).sort(function (left, right) {
            return left.SortValue - right.SortValue;
        });

        if (orderedBuckets.length <= 1) {
            return [];
        }

        return [{
            id: idPrefix + "Trend",
            title: label + " Trend",
            subtitle: "Activation trend by " + label.toLowerCase() + " across the selected range",
            badge: "Trend",
            badgeAccent: "cyan",
            tab: "breakdown",
            type: "bar",
            row: rowNumber,
            aspectRatio: 2.4,
            stacked: true,
            customData: {
                labels: orderedBuckets.map(function (bucket) { return bucket.ShortLabel; }),
                datasets: selectedLabels.map(function (selectedLabel, index) {
                    var color = getPaletteColor(index);
                    return {
                        label: selectedLabel,
                        data: orderedBuckets.map(function (bucket) {
                            return Number(bucket[selectedLabel]) || 0;
                        }),
                        backgroundColor: color.stroke,
                        borderColor: color.stroke,
                        borderWidth: 0
                    };
                })
            },
            showLegend: true,
            valueFormat: "number"
        }];
    }

    function buildServiceDailyBreakdownCharts(rawRows) {
        // Rank services by activation count, take top 6
        var serviceRows = buildDimensionRows(rawRows, "ServiceName", "Unspecified Service", "ActivationCount").slice(0, 6);
        if (serviceRows.length <= 1) {
            return [];
        }

        var selectedLabels = serviceRows.map(function (row) { return row.DisplayLabel; });

        // --- Group raw rows by date bucket, using service name as direct key (same
        //     pattern as buildDimensionTrendChart so there is no key-prefix mismatch) ---
        var actGrouped = {};
        var revGrouped = {};

        rawRows.forEach(function (row) {
            var svc = normalizeText(row.ServiceName) || "Unspecified Service";
            if (selectedLabels.indexOf(svc) < 0) {
                return;
            }

            var bucket = getPeriodBucket(row.ReportDate);
            if (!bucket) {
                return;
            }

            if (!actGrouped[bucket.key]) {
                actGrouped[bucket.key] = { DisplayLabel: bucket.label, ShortLabel: bucket.shortLabel, SortValue: bucket.sortValue };
                revGrouped[bucket.key] = { DisplayLabel: bucket.label, ShortLabel: bucket.shortLabel, SortValue: bucket.sortValue };
            }

            actGrouped[bucket.key][svc] = (Number(actGrouped[bucket.key][svc]) || 0) + (Number(row.ActivationCount) || 0);
            revGrouped[bucket.key][svc] = (Number(revGrouped[bucket.key][svc]) || 0) + (Number(row.TotalRevenue) || 0);
        });

        var actBuckets = Object.keys(actGrouped).map(function (k) { return actGrouped[k]; }).sort(function (a, b) { return a.SortValue - b.SortValue; });
        var revBuckets = Object.keys(revGrouped).map(function (k) { return revGrouped[k]; }).sort(function (a, b) { return a.SortValue - b.SortValue; });

        if (!actBuckets.length) {
            return [];
        }

        var charts = [];

        // Only render multi-bucket trend charts when there is more than one date bucket
        if (actBuckets.length > 1) {
            charts.push({
                id: "serviceDailyActivations",
                title: "Service-wise Daily Activations",
                subtitle: "Stacked daily activation count broken down by service across the selected range",
                badge: "Services",
                badgeAccent: "emerald",
                tab: "services",
                type: "bar",
                row: 4,
                aspectRatio: 2.4,
                stacked: true,
                customData: {
                    labels: actBuckets.map(function (b) { return b.ShortLabel; }),
                    datasets: selectedLabels.map(function (svc, i) {
                        var color = getPaletteColor(i);
                        return {
                            label: svc,
                            data: actBuckets.map(function (b) { return Number(b[svc]) || 0; }),
                            backgroundColor: color.stroke,
                            borderColor: color.stroke,
                            borderWidth: 0
                        };
                    })
                },
                showLegend: true,
                valueFormat: "number"
            });

            charts.push({
                id: "serviceDailyRevenue",
                title: "Service-wise Daily Revenue",
                subtitle: "Stacked daily revenue broken down by service across the selected range",
                badge: "Revenue",
                badgeAccent: "blue",
                tab: "services",
                type: "bar",
                row: 4,
                aspectRatio: 2.4,
                stacked: true,
                customData: {
                    labels: revBuckets.map(function (b) { return b.ShortLabel; }),
                    datasets: selectedLabels.map(function (svc, i) {
                        var color = getPaletteColor(i);
                        return {
                            label: svc,
                            data: revBuckets.map(function (b) { return Number(b[svc]) || 0; }),
                            backgroundColor: color.stroke,
                            borderColor: color.stroke,
                            borderWidth: 0
                        };
                    })
                },
                showLegend: true,
                valueFormat: "currency"
            });
        } else {
            // Snapshot view (single bucket) - show a horizontal bar per service
            charts.push({
                id: "serviceDailyActivations",
                title: "Service-wise Activations",
                subtitle: "Activation count split by service for the selected day",
                badge: "Services",
                badgeAccent: "emerald",
                tab: "services",
                type: "bar",
                row: 4,
                aspectRatio: 2.4,
                indexAxis: "y",
                labels: selectedLabels,
                rows: serviceRows,
                series: [
                    { label: "Activations", columnKey: "ActivationCount", type: "bar", accent: "emerald" }
                ]
            });

            charts.push({
                id: "serviceDailyRevenue",
                title: "Service-wise Revenue",
                subtitle: "Revenue split by service for the selected day",
                badge: "Revenue",
                badgeAccent: "blue",
                tab: "services",
                type: "bar",
                row: 4,
                aspectRatio: 2.4,
                indexAxis: "y",
                labels: selectedLabels,
                rows: serviceRows,
                series: [
                    { label: "Total Revenue", columnKey: "TotalRevenue", type: "bar", accent: "blue" }
                ]
            });
        }

        return charts;
    }

    function buildServiceBreakdownCharts(rows) {
        var definitions = [];
        var dimensions = [
            { key: "ActivationSource", emptyLabel: "Unspecified Source", label: "Activation Source", idPrefix: "source", badgeAccent: "violet" },
            { key: "ActivationCategory", emptyLabel: "Unspecified CTG", label: "Activation CTG", idPrefix: "ctg", badgeAccent: "amber" }
        ];

        dimensions.forEach(function (dimension, index) {
            if (!rows.some(function (row) { return normalizeText(row[dimension.key]); })) {
                return;
            }

            var breakdownRows = buildDimensionRows(rows, dimension.key, dimension.emptyLabel, "ActivationCount").slice(0, 6);
            if (!breakdownRows.length) {
                return;
            }

            definitions.push({
                id: dimension.idPrefix + "ContributionRank",
                title: dimension.label + " Contribution Ranking",
                subtitle: "Ranked share of churn, activations, renewals, and revenue by " + dimension.label.toLowerCase(),
                badge: "Contribution",
                badgeAccent: dimension.badgeAccent,
                tab: "breakdown",
                type: "bar",
                row: 9 + index,
                aspectRatio: 2.0,
                indexAxis: "y",
                axisFormat: "percent",
                maxValue: 100,
                labels: breakdownRows.map(function (row) { return row.ShortLabel; }),
                rows: breakdownRows,
                series: [
                    { label: "Revenue Share", columnKey: "RevenueShare", type: "bar", accent: "blue" },
                    { label: "Activation Share", columnKey: "ActivationShare", type: "bar", accent: "emerald" },
                    { label: "Renewal Share", columnKey: "RenewalCountShare", type: "bar", accent: "amber" },
                    { label: "Churn Share", columnKey: "ChurnShare", type: "bar", accent: "rose" }
                ]
            });

            var doughnutData = {
                labels: breakdownRows.map(function (row) { return row.ShortLabel; }),
                datasets: [{
                    data: breakdownRows.map(function (row) { return row.ActivationCount; }),
                    backgroundColor: breakdownRows.map(function (row, rowIndex) { return getPaletteColor(rowIndex).stroke; }),
                    borderWidth: 0
                }]
            };

            definitions.push({
                id: dimension.idPrefix + "Share",
                title: dimension.label + " Share",
                subtitle: "Activation split for the selected service across the chosen date range",
                badge: "Share",
                badgeAccent: dimension.badgeAccent,
                tab: "breakdown",
                type: "doughnut",
                row: 11 + index,
                aspectRatio: 1.4,
                customData: doughnutData,
                showLegend: true,
                valueFormat: "number"
            });

            definitions = definitions.concat(
                buildDimensionTrendChart(rows, dimension.key, dimension.emptyLabel, dimension.label, dimension.idPrefix, 13 + index)
            );
        });

        return definitions.filter(function (definition) {
            return !!definition;
        });
    }

    function buildOperatorServiceCharts(rows) {
        var serviceRows = buildContributionRows(rows, "ServiceName", "Unspecified Service").slice(0, 8);
        if (serviceRows.length <= 1) {
            return [];
        }

        return [
            {
                id: "serviceActivations",
                title: "Service-wise Activations",
                subtitle: "Compare activation volume across services for the selected operator",
                badge: "Services",
                badgeAccent: "emerald",
                tab: "operator",
                type: "bar",
                row: 9,
                aspectRatio: 2.4,
                labels: serviceRows.map(function (row) { return row.ShortLabel; }),
                rows: serviceRows,
                series: [
                    { label: "Activations", columnKey: "ActivationCount", type: "bar", accent: "emerald" }
                ]
            },
            {
                id: "serviceRevenue",
                title: "Service-wise Revenue",
                subtitle: "Compare total revenue contribution across services",
                badge: "Revenue",
                badgeAccent: "blue",
                tab: "operator",
                type: "bar",
                row: 9,
                aspectRatio: 2.4,
                labels: serviceRows.map(function (row) { return row.ShortLabel; }),
                rows: serviceRows,
                series: [
                    { label: "Total Revenue", columnKey: "TotalRevenue", type: "bar", accent: "blue" }
                ]
            },
            {
                id: "serviceHealth",
                title: "Service Health Snapshot",
                subtitle: "Activation rate, churn rate, and revenue share by service",
                badge: "Health",
                badgeAccent: "rose",
                tab: "operator",
                type: "bar",
                row: 10,
                aspectRatio: 2.4,
                axisFormat: "percent",
                maxValue: 100,
                labels: serviceRows.map(function (row) { return row.ShortLabel; }),
                rows: serviceRows,
                series: [
                    { label: "Activation Rate", columnKey: "ActivationRate", type: "bar", accent: "emerald" },
                    { label: "Churn Rate", columnKey: "ChurnRate", type: "line", accent: "rose", fill: false },
                    { label: "Revenue Share", columnKey: "RevenueShare", type: "line", accent: "blue", fill: false }
                ]
            }
        ];
    }

    function computeTrend(rows, key, inverseGood) {
        if (rows.length < 4) {
            return "";
        }

        var midpoint = Math.max(1, Math.floor(rows.length / 2));
        var firstHalf = rows.slice(0, midpoint).reduce(function (sum, row) {
            return sum + (Number(row[key]) || 0);
        }, 0);
        var secondHalf = rows.slice(-midpoint).reduce(function (sum, row) {
            return sum + (Number(row[key]) || 0);
        }, 0);

        if (!firstHalf) {
            return "";
        }

        var delta = ((secondHalf - firstHalf) / firstHalf) * 100;
        if (!isFinite(delta) || Math.abs(delta) < 0.1) {
            return "";
        }

        var isPositive = delta > 0;
        var good = inverseGood ? !isPositive : isPositive;
        return "<div class=\"kpi-trend " + (good ? "t-pos" : "t-neg") + "\">" +
            (isPositive ? "+ " : "- ") + Math.abs(delta).toFixed(1) + "%</div>";
    }

    function renderKpis(rows) {
        var totals = rows.reduce(function (summary, row) {
            summary.TotalRevenue += row.TotalRevenue;
            summary.ActivationRevenue += row.ActivationRevenue;
            summary.RenewalRevenue += row.RenewalRevenue;
            summary.TotalVisitors += row.TotalVisitors;
            summary.UniqueVisitors += row.UniqueVisitors;
            summary.ActivationCount += row.ActivationCount;
            summary.RenewalCount += row.RenewalCount;
            summary.FreeTrials += row.FreeTrials;
            summary.TotalActivations += row.TotalActivations;
            summary.Churn += row.Churn;
            summary.GrossBase = row.GrossBase;
            summary.ActiveBase = row.ActiveBase;
            return summary;
        }, {
            TotalRevenue: 0,
            ActivationRevenue: 0,
            RenewalRevenue: 0,
            TotalVisitors: 0,
            UniqueVisitors: 0,
            ActivationCount: 0,
            RenewalCount: 0,
            FreeTrials: 0,
            TotalActivations: 0,
            Churn: 0,
            GrossBase: 0,
            ActiveBase: 0
        });

        var activationRate = totals.TotalVisitors > 0 ? (totals.ActivationCount / totals.TotalVisitors) * 100 : 0;

        var cards = [
            { label: "Total Revenue", value: formatCurrency(totals.TotalRevenue), accent: "blue", sub: "Activation + renewal revenue", trend: computeTrend(rows, "TotalRevenue", false) },
            { label: "Activation Revenue", value: formatCurrency(totals.ActivationRevenue), accent: "emerald", sub: "Revenue from new activations", trend: computeTrend(rows, "ActivationRevenue", false) },
            { label: "Renewal Revenue", value: formatCurrency(totals.RenewalRevenue), accent: "amber", sub: "Revenue from renewals", trend: computeTrend(rows, "RenewalRevenue", false) },
            { label: "Revenue / Active Base", value: formatCurrency(totals.ActiveBase > 0 ? (totals.TotalRevenue / totals.ActiveBase) : 0), accent: "blue", sub: "Monetization against latest active base", trend: computeTrend(rows, "RevenuePerActiveBase", false) },
            { label: "Activation Rev / Activation", value: formatCurrency(totals.ActivationCount > 0 ? (totals.ActivationRevenue / totals.ActivationCount) : 0), accent: "emerald", sub: "Average activation monetization", trend: computeTrend(rows, "ActivationUnitRevenue", false) },
            { label: "Renewal Rev / Renewal", value: formatCurrency(totals.RenewalCount > 0 ? (totals.RenewalRevenue / totals.RenewalCount) : 0), accent: "amber", sub: "Average renewal monetization", trend: computeTrend(rows, "RenewalUnitRevenue", false) },
            { label: "Visitors", value: formatNumber(totals.TotalVisitors), accent: "cyan", sub: "Traffic across selected period", trend: computeTrend(rows, "TotalVisitors", false) },
            { label: "Unique Visitors", value: formatNumber(totals.UniqueVisitors), accent: "violet", sub: "Unique audience served", trend: computeTrend(rows, "UniqueVisitors", false) },
            { label: "Free Trial Activations", value: formatNumber(totals.FreeTrials), accent: "amber", sub: "Free trial starts completed", trend: computeTrend(rows, "FreeTrials", false) },
            { label: "Paid Activations", value: formatNumber(totals.ActivationCount), accent: "teal", sub: "Paid activations completed", trend: computeTrend(rows, "ActivationCount", false) },
            { label: "Total Activations", value: formatNumber(totals.TotalActivations), accent: "emerald", sub: "Free trial + paid activations", trend: computeTrend(rows, "TotalActivations", false) },
            { label: "Renewals", value: formatNumber(totals.RenewalCount), accent: "cyan", sub: "Successful renewals", trend: computeTrend(rows, "RenewalCount", false) },
            { label: "Active Base", value: formatNumber(totals.ActiveBase), accent: "teal", sub: "Latest active subscriber base", trend: computeTrend(rows, "ActiveBase", false) },
            { label: "Activation Rate", value: formatPercent(activationRate), accent: "emerald", sub: "Activations as a share of traffic", trend: computeTrend(rows, "ActivationRate", false) },
            { label: "System Churn", value: formatNumber(totals.SystemChurn), accent: "cyan", sub: "CP-initiated churn", trend: computeTrend(rows, "SystemChurn", true) },
            { label: "User Churn", value: formatNumber(totals.UserChurn), accent: "amber", sub: "User-initiated churn", trend: computeTrend(rows, "UserChurn", true) },
            { label: "Gross Base", value: formatNumber(totals.GrossBase), accent: "violet", sub: "Latest gross subscriber base", trend: computeTrend(rows, "GrossBase", false) }
        ];

        byId("kpiGrid").innerHTML = cards.map(function (card) {
            return "<div class=\"kpi-card " + card.accent + "\">" +
                "<div class=\"kpi-label\">" + card.label + "</div>" +
                "<div class=\"kpi-value\">" + card.value + "</div>" +
                "<div class=\"kpi-sub\">" + card.sub + "</div>" +
                (card.trend || "") +
                "</div>";
        }).join("");
    }

    function detectAnomalies(rows) {
        var anomalyBox = byId("anomStrip");
        var anomalyMetrics = [
            { key: "TotalRevenue", label: "Revenue", severeWhenHigher: false },
            { key: "ActivationCount", label: "Activations", severeWhenHigher: false },
            { key: "TotalVisitors", label: "Visitors", severeWhenHigher: false },
            { key: "ChurnRate", label: "Churn Rate", severeWhenHigher: true }
        ];

        var anomalies = [];

        anomalyMetrics.forEach(function (metric) {
            var values = rows.map(function (row) { return Number(row[metric.key]) || 0; });
            if (values.length < 3) {
                return;
            }

            var average = values.reduce(function (sum, value) { return sum + value; }, 0) / values.length;
            var variance = values.reduce(function (sum, value) {
                return sum + Math.pow(value - average, 2);
            }, 0) / values.length;
            var deviation = Math.sqrt(variance);

            if (!deviation) {
                return;
            }

            values.forEach(function (value, index) {
                var score = (value - average) / deviation;
                if (Math.abs(score) < 2.1) {
                    return;
                }

                var isCritical = metric.severeWhenHigher ? score > 0 : score < 0;
                anomalies.push({
                    label: rows[index].DisplayLabel,
                    message: metric.label + " moved to " + formatMetric(value, metric.key.indexOf("Rate") >= 0 ? "percent" : "number"),
                    detail: "Expected around " + formatMetric(average, metric.key.indexOf("Rate") >= 0 ? "percent" : "number") + " for this range.",
                    critical: isCritical
                });
            });
        });

        if (!anomalies.length) {
            anomalyBox.innerHTML = "";
            anomalyBox.classList.add("hidden");
            return;
        }

        anomalyBox.classList.remove("hidden");
        anomalyBox.innerHTML = anomalies.slice(0, 5).map(function (item) {
            return "<div class=\"anomaly " + (item.critical ? "" : "warn") + "\">" +
                "<div class=\"a-dot " + (item.critical ? "crit" : "warn") + "\"></div>" +
                "<div class=\"a-info\"><strong>" + item.label + "</strong><br><span>" + item.message + " " + item.detail + "</span></div>" +
                "</div>";
        }).join("");
    }

    function formatDeltaChip(currentValue, previousValue, format, inverseGood) {
        var delta = (Number(currentValue) || 0) - (Number(previousValue) || 0);
        var deltaPercent = previousValue ? (delta / previousValue) * 100 : 0;
        var direction = delta > 0 ? "up" : (delta < 0 ? "down" : "flat");
        if (inverseGood && direction !== "flat") {
            direction = direction === "up" ? "down" : "up";
        }

        var prefix = delta > 0 ? "+ " : delta < 0 ? "- " : "";
        var valueText;
        if (format === "currency") {
            valueText = formatCurrency(Math.abs(delta));
        } else if (format === "percent") {
            valueText = Math.abs(delta).toFixed(1) + "%";
        } else {
            valueText = formatNumber(Math.abs(delta));
        }

        var percentText = previousValue ? " | " + (deltaPercent > 0 ? "+" : "") + deltaPercent.toFixed(1) + "%" : "";
        return "<div class=\"delta-chip " + direction + "\">" + prefix + valueText + percentText + "</div>";
    }

    function renderPeriodComparison(currentRows, previousRows) {
        var currentSummary = summarizeRows(currentRows);
        var previousSummary = summarizeRows(previousRows);
        var currentRange = getSelectedDateRange();
        var previousRange = getPreviousDateRange();
        var note = state.previousComparisonNote;
        var metrics = [
            { label: "Total Revenue", current: currentSummary.TotalRevenue, previous: previousSummary.TotalRevenue, format: "currency" },
            { label: "Total Activations", current: currentSummary.TotalActivations, previous: previousSummary.TotalActivations, format: "number" },
            { label: "Renewals", current: currentSummary.RenewalCount, previous: previousSummary.RenewalCount, format: "number" },
            { label: "Churn", current: currentSummary.Churn, previous: previousSummary.Churn, format: "number", inverseGood: true },
            { label: "Active Base", current: currentSummary.ActiveBase, previous: previousSummary.ActiveBase, format: "number" }
        ];

        byId("periodComparisonCard").innerHTML =
            "<div class=\"insight-head\">" +
            "<div><div class=\"insight-title\">Period-over-Period Comparison</div>" +
            "<div class=\"insight-sub\">Current range against the previous equal-length period using the same filters.</div></div>" +
            "<div class=\"insight-note\">Equal-length</div></div>" +
            "<div class=\"insight-meta\">" +
            "<span class=\"insight-chip\">Current: " + formatRangeLabel(currentRange && currentRange.fromDate, currentRange && currentRange.toDate) + "</span>" +
            "<span class=\"insight-chip\">Previous: " + formatRangeLabel(previousRange && previousRange.fromDate, previousRange && previousRange.toDate) + "</span>" +
            (note ? "<span class=\"insight-chip\">" + note + "</span>" : "") +
            "</div>" +
            "<div class=\"insight-metrics\">" + metrics.map(function (metric) {
                return "<div class=\"insight-metric\">" +
                    "<div class=\"insight-label\">" + metric.label + "</div>" +
                    "<div class=\"insight-value\">" + formatMetric(metric.current, metric.format) + "</div>" +
                    "<div class=\"insight-secondary\">Previous: " + formatMetric(metric.previous, metric.format) + "</div>" +
                    formatDeltaChip(metric.current, metric.previous, metric.format, metric.inverseGood) +
                    "</div>";
            }).join("") + "</div>";
    }

    function summarizeBaseMovement(rows) {
        var summary = summarizeRows(rows);
        return {
            OpeningActiveBase: summary.OpeningActiveBase,
            ClosingActiveBase: summary.ClosingActiveBase,
            OpeningGrossBase: summary.OpeningGrossBase,
            ClosingGrossBase: summary.ClosingGrossBase,
            NetActiveDelta: summary.ClosingActiveBase - summary.OpeningActiveBase,
            NetGrossDelta: summary.ClosingGrossBase - summary.OpeningGrossBase,
            ActivationSupportRatio: summary.Churn > 0 ? (summary.ActivationCount / summary.Churn) : (summary.ActivationCount > 0 ? summary.ActivationCount : 0),
            ChurnPressure: summary.OpeningActiveBase > 0 ? (summary.Churn / summary.OpeningActiveBase) * 100 : 0
        };
    }

    function renderBaseMovement(rows) {
        var baseMovementCard = byId("baseMovementCard");
        if (!baseMovementCard) {
            return;
        }

        var baseSummary = summarizeBaseMovement(rows);
        var range = getSelectedDateRange();
        var movementMetrics = [
            { label: "Opening Active Base", value: baseSummary.OpeningActiveBase, format: "number" },
            { label: "Closing Active Base", value: baseSummary.ClosingActiveBase, format: "number" },
            { label: "Net Active Delta", value: baseSummary.NetActiveDelta, format: "number", deltaOnly: true },
            { label: "Opening Gross Base", value: baseSummary.OpeningGrossBase, format: "number" },
            { label: "Closing Gross Base", value: baseSummary.ClosingGrossBase, format: "number" },
            { label: "Net Gross Delta", value: baseSummary.NetGrossDelta, format: "number", deltaOnly: true },
            { label: "Activation Support", value: baseSummary.ActivationSupportRatio, format: "ratio" },
            { label: "Churn Pressure", value: baseSummary.ChurnPressure, format: "percent" }
        ];

        baseMovementCard.innerHTML =
            "<div class=\"insight-head\">" +
            "<div><div class=\"insight-title\">Base Movement Summary</div>" +
            "<div class=\"insight-sub\">Snapshot-based opening and closing base view for the selected reporting period.</div></div>" +
            "<div class=\"insight-note\">Snapshot-based</div></div>" +
            "<div class=\"insight-meta\"><span class=\"insight-chip\">" + formatRangeLabel(range && range.fromDate, range && range.toDate) + "</span></div>" +
            "<div class=\"insight-metrics\">" + movementMetrics.map(function (metric) {
                var valueText;
                if (metric.format === "ratio") {
                    valueText = (Number(metric.value) || 0).toFixed(2) + "x";
                } else if (metric.deltaOnly) {
                    valueText = (metric.value > 0 ? "+ " : metric.value < 0 ? "- " : "") + formatMetric(Math.abs(metric.value), metric.format);
                } else {
                    valueText = formatMetric(metric.value, metric.format);
                }

                return "<div class=\"insight-metric\">" +
                    "<div class=\"insight-label\">" + metric.label + "</div>" +
                    "<div class=\"insight-value\">" + valueText + "</div>" +
                    "<div class=\"insight-secondary\">" +
                    (metric.label === "Activation Support" ? "Activations divided by churn for the selected period." :
                        metric.label === "Churn Pressure" ? "Churn as a share of the opening active base." :
                        "Based on the first and last available snapshots in the selected range.") +
                    "</div></div>";
            }).join("") + "</div>";
    }

    function buildDailyChurnReasonRows(rows) {
        var grouped = {};

        rows.forEach(function (row) {
            if (!row || !row.ReportDate) {
                return;
            }

            var key = row.DateKey || toDateKey(row.ReportDate);
            if (!grouped[key]) {
                grouped[key] = {
                    DisplayLabel: formatDisplayDate(row.ReportDate, true),
                    ShortLabel: formatDayMonth(row.ReportDate),
                    SortValue: row.ReportDate.getTime(),
                    Churn: 0,
                    UserChurn: 0,
                    SystemChurn: 0,
                    Deactivation: 0
                };
            }

            grouped[key].Churn += Number(row.Churn) || 0;
            grouped[key].UserChurn += Number(row.UserChurn) || 0;
            grouped[key].SystemChurn += Number(row.SystemChurn) || 0;
            grouped[key].Deactivation += Number(row.Deactivation) || 0;
        });

        return Object.keys(grouped).map(function (key) {
            return grouped[key];
        }).sort(function (left, right) {
            return left.SortValue - right.SortValue;
        });
    }

    function buildSourceChurnFallbackChart(rows) {
        var sourceRows = buildDimensionRows(rows, "ActivationSource", "Unspecified Source", "Churn").slice(0, 8);
        if (!sourceRows.length) {
            return null;
        }

        var isCompactSnapshot = sourceRows.length <= 3;

        return {
            id: "sourceChurn",
            title: "Source-wise Churn Count",
            subtitle: "Churn volume by activation source for the active date range",
            badge: "Churn",
            badgeAccent: "rose",
            tab: "churn",
            type: "bar",
            row: 3,
            aspectRatio: isCompactSnapshot ? 2.8 : 2.8,
            heightPx: isCompactSnapshot ? 160 : undefined,
            indexAxis: "y",
            cardClass: "focus-card ranking-card" + (isCompactSnapshot ? " snapshot-card compact" : ""),
            labels: sourceRows.map(function (row) { return row.ShortLabel; }),
            rows: sourceRows,
            series: [
                { label: "Churn", columnKey: "Churn", type: "bar", accent: "rose", barPercentage: 0.56, categoryPercentage: 0.62 }
            ]
        };
    }

    function buildDailyChurnReasonChart(rows) {
        var dailyRows = buildDailyChurnReasonRows(rows);
        var hasReasonData = dailyRows.some(function (row) {
            return row.UserChurn > 0 || row.SystemChurn > 0 || row.Deactivation > 0;
        });

        if (!dailyRows.length || !hasReasonData) {
            return null;
        }

        var isSnapshotView = dailyRows.length <= 1;

        return {
            id: "dailyChurnStacked",
            title: isSnapshotView ? "Churn Snapshot" : "Daily Churn - Stacked",
            subtitle: isSnapshotView
                ? "User churn, system churn, and deactivation for the selected day"
                : "User churn, system churn, and deactivation by selected day",
            badge: "Churn",
            badgeAccent: "rose",
            tab: "churn",
            type: "bar",
            row: 3,
            aspectRatio: isSnapshotView ? 2.8 : 2.8,
            heightPx: isSnapshotView ? 160 : undefined,
            stacked: true,
            cardClass: "focus-card" + (isSnapshotView ? " snapshot-card compact" : ""),
            customData: {
                labels: dailyRows.map(function (row) { return row.ShortLabel; }),
                datasets: [
                    {
                        label: "System Churn",
                        data: dailyRows.map(function (row) { return row.SystemChurn; }),
                        backgroundColor: chartColors.cyan.stroke,
                        borderColor: chartColors.cyan.stroke,
                        borderWidth: 0,
                        stack: "churn",
                        barPercentage: 0.56,
                        categoryPercentage: 0.62
                    },
                    {
                        label: "User Churn",
                        data: dailyRows.map(function (row) { return row.UserChurn; }),
                        backgroundColor: chartColors.amber.stroke,
                        borderColor: chartColors.amber.stroke,
                        borderWidth: 0,
                        stack: "churn",
                        barPercentage: 0.56,
                        categoryPercentage: 0.62
                    },
                    {
                        label: "Deactivation",
                        data: dailyRows.map(function (row) { return row.Deactivation; }),
                        backgroundColor: chartColors.violet.stroke,
                        borderColor: chartColors.violet.stroke,
                        borderWidth: 0,
                        stack: "churn",
                        barPercentage: 0.56,
                        categoryPercentage: 0.62
                    }
                ]
            },
            showLegend: true,
            valueFormat: "number"
        };
    }

    function buildChartDefinitions(periodRows, rawRows) {
        var weekdayRows = buildWeekdayRows(rawRows);
        var isSnapshotView = periodRows.length <= 1;
        var definitions = [
            {
                id: "revenue",
                title: isSnapshotView ? "Revenue Snapshot" : "Revenue Trend",
                subtitle: isSnapshotView
                    ? "Activation, renewal, and total revenue for the selected day"
                    : "Activation, renewal, and total revenue across the selected range",
                badge: "Revenue",
                badgeAccent: "amber",
                tab: "revenue",
                type: "bar",
                row: 1,
                aspectRatio: isSnapshotView ? 4.2 : 3.8,
                heightPx: isSnapshotView ? 160 : undefined,
                cardClass: isSnapshotView ? "snapshot-card compact" : "",
                series: [
                    { label: "Activation Revenue", columnKey: "ActivationRevenue", type: "bar", accent: "emerald" },
                    { label: "Renewal Revenue", columnKey: "RenewalRevenue", type: "bar", accent: "amber" },
                    { label: "Total Revenue", columnKey: "TotalRevenue", type: "line", accent: "blue", fill: false }
                ]
            },
            {
                id: "funnel",
                title: isSnapshotView ? "Traffic and Conversion Snapshot" : "Traffic and Conversion Funnel",
                subtitle: isSnapshotView
                    ? "Visitors, unique visitors, free trials, and activations for the selected day"
                    : "Grouped traffic stages with activations overlaid for easier comparison",
                badge: "Funnel",
                badgeAccent: "cyan",
                tab: "traffic",
                type: "bar",
                row: 2,
                aspectRatio: isSnapshotView ? 2.8 : 2.8,
                heightPx: isSnapshotView ? 160 : undefined,
                cardClass: isSnapshotView ? "snapshot-card compact" : "",
                series: [
                    { label: "Visitors", columnKey: "TotalVisitors", type: "bar", accent: "blue", barPercentage: 0.56, categoryPercentage: 0.62 },
                    { label: "Unique Visitors", columnKey: "UniqueVisitors", type: "bar", accent: "cyan", barPercentage: 0.56, categoryPercentage: 0.62 },
                    { label: "Free Trials", columnKey: "FreeTrials", type: "bar", accent: "amber", barPercentage: 0.56, categoryPercentage: 0.62 },
                    { label: "Activations", columnKey: "ActivationCount", type: "line", accent: "emerald", fill: false }
                ]
            },
            buildDailyChurnReasonChart(rawRows) || buildSourceChurnFallbackChart(rawRows)
        ];

        definitions = definitions.concat(buildServiceDailyBreakdownCharts(rawRows));

        if (weekdayRows.length > 1) {
            definitions.push(
                {
                    id: "weekdayRevenue",
                    title: "Weekday Revenue Pattern",
                    subtitle: "See which weekdays generate the strongest activation, renewal, and total revenue",
                    badge: "Weekday",
                    badgeAccent: "amber",
                    tab: "weekday",
                    type: "bar",
                    row: 5,
                    aspectRatio: 2.4,
                    labels: weekdayRows.map(function (row) { return row.ShortLabel; }),
                    rows: weekdayRows,
                    series: [
                        { label: "Activation Revenue", columnKey: "ActivationRevenue", type: "bar", accent: "emerald" },
                        { label: "Renewal Revenue", columnKey: "RenewalRevenue", type: "bar", accent: "amber" },
                        { label: "Total Revenue", columnKey: "TotalRevenue", type: "line", accent: "blue", fill: false }
                    ]
                },
                {
                    id: "weekdayVolume",
                    title: "Weekday Subscriber Pattern",
                    subtitle: "Track activations, renewals, and churn by day of week",
                    badge: "Weekday",
                    badgeAccent: "teal",
                    tab: "weekday",
                    type: "bar",
                    row: 5,
                    aspectRatio: 2.4,
                    labels: weekdayRows.map(function (row) { return row.ShortLabel; }),
                    rows: weekdayRows,
                    series: [
                        { label: "Activations", columnKey: "ActivationCount", type: "bar", accent: "emerald" },
                        { label: "Renewals", columnKey: "RenewalCount", type: "bar", accent: "cyan" },
                        { label: "Churn", columnKey: "Churn", type: "line", accent: "rose", fill: false }
                    ]
                }
            );
        }

        definitions = definitions.concat(buildContributionCharts(rawRows));

        if (hasSelectedService()) {
            var serviceCharts = buildServiceBreakdownCharts(rawRows);
            if (serviceCharts.length) {
                definitions = definitions.concat(serviceCharts);
            }
        } else if (hasSelectedOperatorOnly()) {
            var operatorCharts = buildOperatorServiceCharts(rawRows);
            if (operatorCharts.length) {
                definitions = definitions.concat(operatorCharts);
            }
        }

        return definitions;
    }

    function renderCustomLegend(containerId, chartData, valueFormat) {
        var container = byId(containerId);
        if (!container || !chartData || !chartData.datasets || !chartData.datasets.length) {
            return;
        }

        var dataset = chartData.datasets[0];
        var colors = Array.isArray(dataset.backgroundColor) ? dataset.backgroundColor : [];
        var values = Array.isArray(dataset.data) ? dataset.data : [];

        container.innerHTML = (chartData.labels || []).map(function (label, index) {
            return "<div class=\"doughnut-legend-item\">" +
                "<span class=\"doughnut-legend-swatch\" style=\"background:" + (colors[index] || "#94a3b8") + ";\"></span>" +
                "<span class=\"doughnut-legend-copy\">" +
                "<span class=\"doughnut-legend-label\">" + normalizeText(label) + "</span>" +
                "<span class=\"doughnut-legend-value\">" + formatMetric(values[index], valueFormat || "number") + "</span>" +
                "</span>" +
                "</div>";
        }).join("");
    }

    var doughnutSliceLabelsPlugin = {
        id: "vasDoughnutSliceLabels",
        afterDatasetsDraw: function (chart, args, pluginOptions) {
            if (chart.config.type !== "doughnut" || !pluginOptions || !pluginOptions.enabled) {
                return;
            }

            var meta = chart.getDatasetMeta(0);
            var dataset = chart.data && chart.data.datasets ? chart.data.datasets[0] : null;
            if (!meta || !dataset || !meta.data || !meta.data.length) {
                return;
            }

            var values = Array.isArray(dataset.data) ? dataset.data : [];
            var total = values.reduce(function (sum, value) {
                return sum + (Number(value) || 0);
            }, 0);

            if (!total) {
                return;
            }

            var ctx = chart.ctx;
            ctx.save();
            ctx.font = "700 11px 'Nunito Sans', sans-serif";
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            ctx.lineJoin = "round";
            ctx.strokeStyle = "rgba(15,23,42,.42)";
            ctx.lineWidth = 3;
            ctx.fillStyle = "#ffffff";

            meta.data.forEach(function (arc, index) {
                var value = Number(values[index]) || 0;
                var percent = total > 0 ? (value / total) * 100 : 0;
                var label = normalizeText((chart.data.labels || [])[index]);
                var midAngle;
                var radius;
                var x;
                var y;
                var text;

                if (!value || percent < (pluginOptions.minPercent || 12)) {
                    return;
                }

                if (!pluginOptions.showOtherLabel && label === "Other") {
                    return;
                }

                text = getCompactSliceLabel(label, pluginOptions.maxChars || 12);
                if (!text) {
                    return;
                }

                midAngle = (arc.startAngle + arc.endAngle) / 2;
                radius = arc.innerRadius + ((arc.outerRadius - arc.innerRadius) * 0.58);
                x = arc.x + (Math.cos(midAngle) * radius);
                y = arc.y + (Math.sin(midAngle) * radius);

                ctx.strokeText(text, x, y);
                ctx.fillText(text, x, y);
            });

            ctx.restore();
        }
    };

    function isSingleBucketChart(chartLabels, chartRows) {
        var bucketCount = chartLabels && chartLabels.length
            ? chartLabels.length
            : (chartRows && chartRows.length ? chartRows.length : 0);
        return bucketCount <= 1;
    }

    function resolveChartHeightPx(chartDefinition, chartLabels, chartRows) {
        if (typeof chartDefinition.heightPx === "number") {
            return chartDefinition.heightPx;
        }

        if (chartDefinition.type === "doughnut") {
            return null;
        }

        if (isSingleBucketChart(chartLabels, chartRows)) {
            return chartDefinition.indexAxis === "y" ? 220 : 210;
        }

        return null;
    }

    function renderChartInstance(canvas, chartDefinition, labels, rows) {
        if (!canvas) {
            return;
        }

        var isDoughnut = chartDefinition.type === "doughnut";
        var isHorizontal = chartDefinition.indexAxis === "y";
        var interactionAxis = isHorizontal ? "y" : "x";
        var chartLabels = chartDefinition.labels || labels;
        var chartRows = chartDefinition.rows || rows;
        var singleBucketChart = !isDoughnut && isSingleBucketChart(chartLabels, chartRows);
        var resolvedHeightPx = resolveChartHeightPx(chartDefinition, chartLabels, chartRows);
        var datasetsForCheck = chartDefinition.customData ? (chartDefinition.customData.datasets || []) : chartDefinition.series;
        var hasBarSeries = (datasetsForCheck || []).some(function (series) {
            return (series.type || chartDefinition.type || "bar") !== "line";
        });
        var chartData;

        if (chartDefinition.customData) {
            chartData = chartDefinition.customData;
        } else if (isDoughnut) {
            chartData = {
                labels: chartDefinition.series.map(function (series) { return series.label; }),
                datasets: [{
                    data: chartDefinition.series.map(function (series) {
                        return chartRows.reduce(function (sum, row) {
                            return sum + (Number(row[series.columnKey]) || 0);
                        }, 0);
                    }),
                    backgroundColor: chartDefinition.series.map(function (series) {
                        return (chartColors[series.accent] || chartColors.blue).stroke;
                    }),
                    borderWidth: 0
                }]
            };
        } else {
            chartData = {
                labels: chartLabels,
                datasets: chartDefinition.series.map(function (series) {
                    var palette = chartColors[series.accent] || chartColors.blue;
                    return {
                        label: series.label,
                        data: chartRows.map(function (row) {
                            return Number(row[series.columnKey]) || 0;
                        }),
                        type: series.type === "line" ? "line" : "bar",
                        borderColor: palette.stroke,
                        backgroundColor: series.type === "line" ? palette.fill : palette.stroke,
                        fill: !!series.fill,
                        tension: 0.32,
                        borderRadius: 5,
                        barPercentage: series.type === "line" ? undefined : (typeof series.barPercentage === "number" ? series.barPercentage : 0.72),
                        categoryPercentage: series.type === "line" ? undefined : (typeof series.categoryPercentage === "number" ? series.categoryPercentage : 0.72),
                        pointRadius: series.type === "line" ? (hasBarSeries ? 0 : 2) : 0,
                        pointHoverRadius: series.type === "line" ? (hasBarSeries ? 0 : 4) : 0,
                        pointHitRadius: series.type === "line" ? (hasBarSeries ? 0 : 6) : 0,
                        borderWidth: series.type === "line" ? 2 : 0,
                        order: series.type === "line" ? 1 : 2
                    };
                })
            };
        }

        var barDatasetCount = (chartData.datasets || []).filter(function (dataset) {
            var datasetType = dataset.type || (chartDefinition.type === "line" ? "line" : "bar");
            return datasetType !== "line";
        }).length;

        (chartData.datasets || []).forEach(function (dataset) {
            var datasetType = dataset.type || (chartDefinition.type === "line" ? "line" : "bar");
            if (isDoughnut || datasetType === "line") {
                return;
            }

            if (typeof dataset.borderRadius !== "number") {
                dataset.borderRadius = 5;
            }

            if (singleBucketChart) {
                dataset.maxBarThickness = typeof dataset.maxBarThickness === "number"
                    ? dataset.maxBarThickness
                    : (barDatasetCount > 2 ? 40 : 52);
                dataset.barPercentage = 0.46;
                dataset.categoryPercentage = 0.54;
            }
        });

        if (canvas.parentElement) {
            canvas.parentElement.style.height = typeof resolvedHeightPx === "number" ? resolvedHeightPx + "px" : "";
            canvas.parentElement.style.minHeight = typeof resolvedHeightPx === "number" ? resolvedHeightPx + "px" : "";
        }

        state.charts[canvas.id] = new ChartLibrary(canvas, {
            type: isDoughnut ? "doughnut" : (chartDefinition.type === "line" ? "line" : "bar"),
            data: chartData,
            options: {
                normalized: true,
                events: ["mousemove", "mouseout", "click", "touchstart", "touchmove"],
                responsive: true,
                maintainAspectRatio: typeof resolvedHeightPx !== "number",
                indexAxis: isDoughnut ? "x" : (chartDefinition.indexAxis || "x"),
                aspectRatio: chartDefinition.aspectRatio || (isDoughnut ? 1.15 : 2.4),
                cutout: isDoughnut ? "62%" : undefined,
                layout: singleBucketChart ? {
                    padding: { left: 12, right: 12, top: 2, bottom: 4 }
                } : undefined,
                interaction: isDoughnut ? {
                    mode: "nearest",
                    intersect: true
                } : hasBarSeries ? {
                    mode: "index",
                    intersect: false,
                    axis: interactionAxis
                } : {
                    mode: "nearest",
                    intersect: false,
                    axis: interactionAxis
                },
                hover: isDoughnut ? {
                    mode: "nearest",
                    intersect: true
                } : hasBarSeries ? {
                    mode: "index",
                    intersect: false,
                    axis: interactionAxis
                } : {
                    mode: "nearest",
                    intersect: false,
                    axis: interactionAxis
                },
                scales: isDoughnut ? {} : (function () {
                    var categoryAxisKey = isHorizontal ? "y" : "x";
                    var valueAxisKey = isHorizontal ? "x" : "y";
                    var scales = {};
                    scales[categoryAxisKey] = {
                        stacked: !!chartDefinition.stacked,
                        grid: { display: false },
                        ticks: isHorizontal ? {} : {
                            autoSkip: chartLabels.length > 12,
                            maxRotation: chartLabels.length > 8 ? 45 : 0,
                            minRotation: chartLabels.length > 8 ? 45 : 0
                        }
                    };
                    scales[valueAxisKey] = {
                        stacked: !!chartDefinition.stacked,
                        beginAtZero: true,
                        grid: { color: "rgba(15,23,42,.06)" },
                        max: chartDefinition.maxValue,
                        ticks: {
                            callback: function (value) {
                                return formatAxisTick(value, chartDefinition.axisFormat);
                            }
                        }
                    };
                    return scales;
                })(),
                plugins: {
                    legend: {
                        display: typeof chartDefinition.showLegend === "boolean" ? chartDefinition.showLegend : chartData.datasets.length > 1,
                        position: isDoughnut ? "bottom" : "top"
                    },
                    vasDoughnutSliceLabels: chartDefinition.sliceLabelOptions || { enabled: false },
                    tooltip: {
                        enabled: true,
                        backgroundColor: "rgba(17,24,39,.94)",
                        titleColor: "#ffffff",
                        bodyColor: "#ffffff",
                        borderColor: "rgba(148,163,184,.35)",
                        borderWidth: 1,
                        padding: 12,
                        displayColors: true,
                        callbacks: {
                            title: function (contexts) {
                                if (!contexts || !contexts.length) {
                                    return "";
                                }

                                if (chartDefinition.customData && !isDoughnut) {
                                    return contexts[0].label;
                                }

                                if (isDoughnut) {
                                    if (chartDefinition.customData) {
                                        return contexts[0].label || chartDefinition.title;
                                    }
                                    return chartDefinition.title;
                                }

                                var activeRow = chartRows[contexts[0].dataIndex];
                                return contexts[0].label || (activeRow ? activeRow.DisplayLabel : "");
                            },
                            label: function (context) {
                                if (chartDefinition.customData) {
                                    var customLabel = context.dataset && context.dataset.label ? context.dataset.label : context.label;
                                    return customLabel + ": " + formatMetric(context.raw, chartDefinition.valueFormat || "number");
                                }

                                var seriesDefinition = isDoughnut
                                    ? chartDefinition.series[context.dataIndex]
                                    : chartDefinition.series[context.datasetIndex];
                                var label = seriesDefinition ? seriesDefinition.label : context.dataset.label;
                                var value = context.raw;
                                var columnKey = seriesDefinition ? seriesDefinition.columnKey : null;
                                return label + ": " + formatTooltipMetric(columnKey, value);
                            }
                        }
                    }
                }
            }
        });

        if (chartDefinition.renderCustomLegend) {
            renderCustomLegend("legend_" + chartDefinition.id, chartData, chartDefinition.valueFormat);
        }
    }

    function buildChartCardHtml(chartDefinition) {
        var headerHtml =
            "<div class=\"ch-hd\">" +
            "<div><div class=\"ch-title\">" + chartDefinition.title + "</div>" +
            "<div class=\"ch-sub\">" + chartDefinition.subtitle + "</div></div>" +
            "<span class=\"ch-badge " + (chartDefinition.badgeAccent || "blue") + "\">" + chartDefinition.badge + "</span>" +
            "</div>";

        if (chartDefinition.groupedCharts && chartDefinition.groupedCharts.length) {
            return headerHtml +
                "<div class=\"ranking-stack" + (chartDefinition.groupedLayout === "grid" ? " ranking-stack-grid" : "") + "\">" +
                chartDefinition.groupedCharts.map(function (groupedChart) {
                    var heightStyle = typeof groupedChart.heightPx === "number"
                        ? " style=\"height:" + groupedChart.heightPx + "px; min-height:" + groupedChart.heightPx + "px;\""
                        : "";
                    return "<section class=\"ranking-group" + (groupedChart.type === "doughnut" ? " ranking-group-doughnut" : "") + "\">" +
                        "<div class=\"ranking-group-title\">" + groupedChart.title + "</div>" +
                        "<div class=\"chart-shell ranking-shell" + (groupedChart.type === "doughnut" ? " compact-shell ranking-shell-compact" : "") + "\"" + heightStyle + "><canvas id=\"chart_" + groupedChart.id + "\"></canvas></div>" +
                        (groupedChart.renderCustomLegend ? "<div class=\"doughnut-legend\" id=\"legend_" + groupedChart.id + "\"></div>" : "") +
                        "</section>";
                }).join("") +
                "</div>";
        }

        return headerHtml +
            "<div class=\"chart-shell" + (chartDefinition.type === "doughnut" ? " compact-shell" : "") + "\"><canvas id=\"chart_" + chartDefinition.id + "\"></canvas></div>";
    }

    function renderChartsFromDefinitions(chartDefinitions, labels, rows) {
        var chartArea = byId("chartArea");
        chartArea.innerHTML = "";
        destroyCharts();

        if (!ChartLibrary) {
            chartArea.innerHTML = "<div class=\"chart-fallback\">Chart.js could not be loaded, but the detailed tables and KPIs are still available.</div>";
            return;
        }

        if (!ChartLibrary.__vasDoughnutSliceLabelsRegistered) {
            ChartLibrary.register(doughnutSliceLabelsPlugin);
            ChartLibrary.__vasDoughnutSliceLabelsRegistered = true;
        }

        ChartLibrary.defaults.color = "#6b7280";
        ChartLibrary.defaults.borderColor = "rgba(0,0,0,.08)";
        ChartLibrary.defaults.font.family = "'Nunito Sans', sans-serif";
        ChartLibrary.defaults.font.size = 11;
        ChartLibrary.defaults.plugins.legend.labels.usePointStyle = true;
        ChartLibrary.defaults.plugins.legend.labels.pointStyleWidth = 8;
        ChartLibrary.defaults.plugins.legend.labels.padding = 14;

        var TAB_ORDER = ["revenue", "traffic", "churn", "services", "weekday", "contributions", "breakdown", "operator"];
        var TAB_LABELS = {
            revenue: "Revenue",
            traffic: "Traffic & Funnel",
            churn: "Churn",
            services: "Services",
            weekday: "Weekday",
            contributions: "Contributions",
            breakdown: "Breakdown",
            operator: "Operator View"
        };

        // Group definitions by tab
        var tabCharts = {};
        chartDefinitions.forEach(function (chart) {
            if (!chart) { return; }
            var tab = chart.tab || "other";
            if (!tabCharts[tab]) { tabCharts[tab] = []; }
            tabCharts[tab].push(chart);
        });

        // Build ordered tab list containing only tabs that have charts
        var tabOrder = [];
        TAB_ORDER.forEach(function (tab) {
            if (tabCharts[tab] && tabCharts[tab].length) { tabOrder.push(tab); }
        });
        Object.keys(tabCharts).forEach(function (tab) {
            if (TAB_ORDER.indexOf(tab) < 0 && tabCharts[tab].length) { tabOrder.push(tab); }
        });

        if (!tabOrder.length) { return; }

        // Restore last active tab if still available, otherwise default to first
        var activeTab = (state.activeChartTab && tabOrder.indexOf(state.activeChartTab) >= 0)
            ? state.activeChartTab : tabOrder[0];
        state.activeChartTab = activeTab;

        // Build tab bar
        var tabBar = document.createElement("div");
        tabBar.className = "chart-tab-bar";
        tabOrder.forEach(function (tab) {
            var btn = document.createElement("button");
            btn.type = "button";
            btn.className = "chart-tab-btn" + (tab === activeTab ? " active" : "");
            btn.setAttribute("data-chart-tab", tab);
            btn.textContent = TAB_LABELS[tab] || tab;
            tabBar.appendChild(btn);
        });
        chartArea.appendChild(tabBar);

        // Build all tab panels — all temporarily visible so Chart.js can measure canvas dimensions
        var panelEls = {};
        tabOrder.forEach(function (tab) {
            var panel = document.createElement("div");
            panel.className = "chart-tab-panel";
            panel.setAttribute("data-chart-panel", tab);

            var groupedByRow = {};
            tabCharts[tab].forEach(function (chart) {
                var rowKey = chart.row || 0;
                if (!groupedByRow[rowKey]) { groupedByRow[rowKey] = []; }
                groupedByRow[rowKey].push(chart);
            });

            Object.keys(groupedByRow).sort(function (a, b) {
                return Number(a) - Number(b);
            }).forEach(function (rowKey) {
                var rowCharts = groupedByRow[rowKey];
                var rowElement = document.createElement("div");
                rowElement.className = "ch-row c" + Math.min(rowCharts.length, 3);
                rowCharts.forEach(function (chartDefinition) {
                    var card = document.createElement("div");
                    card.className = "ch-card" + (chartDefinition.cardClass ? " " + chartDefinition.cardClass : "");
                    card.innerHTML = buildChartCardHtml(chartDefinition);
                    rowElement.appendChild(card);
                });
                panel.appendChild(rowElement);
            });

            chartArea.appendChild(panel);
            panelEls[tab] = panel;
        });

        // Instantiate all Chart.js instances while all panels are in DOM
        tabOrder.forEach(function (tab) {
            tabCharts[tab].forEach(function (chartDefinition) {
                if (chartDefinition.groupedCharts && chartDefinition.groupedCharts.length) {
                    chartDefinition.groupedCharts.forEach(function (groupedChart) {
                        renderChartInstance(byId("chart_" + groupedChart.id), groupedChart, labels, rows);
                    });
                    return;
                }
                renderChartInstance(byId("chart_" + chartDefinition.id), chartDefinition, labels, rows);
            });
        });

        // Hide all panels except the active one
        tabOrder.forEach(function (tab) {
            if (tab === activeTab) {
                panelEls[tab].classList.add("active");
            }
        });

        // Wire tab click events
        tabBar.querySelectorAll(".chart-tab-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var clickedTab = btn.getAttribute("data-chart-tab");
                if (clickedTab === state.activeChartTab) { return; }

                tabBar.querySelectorAll(".chart-tab-btn").forEach(function (b) { b.classList.remove("active"); });
                panelEls[state.activeChartTab].classList.remove("active");

                btn.classList.add("active");
                panelEls[clickedTab].classList.add("active");
                state.activeChartTab = clickedTab;

                // Resize charts in the newly shown panel to fill their containers correctly
                panelEls[clickedTab].querySelectorAll("canvas").forEach(function (canvas) {
                    var chart = state.charts[canvas.id];
                    if (chart) { chart.resize(); }
                });
            });
        });
    }

    function renderDailyTable(rows, rawRows) {
        var columns = [
            { key: "DisplayLabel", label: state.viewMode === "weekly" ? "Week" : "Date", format: "text", sortKey: "SortValue" },
            { key: "Country", label: "Country", format: "text", sortKey: "Country" },
            { key: "OperatorName", label: "Operator", format: "text", sortKey: "OperatorName" },
            { key: "ServiceName", label: "Service", format: "text", sortKey: "ServiceName" },
            { key: "TotalVisitors", label: "Visitors", format: "number" },
            { key: "UniqueVisitors", label: "Unique Visitors", format: "number" },
            { key: "ActivationAttempts", label: "Attempts", format: "number" },
            { key: "FreeTrials", label: "Free Trials", format: "number" },
            { key: "ActivationCount", label: "Activations", format: "number" },
            { key: "TotalActivations", label: "Total Activations", format: "number" },
            { key: "ActivationRevenue", label: "Activation Revenue", format: "currency" },
            { key: "RenewalCount", label: "Renewals", format: "number" },
            { key: "RenewalRevenue", label: "Renewal Revenue", format: "currency" },
            { key: "TotalRevenue", label: "Total Revenue", format: "currency" },
            { key: "GrossBase", label: "Gross Base", format: "number" },
            { key: "ActiveBase", label: "Active Base", format: "number" },
            { key: "SystemChurn", label: "System Churn", format: "number" },
            { key: "UserChurn", label: "User Churn", format: "number" },
            { key: "Churn", label: "Churn", format: "number" },
            { key: "ActivationRate", label: "Activation Rate", format: "percent" }
        ];

        dailyTableState.rows = rows.slice();
        dailyTableState.rawRows = rawRows ? rawRows.slice() : [];
        dailyTableState.columns = columns;
        dailyTableState.sortKey = "SortValue";
        dailyTableState.ascending = false;

        byId("tblTitle").textContent = state.viewMode === "weekly" ? "Weekly Breakdown" : "Daily Breakdown";
        var rawCount = dailyTableState.rawRows.length;
        byId("tableSummary").textContent = rawCount + " " + (state.viewMode === "weekly" ? "weekly" : "daily") + " records returned from the secured reporting API layer.";

        renderDailyTableHead();
        renderDailyTableRows();
    }

    function renderDailyTableHead() {
        var head = dailyTableState.columns.map(function (column) {
            var sortKey = column.sortKey || column.key;
            return "<th><button type=\"button\" class=\"sort-btn\" data-sort-target=\"daily\" data-sort-key=\"" + sortKey + "\">" +
                column.label + "<span class=\"sort-icon\">+-</span></button></th>";
        }).join("");
        byId("tHead").innerHTML = "<tr>" + head + "</tr>";

        byId("tHead").querySelectorAll(".sort-btn").forEach(function (button) {
            button.addEventListener("click", function () {
                var sortKey = button.getAttribute("data-sort-key");
                if (dailyTableState.sortKey === sortKey) {
                    dailyTableState.ascending = !dailyTableState.ascending;
                } else {
                    dailyTableState.sortKey = sortKey;
                    dailyTableState.ascending = sortKey === "DisplayLabel";
                }
                renderDailyTableRows();
            });
        });
    }

    function renderDailyTableRows() {
        var sortKey = dailyTableState.sortKey;
        var ascending = dailyTableState.ascending;

        var sorted = dailyTableState.rawRows.slice().sort(function (a, b) {
            var av, bv, cmp;
            if (sortKey === "SortValue") {
                av = a.ReportDate ? a.ReportDate.getTime() : 0;
                bv = b.ReportDate ? b.ReportDate.getTime() : 0;
                return ascending ? av - bv : bv - av;
            }
            av = a[sortKey];
            bv = b[sortKey];
            cmp = typeof av === "string" || typeof bv === "string"
                ? normalizeText(av || "").localeCompare(normalizeText(bv || ""))
                : (Number(av) || 0) - (Number(bv) || 0);
            return ascending ? cmp : -cmp;
        });

        var metricCols = dailyTableState.columns.slice(4);

        var html = sorted.map(function (row, idx) {
            var dateLabel = row.ReportDate ? formatDisplayDate(row.ReportDate, false) : (row.DateKey || "—");
            var country = normalizeText(row.Country) || "—";
            var operator = normalizeText(row.OperatorName) || "—";
            var service = normalizeText(row.ServiceName) || "—";
            var dims = [dateLabel, country, operator, service].join(" ").toLowerCase();
            return "<tr data-row-key=\"r" + idx + "\" data-dims=\"" + dims + "\">" +
                "<td class=\"date-cell\">" + dateLabel + "</td>" +
                "<td>" + country + "</td>" +
                "<td>" + operator + "</td>" +
                "<td>" + service + "</td>" +
                metricCols.map(function (col) {
                    return "<td class=\"numeric\">" + formatMetric(row[col.key], col.format) + "</td>";
                }).join("") +
                "</tr>";
        }).join("");

        byId("reportBody").innerHTML = html;
        updateSortIcons("tHead", sortKey, ascending);
        filterTable();
    }

    function buildComparisonRows(rows) {
        var grouped = {};

        rows.forEach(function (row) {
            var key = [row.Country, row.OperatorName, row.ServiceName].join("|");
            if (!grouped[key]) {
                grouped[key] = createAggregateRow("", "", 0);
                grouped[key].Country = row.Country;
                grouped[key].OperatorName = row.OperatorName;
                grouped[key].ServiceName = row.ServiceName;
            }

            accumulateMetrics(grouped[key], row);
        });

        return Object.keys(grouped).map(function (key) {
            applyDerivedMetrics(grouped[key]);
            return grouped[key];
        });
    }

    function renderComparisonTable(rows) {
        var comparisonRows = buildComparisonRows(rows);
        var comparisonCard = byId("tblComparison");

        if (comparisonRows.length <= 1) {
            comparisonCard.classList.add("hidden");
            comparisonTableState.rows = [];
            return;
        }

        comparisonCard.classList.remove("hidden");

        var showCountry = comparisonRows.some(function (row) {
            return row.Country;
        });

        comparisonTableState.rows = comparisonRows;
        comparisonTableState.showCountry = showCountry;
        comparisonTableState.columns = [
            { key: "OperatorName", label: "Operator", format: "text" },
            { key: "ServiceName", label: "Service", format: "text" },
            { key: "TotalRevenue", label: "Total Revenue", format: "currency" },
            { key: "ActivationRevenue", label: "Activation Revenue", format: "currency" },
            { key: "RenewalRevenue", label: "Renewal Revenue", format: "currency" },
            { key: "ActivationCount", label: "Activations", format: "number" },
            { key: "TotalActivations", label: "Total Activations", format: "number" },
            { key: "RenewalCount", label: "Renewals", format: "number" },
            { key: "TotalVisitors", label: "Visitors", format: "number" },
            { key: "ActiveBase", label: "Active Base", format: "number" },
            { key: "GrossBase", label: "Gross Base", format: "number" },
            { key: "Churn", label: "Churn", format: "number" }
        ];
        comparisonTableState.sortKey = "TotalRevenue";
        comparisonTableState.ascending = false;

        renderComparisonHead();
        renderComparisonRows();
    }

    function renderComparisonHead() {
        var headers = [];
        if (comparisonTableState.showCountry) {
            headers.push("<th><button type=\"button\" class=\"sort-btn\" data-sort-target=\"comparison\" data-sort-key=\"Country\">Country<span class=\"sort-icon\">+-</span></button></th>");
        }

        comparisonTableState.columns.forEach(function (column) {
            headers.push("<th><button type=\"button\" class=\"sort-btn\" data-sort-target=\"comparison\" data-sort-key=\"" + column.key + "\">" +
                column.label + "<span class=\"sort-icon\">+-</span></button></th>");
        });

        byId("cmpHead").innerHTML = "<tr>" + headers.join("") + "</tr>";

        byId("cmpHead").querySelectorAll(".sort-btn").forEach(function (button) {
            button.addEventListener("click", function () {
                var sortKey = button.getAttribute("data-sort-key");
                if (comparisonTableState.sortKey === sortKey) {
                    comparisonTableState.ascending = !comparisonTableState.ascending;
                } else {
                    comparisonTableState.sortKey = sortKey;
                    comparisonTableState.ascending = sortKey === "Country" || sortKey === "OperatorName" || sortKey === "ServiceName";
                }
                renderComparisonRows();
            });
        });
    }

    function renderComparisonRows() {
        var rows = comparisonTableState.rows.slice().sort(function (left, right) {
            var leftValue = left[comparisonTableState.sortKey];
            var rightValue = right[comparisonTableState.sortKey];
            var comparison;

            if (typeof leftValue === "string" || typeof rightValue === "string") {
                comparison = normalizeText(leftValue).localeCompare(normalizeText(rightValue));
            } else {
                comparison = (Number(leftValue) || 0) - (Number(rightValue) || 0);
            }

            return comparisonTableState.ascending ? comparison : -comparison;
        });

        byId("cmpBody").innerHTML = rows.map(function (row) {
            var cells = [];
            if (comparisonTableState.showCountry) {
                cells.push("<td>" + normalizeText(row.Country) + "</td>");
            }

            comparisonTableState.columns.forEach(function (column) {
                var value = row[column.key];
                cells.push("<td class=\"" + (column.format === "text" ? "" : "numeric") + "\">" + formatMetric(value, column.format) + "</td>");
            });

            return "<tr>" + cells.join("") + "</tr>";
        }).join("");

        updateSortIcons("cmpHead", comparisonTableState.sortKey, comparisonTableState.ascending);
    }

    function updateSortIcons(headId, activeKey, ascending) {
        byId(headId).querySelectorAll(".sort-btn").forEach(function (button) {
            var icon = button.querySelector(".sort-icon");
            if (!icon) {
                return;
            }

            if (button.getAttribute("data-sort-key") === activeKey) {
                icon.textContent = ascending ? "^" : "v";
                button.classList.add("sorted");
            } else {
                icon.textContent = "+-";
                button.classList.remove("sorted");
            }
        });
    }

    function exportCsv(filename, rows, columns, includeCountry) {
        if (!rows.length) {
            return;
        }

        var csvRows = [];
        var headers = [];

        if (includeCountry) {
            headers.push("Country");
        }

        columns.forEach(function (column) {
            headers.push(column.label);
        });
        csvRows.push(headers);

        rows.forEach(function (row) {
            var values = [];
            if (includeCountry) {
                values.push(row.Country || "");
            }

            columns.forEach(function (column) {
                values.push(row[column.key]);
            });
            csvRows.push(values);
        });

        var blob = new Blob([csvRows.map(function (row) { return row.join(","); }).join("\n")], { type: "text/csv" });
        var link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        link.click();
        URL.revokeObjectURL(link.href);
    }

    function escapeCsvVal(val) {
        var str = val == null ? "" : String(val);
        if (str.indexOf(",") >= 0 || str.indexOf("\"") >= 0 || str.indexOf("\n") >= 0) {
            return "\"" + str.replace(/"/g, "\"\"") + "\"";
        }
        return str;
    }

    function exportDailyTable() {
        if (!dailyTableState.rawRows.length) { return; }

        var raw = (byId("tableSearch").value || "").toLowerCase();
        var terms = raw.split(",").map(function (t) { return t.trim(); }).filter(function (t) { return t.length > 0; });
        var metricCols = dailyTableState.columns.slice(4); // skip Date, Country, Operator, Service
        var headers = ["Date", "Country", "Operator", "Service"].concat(
            metricCols.map(function (col) { return col.label; })
        );

        var sorted = dailyTableState.rawRows.slice().sort(function (a, b) {
            var sortKey = dailyTableState.sortKey;
            var av = sortKey === "SortValue" ? (a.ReportDate ? a.ReportDate.getTime() : 0) : (a[sortKey] || 0);
            var bv = sortKey === "SortValue" ? (b.ReportDate ? b.ReportDate.getTime() : 0) : (b[sortKey] || 0);
            return dailyTableState.ascending ? (av > bv ? 1 : -1) : (av > bv ? -1 : 1);
        });

        // Apply same multi-term AND filter as the grid display
        if (terms.length) {
            sorted = sorted.filter(function (row) {
                var dateLabel = row.ReportDate ? formatDisplayDate(row.ReportDate, false) : (row.DateKey || "");
                var dims = [dateLabel, row.Country, row.OperatorName, row.ServiceName].join(" ").toLowerCase();
                return terms.every(function (term) { return dims.indexOf(term) >= 0; });
            });
        }

        var csvRows = [headers].concat(sorted.map(function (row) {
            var dateLabel = row.ReportDate ? formatDisplayDate(row.ReportDate, false) : (row.DateKey || "");
            return [
                dateLabel,
                normalizeText(row.Country) || "",
                normalizeText(row.OperatorName) || "",
                normalizeText(row.ServiceName) || ""
            ].concat(metricCols.map(function (col) { return row[col.key] != null ? row[col.key] : ""; }));
        }));

        var csvContent = csvRows.map(function (row) {
            return row.map(escapeCsvVal).join(",");
        }).join("\n");

        var blob = new Blob([csvContent], { type: "text/csv" });
        var link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = "vas-report-" + new Date().toISOString().slice(0, 10) + ".csv";
        link.click();
        URL.revokeObjectURL(link.href);
    }

    function exportComparisonTable() {
        exportCsv(
            "vas-service-comparison-" + new Date().toISOString().slice(0, 10) + ".csv",
            comparisonTableState.rows,
            comparisonTableState.columns,
            comparisonTableState.showCountry
        );
    }

    function filterTable() {
        var raw = (byId("tableSearch").value || "").toLowerCase();
        var terms = raw.split(",").map(function (t) { return t.trim(); }).filter(function (t) { return t.length > 0; });
        var total = 0, visible = 0;
        byId("reportBody").querySelectorAll("tr").forEach(function (tr) {
            total++;
            var dims = tr.getAttribute("data-dims") || "";
            var show = terms.length === 0 || terms.every(function (term) { return dims.indexOf(term) >= 0; });
            tr.style.display = show ? "" : "none";
            if (show) { visible++; }
        });
        var label = state.viewMode === "weekly" ? "weekly" : "daily";
        byId("tableSummary").textContent = terms.length
            ? visible + " of " + total + " " + label + " records match your search."
            : total + " " + label + " records returned from the secured reporting API layer.";
    }

    function renderDashboard() {
        var displayRows = getRenderableRows(state.rawRows);
        var previousDisplayRows = getRenderableRows(state.previousRawRows);
        state.periodRows = buildPeriodRows(displayRows);

        renderKpis(state.periodRows);
        renderPeriodComparison(displayRows, previousDisplayRows);
        renderBaseMovement(displayRows);
        detectAnomalies(state.periodRows);
        renderChartsFromDefinitions(
            buildChartDefinitions(state.periodRows, displayRows),
            state.periodRows.map(function (row) { return row.ShortLabel; }),
            state.periodRows
        );
        renderDailyTable(state.periodRows, displayRows);
        renderComparisonTable(displayRows);
        updateRateInfoBar();
        updateHeader();
        hideEmpty();
    }

    function loadDashboard() {
        if (!state.selectedRegionId) {
            resetCurrencyUi(false);
            showEmpty("Select a region first to load the dashboard.");
            return;
        }

        setLoading(true);
        setError("");
        state.previousComparisonNote = "";

        var previousRange = getPreviousDateRange();
        Promise.all([
            fetchReportRows(),
            previousRange
                ? fetchReportRows(previousRange).then(function (rows) {
                    return { rows: rows, failed: false };
                }).catch(function () {
                    return { rows: [], failed: true };
                })
                : Promise.resolve({ rows: [], failed: false })
        ])
            .then(function (results) {
                state.rawRows = results[0];
                state.previousRawRows = results[1].rows || [];

                if (results[1].failed) {
                    state.previousComparisonNote = "Previous period comparison could not be loaded.";
                    setError("Current period loaded, but previous period comparison is unavailable right now.");
                } else if (previousRange && !state.previousRawRows.length) {
                    state.previousComparisonNote = "Previous period returned no rows.";
                }

                if (!state.rawRows.length) {
                    destroyCharts();
                    state.previousRawRows = [];
                    resetCurrencyUi(false);
                    showEmpty("No report rows were returned for this region and filter combination. Try widening the date range or choosing a different country/operator/service.");
                    return;
                }

                resetCurrencyUi(true);
                fetchExchangeRates();
                renderDashboard();
                closeSidebarOnMobile();
            })
            .catch(function (error) {
                destroyCharts();
                resetCurrencyUi(false);
                showEmpty("The dashboard could not be loaded right now.");
                setError(error.message || "Unable to load the dashboard.");
            })
            .finally(function () {
                setLoading(false);
            });
    }

    function handleViewMode(viewMode, button) {
        state.viewMode = viewMode;
        document.querySelectorAll("#viewTabs .tab").forEach(function (tab) {
            tab.classList.toggle("on", tab === button);
        });

        if (state.rawRows.length) {
            renderDashboard();
        } else {
            updateHeader();
        }
    }

    document.querySelectorAll(".region-chip").forEach(function (chip) {
        chip.addEventListener("click", function () {
            setActiveRegion(chip.getAttribute("data-region-id"));
            state.availableOperators = [];
            loadRegionFilters();
        });
    });

    document.querySelectorAll("#viewTabs .tab").forEach(function (tab) {
        tab.addEventListener("click", function () {
            handleViewMode(tab.getAttribute("data-view"), tab);
        });
    });

    document.querySelectorAll("#currencyTabs .cur-tab").forEach(function (tab) {
        tab.addEventListener("click", function () {
            changeCurrency(tab.getAttribute("data-currency"));
        });
    });

    byId("countryName").addEventListener("change", function () {
        loadOperators()
            .catch(function (error) {
                setError(error.message || "Unable to load operators.");
            });
    });

    byId("operatorName").addEventListener("change", function () {
        loadServices().catch(function (error) {
            setError(error.message || "Unable to load services.");
        });
    });
    byId("applyFilters").addEventListener("click", loadDashboard);
    byId("tableSearch").addEventListener("input", filterTable);
    byId("exportCsv").addEventListener("click", exportDailyTable);
    byId("exportComparisonCsv").addEventListener("click", exportComparisonTable);
    byId("sidebarToggle").addEventListener("click", function () { toggleSidebar(); });
    byId("sidebarClose").addEventListener("click", function () { toggleSidebar(false); });
    byId("sidebarOverlay").addEventListener("click", function () { toggleSidebar(false); });

    setDefaultDates();
    resetCurrencyUi(false);
    setActiveRegion(state.selectedRegionId || (document.querySelector(".region-chip") || {}).getAttribute("data-region-id") || "");
    handleViewMode("daily", byId("viewTabs").querySelector(".tab.on"));
    clearSelect("operatorName", "All operators");
    clearSelect("serviceName", "All services");
    loadRegionFilters();
    updateHeader();
})();
