using System;
using System.Collections.Generic;
using System.Text;

namespace Devian
{
    /// <summary>
    /// DFF(Devian Friendly Format) 문자열을 DffValue로 파싱.
    /// 
    /// 지원 문법:
    /// - Empty/Unset: 빈 문자열, null, NULL, ~, - (단독)
    /// - Scalar: 일반 문자열
    /// - List: a,b,c 또는 [a,b,c] 또는 {a,b,c}
    /// - Object: id=1; name=A (pair-list)
    /// 
    /// 배열 리터럴 (모두 동일 의미):
    /// - a,b,c
    /// - [a,b,c]
    /// - {a,b,c}
    /// 
    /// 구분자:
    /// - Object item separator: ;
    /// - Object kv separator: =
    /// - List separator: ,
    /// 
    /// 따옴표: "..." 또는 '...'
    /// 이스케이프: \, \; \= \: \[ \] \{ \} \\
    /// 
    /// 주의: 이 파서는 "문법"만 처리한다.
    /// 타입 기반 문법 강제는 DffConverter에서 수행한다.
    /// </summary>
    public static class DffParser
    {
        /// <summary>
        /// DFF 문자열을 DffValue로 파싱.
        /// 타입 힌트 없이 구조만 파싱한다.
        /// </summary>
        /// <param name="text">DFF 문자열</param>
        /// <param name="options">파싱 옵션 (null이면 기본 옵션)</param>
        /// <returns>파싱된 DffValue</returns>
        public static DffValue Parse(string? text, DffOptions? options = null)
        {
            options ??= DffOptions.Default;

            // 빈 값 / Unset 처리
            if (string.IsNullOrWhiteSpace(text))
                return DffValue.Unset;

            var trimmed = text.Trim();

            // Unset 키워드
            if (IsUnsetKeyword(trimmed))
                return DffValue.Unset;

            // JSON format 감지 (옵션에 따라)
            if (options.AllowJsonFormat && IsJsonFormat(trimmed))
            {
                return ParseJsonFormat(trimmed);
            }

            // [...] 배열 리터럴
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                return ParseBracketList(trimmed, options);
            }

            // {...} 배열 리터럴 (scalar/enum 배열 전용)
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return ParseBraceList(trimmed, options);
            }

            // k=v; a=b 형식 (Object)
            if (ContainsUnquoted(trimmed, '='))
            {
                return ParseObject(trimmed, options);
            }

            // a,b,c 형식 (List)
            if (ContainsUnquoted(trimmed, ','))
            {
                return ParseSimpleList(trimmed, options);
            }

            // Scalar
            return DffValue.FromScalar(Unquote(trimmed));
        }

        /// <summary>
        /// Scalar 값을 직접 파싱 (타입 힌트 없음)
        /// </summary>
        public static DffValue ParseScalar(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return DffValue.Unset;

            var trimmed = text.Trim();
            if (IsUnsetKeyword(trimmed))
                return DffValue.Unset;

            return DffValue.FromScalar(Unquote(trimmed));
        }

        /// <summary>
        /// List 값을 직접 파싱 ([...] 또는 {...} 또는 a,b,c)
        /// </summary>
        public static DffValue ParseList(string? text, DffOptions? options = null)
        {
            options ??= DffOptions.Default;

            if (string.IsNullOrWhiteSpace(text))
                return DffValue.FromList(Array.Empty<DffValue>());

            var trimmed = text.Trim();

            // [...] 형식 처리
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                return ParseBracketList(trimmed, options);
            }

            // {...} 형식 처리
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return ParseBraceList(trimmed, options);
            }

            // a,b,c 형식
            return ParseSimpleList(trimmed, options);
        }

        /// <summary>
        /// [...] 배열 파싱
        /// </summary>
        public static DffValue ParseBracketList(string text, DffOptions? options = null)
        {
            options ??= DffOptions.Default;
            var trimmed = text.Trim();

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            if (string.IsNullOrWhiteSpace(trimmed))
                return DffValue.FromList(Array.Empty<DffValue>());

            return ParseSimpleList(trimmed, options);
        }

        /// <summary>
        /// {...} 배열 파싱 (scalar/enum 배열 전용)
        /// </summary>
        public static DffValue ParseBraceList(string text, DffOptions? options = null)
        {
            options ??= DffOptions.Default;
            var trimmed = text.Trim();

            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            if (string.IsNullOrWhiteSpace(trimmed))
                return DffValue.FromList(Array.Empty<DffValue>());

            // {...}는 scalar/enum 배열 전용이므로 내부에 = 가 있으면 에러
            if (ContainsUnquoted(trimmed, '='))
            {
                throw new DffParseException("Brace list {...} is for scalar/enum arrays only. Use [...] for object arrays.");
            }

            return ParseSimpleList(trimmed, options);
        }

        /// <summary>
        /// Object 값을 직접 파싱
        /// </summary>
        public static DffValue ParseObject(string? text, DffOptions? options = null)
        {
            options ??= DffOptions.Default;

            if (string.IsNullOrWhiteSpace(text))
                return DffValue.FromObject(new Dictionary<string, DffValue>());

            var trimmed = text.Trim();

            var fields = new Dictionary<string, DffValue>(StringComparer.Ordinal);
            var pairs = SplitByDelimiter(trimmed, ';');

            foreach (var pair in pairs)
            {
                var pairTrimmed = pair.Trim();
                if (string.IsNullOrEmpty(pairTrimmed))
                    continue;

                var eqIndex = FindUnquotedChar(pairTrimmed, '=');
                if (eqIndex < 0)
                {
                    throw new DffParseException($"Invalid object pair (missing '='): {pairTrimmed}");
                }

                var key = Unquote(pairTrimmed.Substring(0, eqIndex).Trim());
                var valueStr = pairTrimmed.Substring(eqIndex + 1).Trim();

                if (string.IsNullOrEmpty(key))
                {
                    throw new DffParseException($"Empty key in object pair: {pairTrimmed}");
                }

                var value = Parse(valueStr, options);
                fields[key] = value;
            }

            return DffValue.FromObject(fields);
        }

        #region Private Helpers

        private static bool IsUnsetKeyword(string s)
        {
            return s == "-" ||
                   s.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                   s == "~";
        }

        private static bool IsJsonFormat(string s)
        {
            // JSON object는 {...} + 내부에 : 가 있어야 함
            if (s.StartsWith("{") && s.EndsWith("}") && s.Contains(":"))
            {
                return true;
            }
            // JSON array of objects
            if (s.StartsWith("[") && s.Contains("{"))
            {
                return true;
            }
            return false;
        }

        private static DffValue ParseJsonFormat(string json)
        {
            throw new DffParseException("JSON format parsing requires AllowJsonFormat option and is not recommended. Use DFF pair-list format instead.");
        }

        private static DffValue ParseSimpleList(string text, DffOptions options)
        {
            var items = new List<DffValue>();
            var parts = SplitByDelimiter(text, ',');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // 리스트 항목 내에 object가 있을 수 있음
                var item = Parse(trimmed, options);
                items.Add(item);
            }

            return DffValue.FromList(items);
        }

        private static bool ContainsUnquoted(string s, char c)
        {
            return FindUnquotedChar(s, c) >= 0;
        }

        private static int FindUnquotedChar(string s, char target)
        {
            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                // 이스케이프 처리
                if (c == '\\' && i + 1 < s.Length)
                {
                    i++; // 다음 문자 스킵
                    continue;
                }

                // 따옴표 처리
                if (!inQuote && (c == '"' || c == '\''))
                {
                    inQuote = true;
                    quoteChar = c;
                    continue;
                }

                if (inQuote && c == quoteChar)
                {
                    inQuote = false;
                    quoteChar = '\0';
                    continue;
                }

                if (inQuote)
                    continue;

                // 중첩 구조 처리
                if (c == '[' || c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == ']' || c == '}')
                {
                    depth--;
                    continue;
                }

                // 대상 문자 찾기
                if (depth == 0 && c == target)
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<string> SplitByDelimiter(string s, char delimiter)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                // 이스케이프 처리
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (IsEscapableChar(next))
                    {
                        current.Append(next);
                        i++;
                        continue;
                    }
                }

                // 따옴표 처리
                if (!inQuote && (c == '"' || c == '\''))
                {
                    inQuote = true;
                    quoteChar = c;
                    current.Append(c);
                    continue;
                }

                if (inQuote && c == quoteChar)
                {
                    inQuote = false;
                    quoteChar = '\0';
                    current.Append(c);
                    continue;
                }

                if (inQuote)
                {
                    current.Append(c);
                    continue;
                }

                // 중첩 구조 처리
                if (c == '[' || c == '{')
                {
                    depth++;
                    current.Append(c);
                    continue;
                }

                if (c == ']' || c == '}')
                {
                    depth--;
                    current.Append(c);
                    continue;
                }

                // 구분자 처리
                if (depth == 0 && c == delimiter)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            // 마지막 항목
            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        private static bool IsEscapableChar(char c)
        {
            return c == ',' || c == ';' || c == '=' || c == ':' ||
                   c == '[' || c == ']' || c == '{' || c == '}' ||
                   c == '\\' || c == '"' || c == '\'';
        }

        private static string Unquote(string s)
        {
            if (s.Length >= 2)
            {
                if ((s.StartsWith("\"") && s.EndsWith("\"")) ||
                    (s.StartsWith("'") && s.EndsWith("'")))
                {
                    return UnescapeString(s.Substring(1, s.Length - 2));
                }
            }
            return UnescapeString(s);
        }

        private static string UnescapeString(string s)
        {
            if (!s.Contains("\\"))
                return s;

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (IsEscapableChar(next))
                    {
                        sb.Append(next);
                        i++;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// DFF 파싱 예외
    /// </summary>
    public class DffParseException : Exception
    {
        public DffParseException(string message) : base(message) { }
        public DffParseException(string message, Exception inner) : base(message, inner) { }
    }
}
