using System;
using System.Collections.Generic;

namespace Devian.Protobuf
{
    /// <summary>
    /// DFF 타입 카테고리
    /// </summary>
    public enum DffTypeCategory
    {
        /// <summary>Scalar 단일 (int, string, float 등)</summary>
        Scalar,
        /// <summary>Scalar 배열 (int[], string[] 등)</summary>
        ScalarArray,
        /// <summary>Enum 단일</summary>
        Enum,
        /// <summary>Enum 배열</summary>
        EnumArray,
        /// <summary>Class(Message) 단일</summary>
        Class,
        /// <summary>Class(Message) 배열</summary>
        ClassArray
    }

    /// <summary>
    /// DFF 타입 기반 변환기.
    /// 
    /// 역할:
    /// - Row2 타입(enum:{Name}, class:{Name}, scalar 등)을 보고 허용 문법을 결정
    /// - 입력 문자열을 DffValue로 정규화
    /// - 타입에 맞지 않는 문법 사용 시 에러
    /// 
    /// 허용 문법 (타입별):
    /// 
    /// A) Scalar 단일
    ///    - 허용: value
    ///    - 금지: {...}, [...], a,b,c
    ///    - 정규화: Scalar("value")
    /// 
    /// B) Scalar[] / Enum[] (배열)
    ///    - 허용: a,b,c / {a,b,c} / [a,b,c] (모두 동일)
    ///    - 정규화: List([Scalar("a"), Scalar("b"), Scalar("c")])
    /// 
    /// C) Enum 단일
    ///    - 허용: RARE
    ///    - 금지: A,B / {A,B} / [A,B]
    ///    - 정규화: Scalar("RARE")
    /// 
    /// D) Class(Message) 단일
    ///    - 허용: k=v; a=b (pair-list)
    ///    - 금지: {...}, [...]
    ///    - 정규화: Object({k:Scalar(v), a:Scalar(b)})
    /// 
    /// E) Class(Message) 배열
    ///    - 허용: [k=v; a=b, k=v; a=b]
    ///    - 금지: {...}
    ///    - 정규화: List([Object(...), Object(...)])
    /// 
    /// 주의: 이 클래스는 "문법 강제 + 정규화"만 담당.
    /// Protobuf 타입 변환은 DffProtobufBuilder에서 수행.
    /// </summary>
    public static class DffConverter
    {
        /// <summary>
        /// Row2 타입 문자열에서 타입 카테고리 결정
        /// </summary>
        /// <param name="row2Type">Row2 타입 문자열 (예: "int", "enum:UserType", "class:UserProfile[]")</param>
        /// <returns>타입 카테고리</returns>
        public static DffTypeCategory GetTypeCategory(string row2Type)
        {
            if (string.IsNullOrWhiteSpace(row2Type))
                throw new DffConvertException("Row2 type cannot be empty");

            var trimmed = row2Type.Trim();
            var isArray = trimmed.EndsWith("[]");
            var baseType = isArray ? trimmed[..^2] : trimmed;

            // ref: 금지 (Strict Mode)
            if (baseType.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
            {
                throw new DffConvertException($"ref: is deprecated; use enum:{{X}} or class:{{Y}}. Found: '{row2Type}'");
            }

            // enum:{Name}
            if (baseType.StartsWith("enum:", StringComparison.OrdinalIgnoreCase))
            {
                return isArray ? DffTypeCategory.EnumArray : DffTypeCategory.Enum;
            }

            // class:{Name}
            if (baseType.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
            {
                return isArray ? DffTypeCategory.ClassArray : DffTypeCategory.Class;
            }

            // Scalar
            return isArray ? DffTypeCategory.ScalarArray : DffTypeCategory.Scalar;
        }

        /// <summary>
        /// 셀 문자열을 타입에 맞게 정규화된 DffValue로 변환
        /// </summary>
        /// <param name="raw">셀 원본 문자열</param>
        /// <param name="row2Type">Row2 타입 문자열</param>
        /// <param name="options">DFF 옵션</param>
        /// <returns>정규화된 DffValue</returns>
        public static DffValue Normalize(string? raw, string row2Type, DffOptions? options = null)
        {
            options ??= DffOptions.Default;

            // 빈 값 처리
            if (string.IsNullOrWhiteSpace(raw))
                return DffValue.Unset;

            var trimmed = raw.Trim();
            if (IsUnsetKeyword(trimmed))
                return DffValue.Unset;

            var category = GetTypeCategory(row2Type);

            return category switch
            {
                DffTypeCategory.Scalar => NormalizeScalar(trimmed, options),
                DffTypeCategory.ScalarArray => NormalizeScalarArray(trimmed, options),
                DffTypeCategory.Enum => NormalizeEnum(trimmed, options),
                DffTypeCategory.EnumArray => NormalizeEnumArray(trimmed, options),
                DffTypeCategory.Class => NormalizeClass(trimmed, options),
                DffTypeCategory.ClassArray => NormalizeClassArray(trimmed, options),
                _ => throw new DffConvertException($"Unknown type category: {category}")
            };
        }

        /// <summary>
        /// 타입 카테고리로 직접 정규화
        /// </summary>
        public static DffValue Normalize(string? raw, DffTypeCategory category, DffOptions? options = null)
        {
            options ??= DffOptions.Default;

            if (string.IsNullOrWhiteSpace(raw))
                return DffValue.Unset;

            var trimmed = raw.Trim();
            if (IsUnsetKeyword(trimmed))
                return DffValue.Unset;

            return category switch
            {
                DffTypeCategory.Scalar => NormalizeScalar(trimmed, options),
                DffTypeCategory.ScalarArray => NormalizeScalarArray(trimmed, options),
                DffTypeCategory.Enum => NormalizeEnum(trimmed, options),
                DffTypeCategory.EnumArray => NormalizeEnumArray(trimmed, options),
                DffTypeCategory.Class => NormalizeClass(trimmed, options),
                DffTypeCategory.ClassArray => NormalizeClassArray(trimmed, options),
                _ => throw new DffConvertException($"Unknown type category: {category}")
            };
        }

        #region Scalar

        /// <summary>
        /// Scalar 단일 정규화
        /// 금지: {...}, [...], a,b,c (쉼표 포함)
        /// </summary>
        private static DffValue NormalizeScalar(string raw, DffOptions options)
        {
            // 배열 리터럴 금지
            if (raw.StartsWith("[") || raw.StartsWith("{"))
            {
                throw new DffConvertException($"Scalar type does not allow array literals. Found: '{raw}'");
            }

            // 쉼표 포함 금지 (따옴표 밖)
            if (ContainsUnquoted(raw, ','))
            {
                throw new DffConvertException($"Scalar type does not allow comma-separated values. Use array type or quote the value. Found: '{raw}'");
            }

            return DffParser.ParseScalar(raw);
        }

        /// <summary>
        /// Scalar 배열 정규화
        /// 허용: a,b,c / {a,b,c} / [a,b,c]
        /// </summary>
        private static DffValue NormalizeScalarArray(string raw, DffOptions options)
        {
            return DffParser.ParseList(raw, options);
        }

        #endregion

        #region Enum

        /// <summary>
        /// Enum 단일 정규화
        /// 금지: A,B / {A,B} / [A,B]
        /// </summary>
        private static DffValue NormalizeEnum(string raw, DffOptions options)
        {
            // 배열 리터럴 금지
            if (raw.StartsWith("[") || raw.StartsWith("{"))
            {
                throw new DffConvertException($"Enum type does not allow array literals. Use enum:{{Name}}[] for arrays. Found: '{raw}'");
            }

            // 쉼표 포함 금지 (따옴표 밖)
            if (ContainsUnquoted(raw, ','))
            {
                throw new DffConvertException($"Enum type does not allow comma-separated values. Use enum:{{Name}}[] for arrays. Found: '{raw}'");
            }

            return DffParser.ParseScalar(raw);
        }

        /// <summary>
        /// Enum 배열 정규화
        /// 허용: A,B,C / {A,B,C} / [A,B,C]
        /// </summary>
        private static DffValue NormalizeEnumArray(string raw, DffOptions options)
        {
            return DffParser.ParseList(raw, options);
        }

        #endregion

        #region Class (Message)

        /// <summary>
        /// Class 단일 정규화
        /// 허용: k=v; a=b (pair-list)
        /// 금지: {...}, [...]
        /// 
        /// 규칙: {} 는 scalar/enum 배열 전용이므로 class에서 금지
        /// </summary>
        private static DffValue NormalizeClass(string raw, DffOptions options)
        {
            // {...} 금지 (배열 표기 전용)
            if (raw.StartsWith("{"))
            {
                throw new DffConvertException($"Class type does not allow brace literals. Use pair-list format: k=v; a=b. Found: '{raw}'");
            }

            // [...] 금지 (class 배열에서만 허용)
            if (raw.StartsWith("["))
            {
                throw new DffConvertException($"Class type does not allow bracket literals. Use class:{{Name}}[] for arrays. Found: '{raw}'");
            }

            // = 가 없으면 에러 (빈 object 허용 안함)
            if (!ContainsUnquoted(raw, '='))
            {
                throw new DffConvertException($"Class type requires pair-list format: k=v; a=b. Found: '{raw}'");
            }

            return DffParser.ParseObject(raw, options);
        }

        /// <summary>
        /// Class 배열 정규화
        /// 허용: [k=v; a=b, k=v; a=b]
        /// 금지: {...}
        /// </summary>
        private static DffValue NormalizeClassArray(string raw, DffOptions options)
        {
            // {...} 금지 (class에서는 금지)
            if (raw.StartsWith("{"))
            {
                throw new DffConvertException($"Class array does not allow brace literals. Use bracket format: [k=v; a=b, k=v; a=b]. Found: '{raw}'");
            }

            // [...] 형식이 아니면 에러
            if (!raw.StartsWith("["))
            {
                throw new DffConvertException($"Class array requires bracket format: [k=v; a=b, k=v; a=b]. Found: '{raw}'");
            }

            // [...] 내부 파싱
            var inner = raw.Substring(1, raw.Length - 2).Trim();
            if (string.IsNullOrWhiteSpace(inner))
            {
                return DffValue.FromList(Array.Empty<DffValue>());
            }

            // 콤마로 분리 후 각 항목을 Object로 파싱
            var items = new List<DffValue>();
            var parts = SplitListItems(inner);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // 각 항목은 pair-list여야 함
                if (!ContainsUnquoted(trimmed, '='))
                {
                    throw new DffConvertException($"Class array item must be pair-list format: k=v; a=b. Found: '{trimmed}'");
                }

                var obj = DffParser.ParseObject(trimmed, options);
                items.Add(obj);
            }

            return DffValue.FromList(items);
        }

        #endregion

        #region Helpers

        private static bool IsUnsetKeyword(string s)
        {
            return s == "-" ||
                   s.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                   s == "~";
        }

        private static bool ContainsUnquoted(string s, char target)
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
                    i++;
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
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 리스트 항목 분리 (콤마 기준, 중첩 구조 고려)
        /// </summary>
        private static List<string> SplitListItems(string s)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                // 이스케이프 처리
                if (c == '\\' && i + 1 < s.Length)
                {
                    current.Append(c);
                    current.Append(s[i + 1]);
                    i++;
                    continue;
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

                // 콤마 처리
                if (depth == 0 && c == ',')
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

        #endregion
    }

    /// <summary>
    /// DFF 변환 예외
    /// </summary>
    public class DffConvertException : Exception
    {
        public DffConvertException(string message) : base(message) { }
        public DffConvertException(string message, Exception inner) : base(message, inner) { }
    }
}
