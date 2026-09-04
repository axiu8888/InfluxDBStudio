using System;
using System.Collections.Generic;
using System.Text;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Formats InfluxQL as a single line or a readable multi-line statement.
    /// </summary>
    static class InfluxQlFormatter
    {
        static readonly HashSet<string> ClauseKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "GROUP", "ORDER", "LIMIT", "INTO",
            "SHOW", "DROP", "CREATE", "DELETE", "GRANT", "REVOKE", "ALTER", "KILL"
        };

        static readonly HashSet<string> UppercaseKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "GROUP", "BY", "ORDER", "LIMIT", "OFFSET",
            "SLIMIT", "SOFFSET", "FILL", "INTO", "AS", "AND", "OR", "NOT",
            "DISTINCT", "ASC", "DESC", "SHOW", "DROP", "CREATE", "GRANT", "REVOKE",
            "ALTER", "DELETE", "KILL", "ON", "TO", "WITH", "ALL"
        };

        public static string FormatCompact(string sql)
        {
            var tokens = Tokenize(sql);
            if (tokens.Count == 0) return sql ?? string.Empty;
            return Join(tokens, pretty: false).Trim();
        }

        public static string FormatPretty(string sql)
        {
            var tokens = Tokenize(sql);
            if (tokens.Count == 0) return sql ?? string.Empty;
            return Join(tokens, pretty: true).TrimEnd() + Environment.NewLine;
        }

        static string Join(IList<string> tokens, bool pretty)
        {
            var sb = new StringBuilder();
            string previous = null;

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = Normalize(tokens[i]);
                var upper = token.ToUpperInvariant();

                if (pretty && IsComment(token))
                {
                    if (sb.Length > 0 && !EndsWithNewline(sb)) sb.AppendLine();
                    sb.Append(token.TrimEnd());
                    sb.AppendLine();
                    previous = token;
                    continue;
                }

                if (token == ";")
                {
                    sb.Append(";");
                    if (pretty && i < tokens.Count - 1) sb.AppendLine().AppendLine();
                    previous = token;
                    continue;
                }

                if (pretty && (upper == "AND" || upper == "OR"))
                {
                    if (sb.Length > 0 && !EndsWithNewline(sb)) sb.AppendLine();
                    sb.Append("    ");
                    sb.Append(upper);
                    previous = upper;
                    continue;
                }

                if (pretty && IsClauseStart(tokens, i))
                {
                    if (sb.Length > 0 && !EndsWithNewline(sb) && previous != ";")
                        sb.AppendLine();

                    sb.Append(upper);
                    previous = upper;

                    if ((upper == "GROUP" || upper == "ORDER") &&
                        i + 1 < tokens.Count &&
                        string.Equals(tokens[i + 1], "BY", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(" BY");
                        i++;
                        previous = "BY";
                    }

                    continue;
                }

                AppendWithSpacing(sb, previous, token);
                previous = token;
            }

            return sb.ToString();
        }

        static bool IsClauseStart(IList<string> tokens, int index)
        {
            var token = tokens[index];
            if (!ClauseKeywords.Contains(token)) return false;
            if (token.Equals("BY", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        static string Normalize(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            if (IsComment(token) || token[0] == '\'' || token[0] == '"') return token;
            if (UppercaseKeywords.Contains(token)) return token.ToUpperInvariant();
            return token;
        }

        static bool IsComment(string token)
        {
            return token.StartsWith("--", StringComparison.Ordinal)
                || token.StartsWith("/*", StringComparison.Ordinal);
        }

        static bool EndsWithNewline(StringBuilder sb)
        {
            return sb.Length > 0 && (sb[sb.Length - 1] == '\n' || sb[sb.Length - 1] == '\r');
        }

        static void AppendWithSpacing(StringBuilder sb, string previous, string token)
        {
            if (sb.Length == 0 || EndsWithNewline(sb) || EndsWithIndent(sb))
            {
                sb.Append(token);
                return;
            }

            if (token == "::" || previous == "::") { sb.Append(token); return; }
            if (token == "," || token == ")" || token == ";" || token == ".") { sb.Append(token); return; }
            if (previous == "(" || previous == ".") { sb.Append(token); return; }
            if (token == "(" && IsIdentifier(previous)) { sb.Append(token); return; }

            sb.Append(' ').Append(token);
        }

        static bool EndsWithIndent(StringBuilder sb)
        {
            return sb.Length >= 4 && sb[sb.Length - 1] == ' ' && sb[sb.Length - 2] == ' ';
        }

        static bool IsIdentifier(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            var c = token[0];
            return char.IsLetter(c) || c == '_' || c == '"';
        }

        static List<string> Tokenize(string sql)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(sql)) return tokens;

            var i = 0;
            while (i < sql.Length)
            {
                var c = sql[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                {
                    var start = i;
                    i += 2;
                    while (i < sql.Length && sql[i] != '\n' && sql[i] != '\r') i++;
                    tokens.Add(sql.Substring(start, i - start));
                    continue;
                }

                if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    var start = i;
                    i += 2;
                    while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                    if (i + 1 < sql.Length) i += 2;
                    tokens.Add(sql.Substring(start, Math.Min(i, sql.Length) - start));
                    continue;
                }

                if (c == '\'')
                {
                    tokens.Add(ReadQuoted(sql, ref i, '\''));
                    continue;
                }

                if (c == '"')
                {
                    tokens.Add(ReadQuoted(sql, ref i, '"'));
                    continue;
                }

                string two = null;
                if (i + 1 < sql.Length) two = sql.Substring(i, 2);
                if (two == "::" || two == "!=" || two == "<>" || two == "<=" || two == ">=")
                {
                    tokens.Add(two);
                    i += 2;
                    continue;
                }

                if (c == '=' || c == '<' || c == '>' || c == '+' || c == '-' || c == '*' ||
                    c == '/' || c == '(' || c == ')' || c == ',' || c == ';' || c == '.')
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                if (char.IsDigit(c))
                {
                    var start = i;
                    i++;
                    while (i < sql.Length && (char.IsDigit(sql[i]) || sql[i] == '.')) i++;
                    if (i + 1 < sql.Length && sql[i] == 'm' && sql[i + 1] == 's') i += 2;
                    else if (i < sql.Length && "usmhdw".IndexOf(sql[i]) >= 0) i++;
                    tokens.Add(sql.Substring(start, i - start));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    var start = i;
                    i++;
                    while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_')) i++;
                    tokens.Add(sql.Substring(start, i - start));
                    continue;
                }

                tokens.Add(c.ToString());
                i++;
            }

            return tokens;
        }

        static string ReadQuoted(string sql, ref int i, char quote)
        {
            var start = i;
            i++;
            while (i < sql.Length)
            {
                if (sql[i] == quote)
                {
                    if (quote == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    i++;
                    break;
                }

                i++;
            }

            return sql.Substring(start, i - start);
        }
    }
}
