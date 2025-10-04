using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoTranslate
{
    public static class TextProcessor
    {
        private static StringBuilder escapeBuilder = new StringBuilder(1024);
        private static StringBuilder unescapeBuilder = new StringBuilder(1024);

        public static bool StartsWithString(string source, int startIndex, string pattern)
        {
            return string.CompareOrdinal(source, startIndex, pattern, 0, pattern.Length) == 0;
        }

        public static bool HasNonWhitespace(string str, int start, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (!char.IsWhiteSpace(str[start + i]))
                    return true;
            }
            return false;
        }

        public static bool IsChineseChar(char c) =>
            (c >= '\u4e00' && c <= '\u9fa5') ||
            (c >= '\u3000' && c <= '\u303F') ||
            (c >= '\uFF00' && c <= '\uFFEF');

        public static bool IsNullOrWhiteSpace(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (var ch in s)
                if (!char.IsWhiteSpace(ch))
                    return false;
            return true;
        }

        public static int IndexOfChar(string source, char value, int startIndex = 0)
        {
            return source.IndexOf(value, startIndex);
        }

        public static int IndexOfString(string source, string value, int startIndex = 0)
        {
            return source.IndexOf(value, startIndex, StringComparison.Ordinal);
        }

        public static int IndexOfString(StringBuilder sb, string sub, int startIndex = 0)
        {
            if (sb == null || sub == null)
                return -1;

            int sbLen = sb.Length;
            int subLen = sub.Length;

            if (subLen == 0 || subLen > sbLen || startIndex < 0 || startIndex > sbLen - subLen)
                return -1;

            for (int i = startIndex; i <= sbLen - subLen; i++)
            {
                bool matched = true;

                for (int j = 0; j < subLen; j++)
                {
                    if (sb[i + j] != sub[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                    return i;
            }

            return -1;
        }

        public static string TrimOnlyIfNeeded(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            if (char.IsWhiteSpace(str[0]) || char.IsWhiteSpace(str[str.Length - 1]))
                return str.Trim();

            return str;
        }

        public static string UnescapeJsonString(string s)
        {
            unescapeBuilder.Length = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[++i];
                    switch (next)
                    {
                        case '"': unescapeBuilder.Append('\"'); break;
                        case '\\': unescapeBuilder.Append('\\'); break;
                        case '/': unescapeBuilder.Append('/'); break;
                        case 'b': unescapeBuilder.Append('\b'); break;
                        case 'f': unescapeBuilder.Append('\f'); break;
                        case 'n': unescapeBuilder.Append('\n'); break;
                        case 'r': unescapeBuilder.Append('\r'); break;
                        case 't': unescapeBuilder.Append('\t'); break;
                        case 'u':
                            if (i + 4 < s.Length)
                            {
                                string hex = s.Substring(i + 1, 4);
                                int code = int.Parse(hex, NumberStyles.HexNumber);
                                unescapeBuilder.Append((char)code);
                                i += 4;
                            }
                            break;
                        default:
                            unescapeBuilder.Append(next);
                            break;
                    }
                }
                else
                {
                    unescapeBuilder.Append(c);
                }
            }
            return unescapeBuilder.ToString();
        }

        public static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            escapeBuilder.Length = 0;

            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': escapeBuilder.Append("\\\\"); break;
                    case '\"': escapeBuilder.Append("\\\""); break;
                    case '\b': escapeBuilder.Append("\\b"); break;
                    case '\f': escapeBuilder.Append("\\f"); break;
                    case '\n': escapeBuilder.Append("\\n"); break;
                    case '\r': escapeBuilder.Append("\\r"); break;
                    case '\t': escapeBuilder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            escapeBuilder.AppendFormat("\\u{0:X4}", (int)c);
                        }
                        else
                        {
                            escapeBuilder.Append(c);
                        }
                        break;
                }
            }

            return escapeBuilder.ToString();
        }

        public static void AppendUnescapeJsonString(string s, StringBuilder builder)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[++i];
                    switch (next)
                    {
                        case '"': builder.Append('\"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (i + 4 < s.Length)
                            {
                                string hex = s.Substring(i + 1, 4);
                                int code = int.Parse(hex, NumberStyles.HexNumber);
                                builder.Append((char)code);
                                i += 4;
                            }
                            break;
                        default:
                            builder.Append(next);
                            break;
                    }
                }
                else
                {
                    builder.Append(c);
                }
            }
        }

        public static void AppendEscapeJsonString(string value, StringBuilder builder)
        {
            if (string.IsNullOrEmpty(value)) return;

            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '\"': builder.Append("\\\""); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            builder.AppendFormat("\\u{0:X4}", (int)c);
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
        }
    }
}
