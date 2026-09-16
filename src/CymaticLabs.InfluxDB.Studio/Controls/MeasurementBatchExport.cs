using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Batch-exports selected measurements using chunked (streaming) queries, writing files named after each measurement.
    /// </summary>
    static class MeasurementBatchExport
    {
        public const int DefaultChunkSize = 10000;

        [Flags]
        public enum Formats
        {
            None = 0,
            Csv = 1,
            Excel = 2,
            Json = 4,
            LineProtocol = 8
        }

        public sealed class Options
        {
            public string Database { get; set; }
            public IList<string> Measurements { get; set; }
            public string RetentionPolicyName { get; set; }
            public DateTime FromLocal { get; set; }
            public DateTime ToLocal { get; set; }
            public string ExtraWhere { get; set; }
            public int? Limit { get; set; }
            public Formats Formats { get; set; }
            public string OutputDirectory { get; set; }
            public int ChunkSize { get; set; }
        }

        public sealed class ProgressUpdate
        {
            public int Index { get; set; }
            public int Total { get; set; }
            public string Measurement { get; set; }
            public string Message { get; set; }
        }

        public sealed class Result
        {
            public int Succeeded { get; set; }
            public int SkippedEmpty { get; set; }
            public int Failed { get; set; }
            public List<string> Errors { get; private set; }

            public Result()
            {
                Errors = new List<string>();
            }
        }

        public static string BuildQuery(string measurement, string retentionPolicy, DateTime fromLocal, DateTime toLocal, string extraWhere, int? limit)
        {
            if (string.IsNullOrWhiteSpace(measurement)) throw new ArgumentNullException("measurement");

            var fromUtc = ToUtc(fromLocal);
            var toUtc = ToUtc(toLocal);
            var fromLiteral = FormatRfc3339(fromUtc);
            var toLiteral = FormatRfc3339(toUtc);

            var fromClause = string.IsNullOrWhiteSpace(retentionPolicy)
                ? QuoteIdent(measurement)
                : QuoteIdent(retentionPolicy) + "." + QuoteIdent(measurement);

            var sb = new StringBuilder();
            sb.Append("SELECT * FROM ");
            sb.Append(fromClause);
            sb.Append(" WHERE time >= '");
            sb.Append(fromLiteral);
            sb.Append("' AND time <= '");
            sb.Append(toLiteral);
            sb.Append('\'');

            if (!string.IsNullOrWhiteSpace(extraWhere))
            {
                var normalized = NormalizeExtraWhere(extraWhere);
                if (!string.IsNullOrEmpty(normalized))
                {
                    sb.Append(" AND (");
                    sb.Append(normalized);
                    sb.Append(')');
                }
            }

            if (limit.HasValue && limit.Value > 0)
            {
                sb.Append(" LIMIT ");
                sb.Append(limit.Value.ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "_";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }
            return new string(chars);
        }

        public static async Task<Result> RunAsync(
            InfluxDbClient client,
            Options options,
            IProgress<ProgressUpdate> progress,
            CancellationToken cancellationToken)
        {
            if (client == null) throw new ArgumentNullException("client");
            if (options == null) throw new ArgumentNullException("options");
            if (options.Measurements == null || options.Measurements.Count == 0)
                throw new ArgumentException("No measurements selected.", "options");
            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                throw new ArgumentException("Output directory is required.", "options");
            if (options.Formats == Formats.None)
                throw new ArgumentException("At least one format is required.", "options");

            Directory.CreateDirectory(options.OutputDirectory);

            var chunkSize = options.ChunkSize > 0 ? options.ChunkSize : DefaultChunkSize;
            var result = new Result();
            var total = options.Measurements.Count;

            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var measurement = options.Measurements[i];
                Report(progress, i + 1, total, measurement, Localization.Format("Querying {0}...", measurement));

                try
                {
                    var wrote = await ExportMeasurementStreamingAsync(
                        client, options, measurement, chunkSize, progress, i + 1, total, cancellationToken)
                        .ConfigureAwait(true);

                    if (wrote)
                        result.Succeeded++;
                    else
                        result.SkippedEmpty++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add(string.Format("{0}: {1}", measurement, ex.Message));
                }
            }

            return result;
        }

        static async Task<bool> ExportMeasurementStreamingAsync(
            InfluxDbClient client,
            Options options,
            string measurement,
            int chunkSize,
            IProgress<ProgressUpdate> progress,
            int index,
            int total,
            CancellationToken cancellationToken)
        {
            var sql = BuildQuery(
                measurement,
                options.RetentionPolicyName,
                options.FromLocal,
                options.ToLocal,
                options.ExtraWhere,
                options.Limit);

            var baseName = SanitizeFileName(measurement);
            var wantCsv = (options.Formats & Formats.Csv) != 0;
            var wantExcel = (options.Formats & Formats.Excel) != 0;
            var wantJson = (options.Formats & Formats.Json) != 0;
            var wantLp = (options.Formats & Formats.LineProtocol) != 0;

            StreamWriter csvWriter = null;
            StreamWriter lpWriter = null;
            var csvHeaderWritten = false;
            var excelHeaders = (IList<string>)null;
            List<IList<string>> excelRows = null;
            List<object> jsonRows = null;
            long pointCount = 0;
            var chunkIndex = 0;

            ICollection<string> tagKeys = null;
            ICollection<string> fieldKeys = null;
            if (wantLp)
            {
                try
                {
                    var tags = await client.GetTagKeysAsync(options.Database, measurement).ConfigureAwait(true);
                    var fields = await client.GetFieldKeysAsync(options.Database, measurement).ConfigureAwait(true);
                    if (tags != null) tagKeys = new List<string>(tags);
                    if (fields != null)
                    {
                        fieldKeys = new List<string>();
                        foreach (var fk in fields)
                        {
                            if (fk != null && !string.IsNullOrEmpty(fk.Name))
                                fieldKeys.Add(fk.Name);
                        }
                    }
                }
                catch
                {
                }
            }

            try
            {
                if (wantCsv)
                    csvWriter = new StreamWriter(Path.Combine(options.OutputDirectory, baseName + ".csv"), false, new UTF8Encoding(false));
                if (wantJson)
                    jsonRows = new List<object>();
                if (wantLp)
                    lpWriter = new StreamWriter(Path.Combine(options.OutputDirectory, baseName + ".lp"), false, new UTF8Encoding(false));
                if (wantExcel)
                    excelRows = new List<IList<string>>();

                await client.QueryChunkedAsync(
                    options.Database,
                    sql,
                    chunkSize,
                    async (seriesList, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        chunkIndex++;

                        foreach (var series in seriesList)
                        {
                            if (series == null || series.Values == null || series.Values.Count == 0)
                                continue;

                            // Ensure measurement name for LP when series name is blank.
                            var named = series;
                            if (string.IsNullOrWhiteSpace(series.Name))
                            {
                                named = new InfluxDbSeries(
                                    measurement,
                                    series.Columns,
                                    series.Tags ?? new Dictionary<string, string>(),
                                    series.Values);
                            }

                            pointCount += series.Values.Count;

                            if (wantCsv)
                                AppendCsv(csvWriter, named, ref csvHeaderWritten);

                            if (wantJson)
                                AppendJsonRows(named, jsonRows);

                            if (wantLp)
                                AppendLineProtocol(lpWriter, named, tagKeys, fieldKeys);

                            if (wantExcel)
                                AppendExcelRows(named, ref excelHeaders, excelRows);
                        }

                        Report(progress, index, total, measurement,
                            Localization.Format("Querying {0}... chunk {1}, {2} points", measurement, chunkIndex, pointCount));

                        await Task.Yield();
                    },
                    cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                if (csvWriter != null) csvWriter.Dispose();
                if (lpWriter != null) lpWriter.Dispose();
            }

            if (pointCount == 0)
            {
                // Remove empty files created for this measurement.
                TryDelete(Path.Combine(options.OutputDirectory, baseName + ".csv"));
                TryDelete(Path.Combine(options.OutputDirectory, baseName + ".lp"));
                return false;
            }

            if (wantJson && jsonRows != null && jsonRows.Count > 0)
            {
                var path = Path.Combine(options.OutputDirectory, baseName + ".json");
                // Same format as QueryResultsControl JSON export.
                File.WriteAllText(path, JsonConvert.SerializeObject(jsonRows, Formatting.Indented), new UTF8Encoding(false));
            }

            if (wantExcel && excelHeaders != null && excelRows != null && excelRows.Count > 0)
            {
                var path = Path.Combine(options.OutputDirectory, baseName + ".xlsx");
                ExcelExport.WriteXlsx(path, measurement, excelHeaders, excelRows);
            }

            return true;
        }

        static void AppendCsv(StreamWriter writer, InfluxDbSeries series, ref bool headerWritten)
        {
            var sb = new StringBuilder();
            if (!headerWritten)
            {
                for (var i = 0; i < series.Columns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(series.Columns[i]);
                }
                writer.WriteLine(sb.ToString());
                headerWritten = true;
            }

            foreach (var row in series.Values)
            {
                sb.Clear();
                for (var c = 0; c < series.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(',');
                    var val = c < row.Count ? row[c] : null;
                    var columnName = c < series.Columns.Count ? series.Columns[c] : null;
                    sb.Append(FormatCell(val, columnName));
                }
                writer.WriteLine(sb.ToString());
            }
        }

        static void AppendJsonRows(InfluxDbSeries series, List<object> rows)
        {
            foreach (var row in series.Values)
            {
                var d = new Dictionary<string, object>();
                for (var i = 0; i < series.Columns.Count; i++)
                {
                    var val = i < row.Count ? row[i] : null;
                    d[series.Columns[i]] = AppForm.Settings.ToLocalExportValue(val, series.Columns[i]);
                }
                rows.Add(d);
            }
        }

        static void AppendLineProtocol(
            StreamWriter writer,
            InfluxDbSeries series,
            ICollection<string> tagKeys,
            ICollection<string> fieldKeys)
        {
            var body = LineProtocolExport.FormatSeries(series, tagKeys, fieldKeys, null);
            if (string.IsNullOrEmpty(body)) return;

            if (writer.BaseStream.Position > 0)
                writer.WriteLine();
            writer.Write(body);
        }

        static void AppendExcelRows(InfluxDbSeries series, ref IList<string> headers, List<IList<string>> rows)
        {
            if (headers == null)
                headers = new List<string>(series.Columns);

            foreach (var row in series.Values)
            {
                var cells = new List<string>(series.Columns.Count);
                for (var c = 0; c < series.Columns.Count; c++)
                {
                    var val = c < row.Count ? row[c] : null;
                    var columnName = c < series.Columns.Count ? series.Columns[c] : null;
                    cells.Add(FormatCell(val, columnName));
                }
                rows.Add(cells);
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        static void Report(IProgress<ProgressUpdate> progress, int index, int total, string measurement, string message)
        {
            if (progress == null) return;
            progress.Report(new ProgressUpdate
            {
                Index = index,
                Total = total,
                Measurement = measurement,
                Message = message
            });
        }

        static string FormatCell(object value, string columnName)
        {
            if (value == null) return string.Empty;
            var formatted = AppForm.Settings.FormatValue(value, columnName);
            return formatted ?? string.Empty;
        }

        /// <summary>
        /// Collapses multiline extra WHERE into a single InfluxQL predicate (newlines → spaces).
        /// Strips a leading WHERE / AND / OR so users can paste fragments from existing queries.
        /// </summary>
        public static string NormalizeExtraWhere(string extraWhere)
        {
            if (string.IsNullOrWhiteSpace(extraWhere)) return string.Empty;

            var sb = new StringBuilder(extraWhere.Length);
            var lastWasSpace = true;
            for (var i = 0; i < extraWhere.Length; i++)
            {
                var ch = extraWhere[i];
                if (ch == '\r' || ch == '\n' || ch == '\t' || char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }
                    continue;
                }

                sb.Append(ch);
                lastWasSpace = false;
            }

            var normalized = sb.ToString().Trim();
            normalized = StripLeadingBooleanKeyword(normalized);
            return normalized;
        }

        /// <summary>
        /// Removes a leading WHERE / AND / OR keyword (case-insensitive).
        /// </summary>
        static string StripLeadingBooleanKeyword(string predicate)
        {
            if (string.IsNullOrEmpty(predicate)) return predicate;

            string[] prefixes = { "WHERE", "AND", "OR" };
            foreach (var prefix in prefixes)
            {
                if (predicate.Length <= prefix.Length) continue;
                if (!predicate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                var next = predicate[prefix.Length];
                if (!char.IsWhiteSpace(next) && next != '(') continue;

                return predicate.Substring(prefix.Length).TrimStart();
            }

            return predicate;
        }

        static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Unspecified)
                return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
            return value.ToUniversalTime();
        }

        static string FormatRfc3339(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        static string QuoteIdent(string ident)
        {
            return "\"" + (ident ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
