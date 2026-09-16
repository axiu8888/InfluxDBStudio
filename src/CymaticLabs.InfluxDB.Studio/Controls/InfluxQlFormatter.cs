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
        /// <summary>
        /// Parenthetical groups shorter than this stay on one line (including inner AND/OR).
        /// Longer groups break across lines with indented content.
        /// </summary>
        const int MaxInlineParenLength = 72;

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
            var expandParen = pretty ? ComputeExpandParen(tokens) : null;
            var sb = new StringBuilder();
            string previous = null;
            var parenDepth = 0;
            // At each paren depth, whether the opening group was expanded.
            var expandStack = new Stack<bool>();

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

                if (pretty && token == "(")
                {
                    var expand = expandParen != null && expandParen[i];
                    AppendOpenParen(sb, previous, expand);
                    parenDepth++;
                    expandStack.Push(expand);
                    previous = token;
                    continue;
                }

                if (pretty && token == ")")
                {
                    var expand = expandStack.Count > 0 && expandStack.Pop();
                    parenDepth = Math.Max(0, parenDepth - 1);
                    if (expand)
                    {
                        if (!EndsWithNewline(sb)) sb.AppendLine();
                        AppendIndent(sb, parenDepth == 0 ? 1 : parenDepth + 1);
                    }
                    sb.Append(")");
                    previous = token;
                    continue;
                }

                if (pretty && (upper == "AND" || upper == "OR"))
                {
                    var insideExpand = expandStack.Count > 0 && expandStack.Peek();
                    var breakLine = parenDepth == 0 || insideExpand;

                    if (breakLine)
                    {
                        if (sb.Length > 0 && !EndsWithNewline(sb)) sb.AppendLine();
                        // Top-level WHERE predicates: 4 spaces.
                        // Inside expanded (...): one more indent level than the open paren line.
                        AppendIndent(sb, parenDepth == 0 ? 1 : parenDepth + 1);
                        sb.Append(upper);
                    }
                    else
                    {
                        AppendWithSpacing(sb, previous, upper);
                    }

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

                if (pretty && expandStack.Count > 0 && expandStack.Peek()
                    && EndsWithNewline(sb) && token != ")")
                {
                    // First token after an expanded "(" — already indented by AppendOpenParen.
                    AppendIndent(sb, parenDepth + 1);
                }

                AppendWithSpacing(sb, previous, token);
                previous = token;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Marks each "(" token index that should break across multiple lines.
        /// Computed right-to-left so nested groups are resolved first.
        /// </summary>
        static bool[] ComputeExpandParen(IList<string> tokens)
        {
            var expand = new bool[tokens.Count];
            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i] != "(") continue;
                expand[i] = ShouldExpandParen(tokens, i, expand);
            }
            return expand;
        }

        static bool ShouldExpandParen(IList<string> tokens, int openIndex, bool[] expand)
        {
            var closeIndex = FindMatchingClose(tokens, openIndex);
            if (closeIndex < 0) return false;

            // Nested expanded groups force the parent to expand.
            for (var i = openIndex + 1; i < closeIndex; i++)
            {
                if (tokens[i] == "(" && expand[i]) return true;
            }

            // Function calls like now() / count(x) stay inline when short and have no AND/OR.
            var hasBoolOp = false;
            for (var i = openIndex + 1; i < closeIndex; i++)
            {
                var t = tokens[i];
                if (t.Equals("AND", StringComparison.OrdinalIgnoreCase)
                    || t.Equals("OR", StringComparison.OrdinalIgnoreCase))
                {
                    hasBoolOp = true;
                    break;
                }
            }

            var compact = CompactRange(tokens, openIndex, closeIndex);
            if (compact.Length > MaxInlineParenLength) return true;

            // Short boolean groups stay on one line: (a = 1 OR b = 2)
            // Longer boolean groups expand even if under the hard limit when they look heavy.
            if (hasBoolOp && compact.Length > 48) return true;

            return false;
        }

        static int FindMatchingClose(IList<string> tokens, int openIndex)
        {
            var depth = 0;
            for (var i = openIndex; i < tokens.Count; i++)
            {
                if (tokens[i] == "(") depth++;
                else if (tokens[i] == ")")
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        static string CompactRange(IList<string> tokens, int start, int endInclusive)
        {
            var sb = new StringBuilder();
            string previous = null;
            for (var i = start; i <= endInclusive; i++)
            {
                var token = Normalize(tokens[i]);
                AppendWithSpacing(sb, previous, token);
                previous = token;
            }
            return sb.ToString();
        }

        static void AppendOpenParen(StringBuilder sb, string previous, bool expand)
        {
            // Space before "(" unless it is a function call: name(...)
            var functionCall = IsFunctionName(previous);
            if (sb.Length > 0 && !EndsWithNewline(sb) && !functionCall)
                sb.Append(' ');
            else if (sb.Length > 0 && !EndsWithNewline(sb) && functionCall)
            {
                // no space
            }

            sb.Append('(');
            if (expand)
                sb.AppendLine();
        }

        static void AppendIndent(StringBuilder sb, int levels)
        {
            if (levels < 1) levels = 1;
            for (var i = 0; i < levels; i++)
                sb.Append("    ");
        }

        static bool IsFunctionName(string previous)
        {
            if (string.IsNullOrEmpty(previous)) return false;
            if (UppercaseKeywords.Contains(previous)) return false;
            return IsIdentifier(previous);
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

            // Function call: identifier(  — no space. Keyword (: space was handled in AppendOpenParen for pretty.
            if (token == "(" && IsFunctionName(previous)) { sb.Append(token); return; }
            if (token == "(") { sb.Append(' ').Append(token); return; }

            sb.Append(' ').Append(token);
        }

        static bool EndsWithIndent(StringBuilder sb)
        {
            return sb.Length >= 4 && sb[sb.Length - 1] == ' ' && sb[sb.Length - 2] == ' '
                && sb[sb.Length - 3] == ' ' && sb[sb.Length - 4] == ' ';
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
