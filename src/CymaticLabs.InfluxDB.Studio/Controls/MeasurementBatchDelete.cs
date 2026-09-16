using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Batch-deletes points from selected measurements using InfluxQL DELETE ... WHERE time / tags.
    /// </summary>
    static class MeasurementBatchDelete
    {
        public sealed class Options
        {
            public string Database { get; set; }
            public IList<string> Measurements { get; set; }
            public string RetentionPolicyName { get; set; }
            public DateTime FromLocal { get; set; }
            public DateTime ToLocal { get; set; }
            public string ExtraWhere { get; set; }
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
            public int Failed { get; set; }
            public List<string> Errors { get; private set; }

            public Result()
            {
                Errors = new List<string>();
            }
        }

        public static string BuildDeleteStatement(
            string measurement,
            string retentionPolicy,
            DateTime fromLocal,
            DateTime toLocal,
            string extraWhere)
        {
            if (string.IsNullOrWhiteSpace(measurement)) throw new ArgumentNullException("measurement");

            var fromUtc = ToUtc(fromLocal);
            var toUtc = ToUtc(toLocal);
            var fromLiteral = FormatRfc3339(fromUtc);
            var toLiteral = FormatRfc3339(toUtc);

            // InfluxDB DELETE does not support retention policy in FROM (SELECT does).
            // Specifying "rp"."measurement" yields: retention policy not supported.
            var sb = new StringBuilder();
            sb.Append("DELETE FROM ");
            sb.Append(QuoteIdent(measurement));
            sb.Append(" WHERE time >= '");
            sb.Append(fromLiteral);
            sb.Append("' AND time <= '");
            sb.Append(toLiteral);
            sb.Append('\'');

            if (!string.IsNullOrWhiteSpace(extraWhere))
            {
                var normalized = MeasurementBatchExport.NormalizeExtraWhere(extraWhere);
                if (!string.IsNullOrEmpty(normalized))
                {
                    sb.Append(" AND (");
                    sb.Append(normalized);
                    sb.Append(')');
                }
            }

            return sb.ToString();
        }

        public static async Task<Result> RunAsync(
            InfluxDbClient client,
            Options options,
            IProgress<ProgressUpdate> progress,
            CancellationToken cancellationToken)
        {
            if (client == null) throw new ArgumentNullException("client");
            if (options == null) throw new ArgumentNullException("options");
            if (string.IsNullOrWhiteSpace(options.Database))
                throw new ArgumentException("Database is required.", "options");
            if (options.Measurements == null || options.Measurements.Count == 0)
                throw new ArgumentException("No measurements selected.", "options");

            var result = new Result();
            var total = options.Measurements.Count;

            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var measurement = options.Measurements[i];
                Report(progress, i + 1, total, measurement,
                    Localization.Format("Deleting from {0}...", measurement));

                try
                {
                    var sql = BuildDeleteStatement(
                        measurement,
                        options.RetentionPolicyName,
                        options.FromLocal,
                        options.ToLocal,
                        options.ExtraWhere);

                    // DELETE returns an empty result set; errors throw from the client.
                    await client.QueryAsync(options.Database, sql).ConfigureAwait(true);
                    result.Succeeded++;
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
