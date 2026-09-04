using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Formats query series rows as InfluxDB line protocol.
    /// </summary>
    static class LineProtocolExport
    {
        static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Formats a query series as line protocol text (one point per line).
        /// </summary>
        /// <param name="series">Query result series.</param>
        /// <param name="tagKeys">Known tag keys for the measurement (may be null/empty).</param>
        /// <param name="fieldKeys">Known field keys for the measurement (may be null/empty).</param>
        /// <param name="selectedRowIndexes">When non-null, only these row indexes are exported.</param>
        /// <returns>Line protocol body, or empty if nothing could be written.</returns>
        public static string FormatSeries(
            InfluxDbSeries series,
            ICollection<string> tagKeys,
            ICollection<string> fieldKeys,
            ICollection<int> selectedRowIndexes = null)
        {
            if (series == null) throw new ArgumentNullException("series");
            if (string.IsNullOrWhiteSpace(series.Name)) return string.Empty;

            var tagKeySet = ToOrdinalSet(tagKeys);
            var fieldKeySet = ToOrdinalSet(fieldKeys);
            var selected = selectedRowIndexes == null ? null : ToIndexSet(selectedRowIndexes);

            var timeIndex = -1;
            for (var i = 0; i < series.Columns.Count; i++)
            {
                if (string.Equals(series.Columns[i], "time", StringComparison.OrdinalIgnoreCase))
                {
                    timeIndex = i;
                    break;
                }
            }

            var sb = new StringBuilder();
            var line = new StringBuilder();

            for (var rowIndex = 0; rowIndex < series.Values.Count; rowIndex++)
            {
                if (selected != null && !selected.Contains(rowIndex)) continue;

                var row = series.Values[rowIndex];
                line.Clear();

                if (!TryAppendPoint(line, series, row, timeIndex, tagKeySet, fieldKeySet))
                    continue;

                if (sb.Length > 0) sb.Append('\n');
                sb.Append(line);
            }

            return sb.ToString();
        }

        static bool TryAppendPoint(
            StringBuilder line,
            InfluxDbSeries series,
            IList<object> row,
            int timeIndex,
            HashSet<string> tagKeySet,
            HashSet<string> fieldKeySet)
        {
            EscapeMeasurement(line, series.Name);

            var tags = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (series.Tags != null)
            {
                foreach (var tag in series.Tags)
                {
                    if (string.IsNullOrEmpty(tag.Key) || tag.Value == null) continue;
                    tags[tag.Key] = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
                }
            }

            var fields = new List<KeyValuePair<string, object>>();
            DateTime? timestamp = null;

            for (var i = 0; i < series.Columns.Count; i++)
            {
                var column = series.Columns[i];
                var value = i < row.Count ? row[i] : null;

                if (i == timeIndex || string.Equals(column, "time", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime dt;
                    if (AppSettings.TryGetDateTime(value, out dt))
                        timestamp = ToUtc(dt);
                    continue;
                }

                if (IsTagColumn(column, tagKeySet, fieldKeySet, series.Tags))
                {
                    if (value == null) continue;
                    tags[column] = Convert.ToString(value, CultureInfo.InvariantCulture);
                }
                else if (value != null)
                {
                    fields.Add(new KeyValuePair<string, object>(column, value));
                }
            }

            foreach (var tag in tags)
            {
                line.Append(',');
                EscapeTagKey(line, tag.Key);
                line.Append('=');
                EscapeTagValue(line, tag.Value);
            }

            if (fields.Count == 0) return false;

            line.Append(' ');
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0) line.Append(',');
                var field = fields[i];
                EscapeFieldKey(line, field.Key);
                line.Append('=');
                AppendFieldValue(line, field.Value);
            }

            if (timestamp.HasValue)
            {
                line.Append(' ');
                line.Append(ToUnixNanoseconds(timestamp.Value).ToString(CultureInfo.InvariantCulture));
            }

            return true;
        }

        static bool IsTagColumn(
            string column,
            HashSet<string> tagKeySet,
            HashSet<string> fieldKeySet,
            IDictionary<string, string> seriesTags)
        {
            if (seriesTags != null && seriesTags.ContainsKey(column)) return true;
            if (tagKeySet.Contains(column)) return true;
            if (fieldKeySet.Contains(column)) return false;
            return false;
        }

        static void AppendFieldValue(StringBuilder sb, object value)
        {
            if (value is bool)
            {
                sb.Append((bool)value ? "t" : "f");
                return;
            }

            if (IsInteger(value))
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                sb.Append('i');
                return;
            }

            if (value is float || value is double || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            // Quoted string (also covers unexpected types)
            sb.Append('"');
            EscapeStringField(sb, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            sb.Append('"');
        }

        static bool IsInteger(object value)
        {
            return value is sbyte || value is byte
                || value is short || value is ushort
                || value is int || value is uint
                || value is long || value is ulong;
        }

        static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static long ToUnixNanoseconds(DateTime utc)
        {
            utc = ToUtc(utc);
            return (utc.Ticks - UnixEpoch.Ticks) * 100L;
        }

        static HashSet<string> ToOrdinalSet(ICollection<string> values)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (values == null) return set;
            foreach (var v in values)
            {
                if (!string.IsNullOrEmpty(v)) set.Add(v);
            }
            return set;
        }

        static HashSet<int> ToIndexSet(ICollection<int> values)
        {
            var set = new HashSet<int>();
            foreach (var v in values) set.Add(v);
            return set;
        }

        // measurement: escape space, comma, backslash
        static void EscapeMeasurement(StringBuilder sb, string value)
        {
            Escape(sb, value, escapeEquals: false);
        }

        // tag key / field key / tag value: escape space, comma, equals, backslash
        static void EscapeTagKey(StringBuilder sb, string value)
        {
            Escape(sb, value, escapeEquals: true);
        }

        static void EscapeTagValue(StringBuilder sb, string value)
        {
            Escape(sb, value, escapeEquals: true);
        }

        static void EscapeFieldKey(StringBuilder sb, string value)
        {
            Escape(sb, value, escapeEquals: true);
        }

        static void Escape(StringBuilder sb, string value, bool escapeEquals)
        {
            if (value == null) return;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '\\' || c == ' ' || c == ',' || (escapeEquals && c == '='))
                    sb.Append('\\');
                sb.Append(c);
            }
        }

        static void EscapeStringField(StringBuilder sb, string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '\\' || c == '"')
                    sb.Append('\\');
                sb.Append(c);
            }
        }
    }
}
