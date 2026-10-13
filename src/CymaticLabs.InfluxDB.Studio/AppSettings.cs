using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CymaticLabs.InfluxDB.Data;

namespace CymaticLabs.InfluxDB.Studio
{
    /// <summary>
    /// Intermediate object used to import or export application settings.
    /// </summary>
    public class AppSettings
    {
        #region Fields

        /// <summary>
        /// Time format string for 12 hour time.
        /// </summary>
        public const string TimeFormat12Hour = "hh:mm:ss tt";

        /// <summary>
        /// Time format string for 24 hour time.
        /// </summary>
        public const string TimeFormat24Hour = "HH:mm:ss";

        /// <summary>
        /// Preferred default date format.
        /// </summary>
        public const string DateFormatIso = "yyyy-MM-dd";

        /// <summary>
        /// Date format string for day-first dates.
        /// </summary>
        public const string DateFormatDay = "d/MM/yyyy";

        /// <summary>
        /// Date format string for month-first dates.
        /// </summary>
        public const string DateFormatMonth = "M/dd/yyyy";

        // Historical factory defaults from the original English app / old user.config.
        const string LegacyFactoryTimeFormat = "hh:mm:ss tt";
        const string LegacyFactoryDateFormat = "M/dd/yyyy";

        /// <summary>
        /// Preset date format strings shown in Settings.
        /// </summary>
        public static readonly string[] DateFormats = new[]
        {
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy",
            DateFormatMonth,
            DateFormatDay
        };

        /// <summary>
        /// Preset time format strings shown in Settings.
        /// </summary>
        public static readonly string[] TimeFormats = new[]
        {
            "HH:mm:ss",
            "HH:mm:ss.fff",
            "HH:mm:ss.SSS",
            TimeFormat12Hour,
            "HH:mm"
        };

        // Whether or not to allow untrusted SSL certificates
        bool allowUntrustedSsl = false;

        // Internal app time format setting
        string timeFormat;

        // Internal app date format setting
        string dateFormat;

        // UI language (en / zh-CN)
        string language;

        string queryFavoritesJson = string.Empty;
        string queryHistoryJson = string.Empty;
        string lastMeasurementExportDirectory = string.Empty;

        // Bumped when preferred ISO defaults replace old English factory formats.
        const int CurrentFormatDefaultsVersion = 2;
        int formatDefaultsVersion;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Path to portable app settings (same folder as the exe).
        /// </summary>
        public static string SettingsFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"); }
        }

        /// <summary>
        /// Gets or sets the application version the settings are for/from.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// Gets the current time format setting.
        /// </summary>
        public string TimeFormat
        {
            get { return timeFormat; }

            set
            {
                if (timeFormat != value)
                {
                    timeFormat = value;
                    SavePortableSettings();
                }
            }
        }

        /// <summary>
        /// Gets the combined date and time format used to display timestamps.
        /// </summary>
        public string DateTimeFormat
        {
            get { return (DateFormat + " " + TimeFormat).Trim(); }
        }

        /// <summary>
        /// Gets a DateTimePicker-safe format (milliseconds are not supported there).
        /// </summary>
        public string DateTimePickerFormat
        {
            get
            {
                var time = Regex.Replace(TimeFormat ?? string.Empty, @"\.(f+|S+)", string.Empty, RegexOptions.IgnoreCase).Trim();
                string combined;
                if (string.IsNullOrEmpty(time))
                    combined = DateFormat;
                else
                    combined = (DateFormat + " " + time).Trim();
                return EscapeDateSeparators(ToDotNetDateTimeFormat(combined));
            }
        }

        /// <summary>
        /// Gets the current date format setting.
        /// </summary>
        public string DateFormat
        {
            get { return dateFormat; }

            set
            {
                if (dateFormat != value)
                {
                    dateFormat = value;
                    SavePortableSettings();
                }
            }
        }

        /// <summary>
        /// Gets or sets the UI language. Empty means follow the operating system.
        /// </summary>
        public string Language
        {
            get { return language; }

            set
            {
                if (language != value)
                {
                    language = value ?? string.Empty;
                    SavePortableSettings();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether or not the application should allow untrusted SSL certificates
        /// when communicating to InfluxDB servers.
        /// </summary>
        public bool AllowUntrustedSsl
        {
            get { return allowUntrustedSsl; }

            set
            {
                if (allowUntrustedSsl != value)
                {
                    allowUntrustedSsl = value;
                    SavePortableSettings();
                }
            }
        }

        /// <summary>
        /// Serialized query favorites JSON (portable settings).
        /// </summary>
        public string QueryFavoritesJson
        {
            get { return queryFavoritesJson ?? string.Empty; }
            set
            {
                queryFavoritesJson = value ?? string.Empty;
                SavePortableSettings();
            }
        }

        /// <summary>
        /// Serialized query history JSON (portable settings).
        /// </summary>
        public string QueryHistoryJson
        {
            get { return queryHistoryJson ?? string.Empty; }
            set
            {
                queryHistoryJson = value ?? string.Empty;
                SavePortableSettings();
            }
        }

        /// <summary>
        /// Last folder used by measurement export.
        /// </summary>
        public string LastMeasurementExportDirectory
        {
            get { return lastMeasurementExportDirectory ?? string.Empty; }
            set
            {
                lastMeasurementExportDirectory = value ?? string.Empty;
                SavePortableSettings();
            }
        }

        /// <summary>
        /// Gets or sets the available InfluxDB connections.
        /// </summary>
        public List<InfluxDbConnection> Connections { get; set; }

        #endregion Properties

        #region Constructors

        public AppSettings()
        {
            // Preferred defaults (also used when settings.json is missing).
            timeFormat = TimeFormat24Hour;
            dateFormat = DateFormatIso;
            language = string.Empty;
            allowUntrustedSsl = false;
            queryFavoritesJson = string.Empty;
            queryHistoryJson = string.Empty;
            lastMeasurementExportDirectory = string.Empty;
            formatDefaultsVersion = CurrentFormatDefaultsVersion;
            Connections = new List<InfluxDbConnection>();

            // Set the version string
            Version = GetType().Assembly.GetName().Version.ToString();

            // Upgrade legacy user.config once (for migration into settings.json).
            try { Properties.Settings.Default.Upgrade(); }
            catch { }
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Formats a timestamp in the local time zone using the current date and time settings.
        /// </summary>
        public string FormatDateTime(DateTime value)
        {
            var local = ToLocalDisplayTime(value);
            try
            {
                var format = ToDotNetDateTimeFormat(EscapeDateSeparators(DateTimeFormat));
                if (string.IsNullOrWhiteSpace(format))
                    format = "yyyy-MM-dd HH:mm:ss";
                return local.ToString(format, CultureInfo.InvariantCulture);
            }
            catch
            {
                return local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Formats a cell value. Timezone conversion applies only to the reserved Influx column
        /// named exactly <c>time</c>. User fields such as <c>save_time</c> or <c>device_time</c>
        /// are returned as stored — they are not parsed as timestamps.
        /// </summary>
        public string FormatValue(object value, string columnName = null)
        {
            if (value == null) return null;
            if (IsInfluxTimeColumn(columnName))
            {
                DateTime dt;
                if (TryGetDateTime(value, out dt))
                    return FormatDateTime(dt);
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a value for JSON / clipboard export so the <c>time</c> column matches the query list.
        /// </summary>
        public object ToLocalExportValue(object value, string columnName = null)
        {
            if (IsInfluxTimeColumn(columnName))
            {
                DateTime dt;
                if (TryGetDateTime(value, out dt))
                    return FormatDateTime(dt);
            }
            return value;
        }

        /// <summary>
        /// True only for Influx's reserved point timestamp column <c>time</c>.
        /// Names like <c>save_time</c> or <c>device_time</c> are not matched.
        /// </summary>
        public static bool IsInfluxTimeColumn(string columnName)
        {
            return string.Equals(columnName, "time", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts an InfluxDB UTC timestamp to local time for display.
        /// Unspecified Kind is treated as UTC, which is how InfluxDB stores time.
        /// </summary>
        public static DateTime ToLocalDisplayTime(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local)
                return value;
            if (value.Kind == DateTimeKind.Unspecified)
                value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.ToLocalTime();
        }

        public static bool TryGetDateTime(object value, out DateTime dateTime)
        {
            dateTime = default(DateTime);
            if (value == null) return false;

            if (value is DateTime)
            {
                dateTime = (DateTime)value;
                return true;
            }

            if (value is DateTimeOffset)
            {
                dateTime = ((DateTimeOffset)value).UtcDateTime;
                return true;
            }

            // Do NOT treat bare integers/longs as Unix timestamps here:
            // measurement fields (memory_mb, uptime_sec, process_count, etc.) are often ints
            // and would be wrongly converted to 1970-... dates.

            var text = value as string;
            if (string.IsNullOrWhiteSpace(text) || !LooksLikeTimestamp(text))
                return false;

            // Truncate >7 fractional digits so .NET Framework can parse Influx RFC3339 nano times.
            text = TruncateFractionalSeconds(text);

            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
            {
                dateTime = dto.UtcDateTime;
                return true;
            }

            return false;
        }

        static bool LooksLikeTimestamp(string text)
        {
            if (text.Length < 19) return false;
            if (text.IndexOf('T') >= 0) return true;
            return text.Length >= 10 && text[4] == '-' && text[7] == '-';
        }

        /// <summary>
        /// In custom DateTime formats, '/' is the culture date separator. Quote it so Settings
        /// patterns like yyyy/MM/dd always render literal slashes with InvariantCulture.
        /// </summary>
        public static string EscapeDateSeparators(string format)
        {
            if (string.IsNullOrEmpty(format)) return format;
            var sb = new StringBuilder(format.Length * 2);
            var inQuote = false;
            for (var i = 0; i < format.Length; i++)
            {
                var c = format[i];
                if (c == '\'')
                {
                    inQuote = !inQuote;
                    sb.Append(c);
                    continue;
                }
                if (!inQuote && c == '/')
                {
                    sb.Append("'/'");
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string TruncateFractionalSeconds(string text)
        {
            var dot = text.IndexOf('.');
            if (dot < 0) return text;

            var end = dot + 1;
            while (end < text.Length && char.IsDigit(text[end])) end++;
            var fracLen = end - dot - 1;
            if (fracLen <= 7) return text;

            // Keep 7 digits (100ns resolution) + remainder suffix (Z or offset).
            return text.Substring(0, dot + 1 + 7) + text.Substring(end);
        }

        /// <summary>
        /// Converts Java-style millisecond tokens (SSS) to .NET (fff).
        /// </summary>
        public static string ToDotNetDateTimeFormat(string format)
        {
            if (string.IsNullOrEmpty(format)) return format;
            return format.Replace("SSS", "fff");
        }

        /// <summary>
        /// Loads settings from disk.
        /// </summary>
        public void LoadAll()
        {
            LoadPortableSettings();
            LoadConnections();
        }

        /// <summary>
        /// Saves all current settings to disk.
        /// </summary>
        public void SaveAll()
        {
            SavePortableSettings();
            SaveConnections();
        }

        /// <summary>
        /// Reads the language preference from portable settings (or legacy user.config).
        /// Safe to call before the main form exists.
        /// </summary>
        public static string ReadPortableLanguage()
        {
            try
            {
                var path = SettingsFilePath;
                if (File.Exists(path))
                {
                    var data = JsonConvert.DeserializeObject<PortableSettingsData>(
                        File.ReadAllText(path, Encoding.UTF8));
                    if (data != null && data.Language != null)
                        return data.Language;
                }
            }
            catch { }

            try
            {
                return Properties.Settings.Default.Language ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        void LoadPortableSettings()
        {
            try
            {
                var path = SettingsFilePath;
                if (File.Exists(path))
                {
                    var data = JsonConvert.DeserializeObject<PortableSettingsData>(
                        File.ReadAllText(path, Encoding.UTF8));
                    if (data != null)
                    {
                        ApplyPortableData(data);
                        if (UpgradeLegacyFactoryFormatsIfNeeded())
                            SavePortableSettings();
                        return;
                    }
                }

                // One-time migration from path-scoped user.config.
                MigrateFromLegacyUserConfig();
                UpgradeLegacyFactoryFormatsIfNeeded();
                formatDefaultsVersion = CurrentFormatDefaultsVersion;
                SavePortableSettings();
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        void MigrateFromLegacyUserConfig()
        {
            try
            {
                var legacyTime = Properties.Settings.Default.TimeFormat;
                var legacyDate = Properties.Settings.Default.DateFormat;

                // Do NOT import the original app factory defaults — keep yyyy-MM-dd / HH:mm:ss.
                if (!string.IsNullOrWhiteSpace(legacyTime) && !IsLegacyFactoryTimeFormat(legacyTime))
                    timeFormat = legacyTime;
                if (!string.IsNullOrWhiteSpace(legacyDate) && !IsLegacyFactoryDateFormat(legacyDate))
                    dateFormat = legacyDate;

                language = Properties.Settings.Default.Language ?? string.Empty;
                allowUntrustedSsl = Properties.Settings.Default.AllowUntrustedSsl;
                queryFavoritesJson = Properties.Settings.Default.QueryFavoritesJson ?? string.Empty;
                queryHistoryJson = Properties.Settings.Default.QueryHistoryJson ?? string.Empty;
                lastMeasurementExportDirectory =
                    Properties.Settings.Default.LastMeasurementExportDirectory ?? string.Empty;
            }
            catch { }
        }

        void ApplyPortableData(PortableSettingsData data)
        {
            if (!string.IsNullOrWhiteSpace(data.TimeFormat)) timeFormat = data.TimeFormat;
            if (!string.IsNullOrWhiteSpace(data.DateFormat)) dateFormat = data.DateFormat;
            language = data.Language ?? string.Empty;
            allowUntrustedSsl = data.AllowUntrustedSsl;
            queryFavoritesJson = data.QueryFavoritesJson ?? string.Empty;
            queryHistoryJson = data.QueryHistoryJson ?? string.Empty;
            lastMeasurementExportDirectory = data.LastMeasurementExportDirectory ?? string.Empty;
            formatDefaultsVersion = data.FormatDefaultsVersion;
        }

        /// <summary>
        /// One-time: replace old English factory formats with preferred ISO defaults.
        /// After FormatDefaultsVersion is bumped, intentional choices like M/dd/yyyy are kept.
        /// </summary>
        bool UpgradeLegacyFactoryFormatsIfNeeded()
        {
            if (formatDefaultsVersion >= CurrentFormatDefaultsVersion)
                return false;

            if (IsLegacyFactoryTimeFormat(timeFormat))
                timeFormat = TimeFormat24Hour;
            if (IsLegacyFactoryDateFormat(dateFormat))
                dateFormat = DateFormatIso;

            formatDefaultsVersion = CurrentFormatDefaultsVersion;
            return true; // always persist the version bump
        }

        static bool IsLegacyFactoryTimeFormat(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                || string.Equals(value, LegacyFactoryTimeFormat, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsLegacyFactoryDateFormat(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                || string.Equals(value, LegacyFactoryDateFormat, StringComparison.OrdinalIgnoreCase);
        }

        void SavePortableSettings()
        {
            try
            {
                var data = new PortableSettingsData
                {
                    TimeFormat = timeFormat,
                    DateFormat = dateFormat,
                    Language = language ?? string.Empty,
                    AllowUntrustedSsl = allowUntrustedSsl,
                    QueryFavoritesJson = queryFavoritesJson ?? string.Empty,
                    QueryHistoryJson = queryHistoryJson ?? string.Empty,
                    LastMeasurementExportDirectory = lastMeasurementExportDirectory ?? string.Empty,
                    FormatDefaultsVersion = formatDefaultsVersion
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        sealed class PortableSettingsData
        {
            public string TimeFormat { get; set; }
            public string DateFormat { get; set; }
            public string Language { get; set; }
            public bool AllowUntrustedSsl { get; set; }
            public string QueryFavoritesJson { get; set; }
            public string QueryHistoryJson { get; set; }
            public string LastMeasurementExportDirectory { get; set; }
            public int FormatDefaultsVersion { get; set; }
        }

        /// <summary>
        /// Path to the portable connections file (same folder as the exe).
        /// </summary>
        public static string ConnectionsFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connections.json"); }
        }

        /// <summary>
        /// Loads all connections from <c>connections.json</c> next to the executable.
        /// If the file is missing, migrates once from the legacy user.config setting.
        /// </summary>
        public void LoadConnections()
        {
            Connections = new List<InfluxDbConnection>();

            try
            {
                var path = ConnectionsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    Connections = ParseConnectionsJson(json);
                    return;
                }

                // One-time migration from Properties.Settings (path-scoped user.config).
                var legacy = Properties.Settings.Default.ConnectionsJson;
                if (!string.IsNullOrWhiteSpace(legacy))
                {
                    Connections = ParseConnectionsJson(legacy);
                    if (Connections.Count > 0)
                        SaveConnections();
                }
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        /// <summary>
        /// Saves current connection data to <c>connections.json</c> next to the executable.
        /// </summary>
        public void SaveConnections()
        {
            try
            {
                var path = ConnectionsFilePath;
                var json = JsonConvert.SerializeObject(Connections, Formatting.Indented);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        static List<InfluxDbConnection> ParseConnectionsJson(string json)
        {
            var list = new List<InfluxDbConnection>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            var loaded = JsonConvert.DeserializeObject(json);
            var array = loaded as JArray;
            if (array == null) return list;

            foreach (var item in array)
            {
                try
                {
                    var connection = item.ToObject<InfluxDbConnection>();
                    if (connection != null) list.Add(connection);
                }
                catch (Exception ex)
                {
                    AppForm.DisplayException(ex);
                }
            }

            return list;
        }

        #endregion Methods
    }
}
