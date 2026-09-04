using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Builds InfluxQL autocomplete suggestions from keywords, functions, and schema names.
    /// </summary>
    static class InfluxQlAutoComplete
    {
        public enum Context
        {
            General,
            Select,
            From,
            Where,
            GroupBy,
            OrderBy,
            Show,
            Fill,
            Limit
        }

        public enum Kind
        {
            Field = 0,
            Tag = 1,
            Function = 2,
            Measurement = 3,
            Keyword = 4
        }

        public sealed class CompletionItem
        {
            public string InsertText { get; private set; }
            public Kind Kind { get; private set; }

            public CompletionItem(string insertText, Kind kind)
            {
                InsertText = insertText;
                Kind = kind;
            }

            public string ToListEntry()
            {
                var label = KindLabel(Kind);
                return InsertText + "  [" + label + "]" + TypeSeparator + ((int)Kind).ToString();
            }
        }

        public const int MaxItems = 80;
        public const char ListSeparator = '\n';
        public const char TypeSeparator = '?';

        public static readonly string[] Keywords =
        {
            "SELECT", "FROM", "WHERE", "GROUP BY", "BY", "ORDER BY", "LIMIT", "OFFSET",
            "SLIMIT", "SOFFSET", "FILL", "INTO", "AS", "AND", "OR", "NOT", "ON",
            "DISTINCT", "ALL", "ASC", "DESC", "TRUE", "FALSE", "NULL", "TZ",
            "SHOW", "DROP", "CREATE", "GRANT", "REVOKE", "KILL", "ALTER", "DELETE",
            "DATABASE", "DATABASES", "MEASUREMENT", "MEASUREMENTS", "SERIES",
            "RETENTION", "POLICY", "POLICIES", "USER", "USERS", "CONTINUOUS",
            "QUERY", "QUERIES", "SHARD", "SHARDS", "FIELD", "TAG", "KEYS", "VALUES",
            "DIAGNOSTICS", "STATS", "WITH", "REPLICATION", "DURATION", "DEFAULT",
            "PASSWORD", "PRIVILEGES", "READ", "WRITE"
        };

        public static readonly string[] Functions =
        {
            "COUNT", "MEAN", "MEDIAN", "MODE", "SPREAD", "STDDEV", "SUM",
            "MIN", "MAX", "FIRST", "LAST", "PERCENTILE", "SAMPLE", "TOP", "BOTTOM",
            "DERIVATIVE", "NON_NEGATIVE_DERIVATIVE", "DIFFERENCE", "NON_NEGATIVE_DIFFERENCE",
            "ELAPSED", "MOVING_AVERAGE", "CUMULATIVE_SUM", "INTEGRAL", "HOLT_WINTERS",
            "ABS", "CEIL", "FLOOR", "ROUND", "SQRT", "EXP", "LN", "LOG", "LOG2", "LOG10",
            "SIN", "COS", "TAN", "ASIN", "ACOS", "ATAN", "ATAN2", "POW",
            "now()"
        };

        public static readonly string[] ShowCompletions =
        {
            "DATABASES", "MEASUREMENTS", "SERIES", "TAG", "KEYS", "FIELD",
            "RETENTION", "POLICIES", "CONTINUOUS", "QUERIES", "USERS",
            "DIAGNOSTICS", "STATS", "SHARDS", "VALUES"
        };

        public static readonly string[] FillCompletions =
        {
            "none", "null", "previous", "linear", "0"
        };

        static readonly string[] AfterSelectClauses =
        {
            "FROM", "INTO", "DISTINCT", "WHERE", "GROUP BY", "ORDER BY", "LIMIT"
        };

        static readonly string[] AfterFromClauses =
        {
            "WHERE", "GROUP BY", "ORDER BY", "LIMIT", "OFFSET"
        };

        static readonly string[] AfterWhereClauses =
        {
            "AND", "OR", "GROUP BY", "ORDER BY", "LIMIT", "OFFSET"
        };

        static readonly string[] AfterGroupClauses =
        {
            "BY", "FILL", "ORDER BY", "LIMIT", "OFFSET"
        };

        static readonly string[] AfterOrderClauses =
        {
            "BY", "ASC", "DESC", "LIMIT", "OFFSET"
        };

        static readonly string[] AfterLimitClauses =
        {
            "OFFSET", "SLIMIT", "SOFFSET"
        };

        static readonly Regex FromMeasurementRegex = new Regex(
            @"\bFROM\s+(?:(?:""[^""]+""|[\w.\-]+)\.)*(?:""(?<m>[^""]+)""|(?<m>[\w.\-]+))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static readonly Regex IdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");

        public static string KindLabel(Kind kind)
        {
            switch (kind)
            {
                case Kind.Field: return Localization.T("Field");
                case Kind.Tag: return Localization.T("Tag");
                case Kind.Function: return Localization.T("Function");
                case Kind.Measurement: return Localization.T("Measurement");
                default: return Localization.T("Keyword");
            }
        }

        public static bool IsInStringOrComment(string textBeforeCursor)
        {
            if (string.IsNullOrEmpty(textBeforeCursor)) return false;

            var inSingle = false;

            for (var i = 0; i < textBeforeCursor.Length; i++)
            {
                var c = textBeforeCursor[i];
                var next = i + 1 < textBeforeCursor.Length ? textBeforeCursor[i + 1] : '\0';

                if (!inSingle && c == '-' && next == '-')
                    return true;

                if (c == '\'') inSingle = !inSingle;
            }

            return inSingle;
        }

        public static Context GetContext(string textBeforeWord)
        {
            if (string.IsNullOrWhiteSpace(textBeforeWord)) return Context.General;
            if (Regex.IsMatch(textBeforeWord, @"\bFILL\s*\(\s*$", RegexOptions.IgnoreCase)) return Context.Fill;

            var ctx = Context.General;
            var lastIndex = -1;

            Consider("SELECT", Context.Select, textBeforeWord, ref lastIndex, ref ctx);
            Consider("FROM", Context.From, textBeforeWord, ref lastIndex, ref ctx);
            Consider("INTO", Context.From, textBeforeWord, ref lastIndex, ref ctx);
            Consider("WHERE", Context.Where, textBeforeWord, ref lastIndex, ref ctx);
            Consider("AND", Context.Where, textBeforeWord, ref lastIndex, ref ctx);
            Consider("OR", Context.Where, textBeforeWord, ref lastIndex, ref ctx);
            Consider("GROUP", Context.GroupBy, textBeforeWord, ref lastIndex, ref ctx);
            Consider("ORDER", Context.OrderBy, textBeforeWord, ref lastIndex, ref ctx);
            Consider("LIMIT", Context.Limit, textBeforeWord, ref lastIndex, ref ctx);
            Consider("OFFSET", Context.Limit, textBeforeWord, ref lastIndex, ref ctx);
            Consider("SHOW", Context.Show, textBeforeWord, ref lastIndex, ref ctx);
            Consider("FILL", Context.Fill, textBeforeWord, ref lastIndex, ref ctx);

            return ctx;
        }

        static void Consider(string keyword, Context context, string text, ref int lastIndex, ref Context current)
        {
            var matches = Regex.Matches(text, @"\b" + keyword + @"\b", RegexOptions.IgnoreCase);
            if (matches.Count == 0) return;
            var idx = matches[matches.Count - 1].Index;
            if (idx >= lastIndex)
            {
                lastIndex = idx;
                current = context;
            }
        }

        public static string GetFromMeasurement(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            var matches = FromMeasurementRegex.Matches(query);
            if (matches.Count == 0) return null;
            return matches[matches.Count - 1].Groups["m"].Value;
        }

        public static string FormatIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (IdentifierRegex.IsMatch(name)) return name;
            return "\"" + name.Replace("\"", "\\\"") + "\"";
        }

        public static string GetInsertText(string selected)
        {
            if (string.IsNullOrEmpty(selected)) return selected;

            var text = selected;
            var typeAt = selected.LastIndexOf(TypeSeparator);
            if (typeAt > 0 && typeAt < selected.Length - 1 && char.IsDigit(selected[typeAt + 1]))
                text = selected.Substring(0, typeAt);

            var labelAt = text.LastIndexOf("  [", StringComparison.Ordinal);
            if (labelAt > 0) return text.Substring(0, labelAt).TrimEnd();

            var bracketAt = text.LastIndexOf('[');
            if (bracketAt > 0) return text.Substring(0, bracketAt).TrimEnd();

            return text.Trim();
        }

        public static IList<CompletionItem> BuildItems(Context context, string prefix,
            IEnumerable<string> measurements, IEnumerable<string> fields, IEnumerable<string> tags)
        {
            var items = new List<CompletionItem>();

            switch (context)
            {
                case Context.From:
                    AddNames(items, measurements, Kind.Measurement);
                    AddNames(items, AfterFromClauses, Kind.Keyword);
                    break;

                case Context.Show:
                    AddNames(items, ShowCompletions, Kind.Keyword);
                    break;

                case Context.Fill:
                    AddNames(items, FillCompletions, Kind.Keyword);
                    break;

                case Context.Where:
                    AddNames(items, fields, Kind.Field);
                    AddNames(items, tags, Kind.Tag);
                    items.Add(new CompletionItem("time", Kind.Field));
                    items.Add(new CompletionItem("now()", Kind.Function));
                    AddNames(items, AfterWhereClauses, Kind.Keyword);
                    break;

                case Context.GroupBy:
                    AddNames(items, tags, Kind.Tag);
                    items.Add(new CompletionItem("time", Kind.Field));
                    AddNames(items, fields, Kind.Field);
                    AddNames(items, AfterGroupClauses, Kind.Keyword);
                    break;

                case Context.OrderBy:
                    items.Add(new CompletionItem("time", Kind.Field));
                    AddNames(items, AfterOrderClauses, Kind.Keyword);
                    break;

                case Context.Limit:
                    AddNames(items, AfterLimitClauses, Kind.Keyword);
                    break;

                case Context.Select:
                    AddNames(items, fields, Kind.Field);
                    AddNames(items, tags, Kind.Tag);
                    items.Add(new CompletionItem("*", Kind.Keyword));
                    items.Add(new CompletionItem("time", Kind.Field));
                    AddNames(items, Functions, Kind.Function);
                    AddNames(items, AfterSelectClauses, Kind.Keyword);
                    break;

                default:
                    AddNames(items, fields, Kind.Field);
                    AddNames(items, tags, Kind.Tag);
                    AddNames(items, Functions, Kind.Function);
                    AddNames(items, measurements, Kind.Measurement);
                    AddNames(items, Keywords, Kind.Keyword);
                    break;
            }

            // Always offer matching keywords/functions once the user has started a word,
            // so prefixes like "ord" still complete to ORDER after FROM/WHERE.
            if (!string.IsNullOrEmpty(prefix) && context != Context.Fill)
            {
                AddNames(items, Keywords, Kind.Keyword);
                AddNames(items, Functions, Kind.Function);
            }

            return Filter(items, prefix, MaxItems);
        }

        static void AddNames(ICollection<CompletionItem> items, IEnumerable<string> names, Kind kind)
        {
            if (names == null) return;
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.IndexOf(ListSeparator) >= 0) continue;
                if (name.IndexOf(TypeSeparator) >= 0) continue;
                items.Add(new CompletionItem(kind == Kind.Measurement || kind == Kind.Field || kind == Kind.Tag
                    ? FormatIdentifier(name)
                    : name, kind));
            }
        }

        static IList<CompletionItem> Filter(IEnumerable<CompletionItem> items, string prefix, int max)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filtered = new List<CompletionItem>();
            var prefixBare = (prefix ?? string.Empty).Trim().Trim('"');

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.InsertText)) continue;
                var key = ((int)item.Kind) + ":" + item.InsertText;
                if (!seen.Add(key)) continue;
                if (!MatchesPrefix(item.InsertText, prefixBare)) continue;
                filtered.Add(item);
            }

            filtered.Sort((a, b) => CompareItems(a, b, prefixBare));

            if (filtered.Count <= max) return filtered;

            // Prefer keywords/functions that already match the prefix so they are not
            // dropped when a measurement has many fields/tags.
            var preferred = filtered.Where(i => i.Kind == Kind.Keyword || i.Kind == Kind.Function).ToList();
            var rest = filtered.Where(i => i.Kind != Kind.Keyword && i.Kind != Kind.Function).ToList();
            var kept = new List<CompletionItem>(preferred);
            var remaining = Math.Max(0, max - kept.Count);
            if (remaining > 0) kept.AddRange(rest.GetRange(0, Math.Min(remaining, rest.Count)));
            kept.Sort((a, b) => CompareItems(a, b, prefixBare));
            return kept;
        }

        static int CompareItems(CompletionItem a, CompletionItem b, string prefix)
        {
            var rank = SortRank(a.Kind, prefix).CompareTo(SortRank(b.Kind, prefix));
            if (rank != 0) return rank;
            return string.Compare(a.InsertText, b.InsertText, StringComparison.OrdinalIgnoreCase);
        }

        static int SortRank(Kind kind, string prefix)
        {
            // After the user starts typing, put SQL keywords/functions first
            // so "g" shows GROUP BY instead of being buried under fields.
            if (!string.IsNullOrEmpty(prefix))
            {
                switch (kind)
                {
                    case Kind.Keyword: return 0;
                    case Kind.Function: return 1;
                    case Kind.Measurement: return 2;
                    case Kind.Field: return 3;
                    default: return 4;
                }
            }

            return (int)kind;
        }

        static bool MatchesPrefix(string item, string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return true;
            var bare = item.Trim('"');
            return bare.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
