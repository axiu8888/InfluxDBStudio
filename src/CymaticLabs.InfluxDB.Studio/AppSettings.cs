using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// Date format string for day-first dates.
        /// </summary>
        public const string DateFormatDay = "d/MM/yyyy";

        /// <summary>
        /// Date format string for month-first dates.
        /// </summary>
        public const string DateFormatMonth = "M/dd/yyyy";

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

        #endregion Fields

        #region Properties

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
                    Properties.Settings.Default.TimeFormat = timeFormat;
                    Properties.Settings.Default.Save(); // update settings file
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
                if (string.IsNullOrEmpty(time)) return DateFormat;
                return (DateFormat + " " + time).Trim();
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
                    Properties.Settings.Default.DateFormat = dateFormat;
                    Properties.Settings.Default.Save(); // update settings file
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
                    Properties.Settings.Default.Language = language;
                    Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.AllowUntrustedSsl = allowUntrustedSsl;
                    Properties.Settings.Default.Save(); // update settings file
                }
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
            // Initialize default settings
            timeFormat = TimeFormat12Hour;
            dateFormat = DateFormatMonth;
            language = string.Empty;
            allowUntrustedSsl = false;
            Connections = new List<InfluxDbConnection>();

            // Set the version string
            Version = GetType().Assembly.GetName().Version.ToString();

            // Upgrade settings as needed
            Properties.Settings.Default.Upgrade();
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
                var format = ToDotNetDateTimeFormat(DateTimeFormat);
                return local.ToString(format, CultureInfo.InvariantCulture);
            }
            catch
            {
                return local.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Formats a cell value, applying the current date/time format when the value is a timestamp.
        /// InfluxDB timestamps are UTC and are converted to the local time zone for display.
        /// </summary>
        public string FormatValue(object value)
        {
            if (value == null) return null;
            DateTime dt;
            if (TryGetDateTime(value, out dt))
                return FormatDateTime(dt);
            return value.ToString();
        }

        /// <summary>
        /// Converts a timestamp value to local time for JSON export so it matches the query list.
        /// </summary>
        public object ToLocalExportValue(object value)
        {
            DateTime dt;
            if (TryGetDateTime(value, out dt))
                return ToLocalDisplayTime(dt);
            return value;
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

            var text = value as string;
            if (string.IsNullOrWhiteSpace(text) || !LooksLikeTimestamp(text))
                return false;

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
            timeFormat = Properties.Settings.Default.TimeFormat;
            dateFormat = Properties.Settings.Default.DateFormat;
            language = Properties.Settings.Default.Language;
            allowUntrustedSsl = Properties.Settings.Default.AllowUntrustedSsl;
            LoadConnections();
        }

        /// <summary>
        /// Saves all current settings ti disk.
        /// </summary>
        public void SaveAll()
        {
            Properties.Settings.Default.TimeFormat = TimeFormat;
            Properties.Settings.Default.DateFormat = DateFormat;
            Properties.Settings.Default.Language = Language;
            Properties.Settings.Default.AllowUntrustedSsl = AllowUntrustedSsl;
            SaveConnections();
        }

        /// <summary>
        /// Loads all connections data from disk.
        /// </summary>
        public void LoadConnections()
        {
            var json = Properties.Settings.Default.ConnectionsJson;
            var loadedConnections = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            Connections = new List<InfluxDbConnection>();

            if (loadedConnections is JArray)
            {
                var array = (JArray)loadedConnections;

                foreach (var item in array)
                {
                    try
                    {
                        // Convert to connection object
                        var connection = item.ToObject<InfluxDbConnection>();
                        if (connection != null) Connections.Add(connection);
                    }
                    catch (Exception ex)
                    {
                        AppForm.DisplayException(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Saves current connection data to disk.
        /// </summary>
        public void SaveConnections()
        {
            try
            {
                // Save to disk
                var json = JsonConvert.SerializeObject(Connections);
                Properties.Settings.Default.ConnectionsJson = json;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        #endregion Methods
    }
}
