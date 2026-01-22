using System;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Devian.Protobuf
{
    /// <summary>
    /// DFF(Devian Friendly Format) 처리를 위한 Public API.
    /// 
    /// Devian의 입력 포맷 정책:
    /// - Input(작성 포맷): DFF
    /// - Internal(정본 IR): Protobuf IMessage
    /// - Debug Output: ProtoJSON (Google.Protobuf.JsonFormatter)
    /// 
    /// 변환 단계:
    /// 1) 셀 문자열 → DffConverter.Normalize() → DffValue (타입 기반 정규화)
    /// 2) DffValue → DffProtobufBuilder.Build() → IMessage (Descriptor 기반 구성)
    /// 
    /// 주의: Excel/JSON 작성자는 ProtoJSON을 직접 작성하지 않는다.
    /// ProtoJSON은 디버그 출력 목적으로만 사용한다.
    /// </summary>
    public static class DffProtobuf
    {
        /// <summary>
        /// Excel 셀 문자열을 Row2 타입 기반으로 IMessage로 변환.
        /// 
        /// 변환 단계:
        /// 1) DffConverter.Normalize(raw, row2Type) → DffValue (타입 기반 문법 강제)
        /// 2) DffProtobufBuilder.BuildMessage(desc, dffValue) → IMessage
        /// 
        /// 타입별 허용 문법:
        /// - Scalar: value (배열 금지)
        /// - Scalar[]: a,b,c / {a,b,c} / [a,b,c]
        /// - Enum: RARE (배열 금지)
        /// - Enum[]: A,B,C / {A,B,C} / [A,B,C]
        /// - Class: k=v; a=b ({...} 금지)
        /// - Class[]: [k=v; a=b, k=v; a=b] ({...} 금지)
        /// </summary>
        /// <param name="raw">셀 원본 문자열</param>
        /// <param name="row2Type">Row2 타입 (예: "int", "enum:UserType", "class:UserProfile[]")</param>
        /// <param name="descriptor">대상 message의 Descriptor</param>
        /// <param name="options">변환 옵션</param>
        /// <returns>생성된 IMessage</returns>
        public static IMessage ParseCell(string? raw, string row2Type, MessageDescriptor descriptor, DffOptions? options = null)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            options ??= DffOptions.Default;

            // 1) 셀 문자열 → DffValue (타입 기반 정규화)
            var dffValue = DffConverter.Normalize(raw, row2Type, options);

            // 2) DffValue → IMessage (Descriptor 기반)
            return DffProtobufBuilder.BuildMessage(descriptor, dffValue, options);
        }

        /// <summary>
        /// Excel 셀 문자열을 Row2 타입 기반으로 DffValue로 정규화.
        /// IMessage 변환 없이 정규화된 DffValue만 얻을 때 사용.
        /// </summary>
        /// <param name="raw">셀 원본 문자열</param>
        /// <param name="row2Type">Row2 타입</param>
        /// <param name="options">변환 옵션</param>
        /// <returns>정규화된 DffValue</returns>
        public static DffValue NormalizeCell(string? raw, string row2Type, DffOptions? options = null)
        {
            return DffConverter.Normalize(raw, row2Type, options ?? DffOptions.Default);
        }

        /// <summary>
        /// DFF 문자열을 IMessage로 파싱 (타입 힌트 없음).
        /// 
        /// DFF 포맷 예시:
        /// - Scalar: "value"
        /// - List: "a, b, c" 또는 "[a, b, c]" 또는 "{a, b, c}"
        /// - Object: "id=1; name=Sword; tags=[rare, event]"
        /// 
        /// 주의: 이 메소드는 타입 기반 문법 강제 없이 파싱한다.
        /// Excel 셀 처리에는 ParseCell()을 사용하라.
        /// </summary>
        /// <param name="dffText">DFF 포맷 문자열</param>
        /// <param name="descriptor">대상 message의 Descriptor</param>
        /// <param name="options">파싱/변환 옵션 (null이면 기본 엄격 모드)</param>
        /// <returns>생성된 IMessage</returns>
        /// <exception cref="DffParseException">DFF 문법 오류</exception>
        /// <exception cref="DffBuildException">타입 변환/검증 오류</exception>
        public static IMessage ParseMessage(string? dffText, MessageDescriptor descriptor, DffOptions? options = null)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            options ??= DffOptions.Default;

            // 1) DFF 문자열 → DffValue (중간 표현)
            var dffValue = DffParser.Parse(dffText, options);

            // 2) DffValue → IMessage (Descriptor 기반)
            return DffProtobufBuilder.BuildMessage(descriptor, dffValue, options);
        }

        /// <summary>
        /// DFF 문자열을 지정된 타입의 IMessage로 파싱.
        /// </summary>
        /// <typeparam name="T">대상 message 타입</typeparam>
        /// <param name="dffText">DFF 포맷 문자열</param>
        /// <param name="options">파싱/변환 옵션</param>
        /// <returns>생성된 message</returns>
        public static T ParseMessage<T>(string? dffText, DffOptions? options = null) where T : IMessage<T>, new()
        {
            var descriptor = new T().Descriptor;
            return (T)ParseMessage(dffText, descriptor, options);
        }

        /// <summary>
        /// DFF 문자열을 DffValue (중간 표현)로 파싱.
        /// IMessage 변환 없이 구조만 파싱할 때 사용.
        /// </summary>
        /// <param name="dffText">DFF 포맷 문자열</param>
        /// <param name="options">파싱 옵션</param>
        /// <returns>파싱된 DffValue</returns>
        public static DffValue ParseValue(string? dffText, DffOptions? options = null)
        {
            return DffParser.Parse(dffText, options);
        }

        /// <summary>
        /// IMessage를 디버그용 ProtoJSON 문자열로 변환.
        /// 
        /// 주의: 이 출력은 디버그/검증 목적으로만 사용한다.
        /// 이 JSON을 다시 입력으로 사용하는 것은 권장하지 않는다.
        /// 입력은 항상 DFF 포맷을 사용해야 한다.
        /// </summary>
        /// <param name="message">변환할 message</param>
        /// <param name="includeDefaultValues">기본값 필드 포함 여부</param>
        /// <returns>ProtoJSON 문자열 (디버그용)</returns>
        public static string ToDebugJson(IMessage message, bool includeDefaultValues = false)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var settings = includeDefaultValues
                ? JsonFormatter.Settings.Default.WithFormatDefaultValues(true)
                : JsonFormatter.Settings.Default;

            var formatter = new JsonFormatter(settings);
            return formatter.Format(message);
        }

        /// <summary>
        /// IMessage를 들여쓰기된 디버그용 ProtoJSON으로 변환.
        /// </summary>
        /// <param name="message">변환할 message</param>
        /// <param name="includeDefaultValues">기본값 필드 포함 여부</param>
        /// <returns>들여쓰기된 ProtoJSON 문자열 (디버그용)</returns>
        public static string ToDebugJsonIndented(IMessage message, bool includeDefaultValues = false)
        {
            var json = ToDebugJson(message, includeDefaultValues);
            return IndentJson(json);
        }

        /// <summary>
        /// DffValue를 IMessage로 변환.
        /// 이미 파싱된 DffValue가 있을 때 사용.
        /// </summary>
        /// <param name="value">DffValue</param>
        /// <param name="descriptor">대상 message의 Descriptor</param>
        /// <param name="options">변환 옵션</param>
        /// <returns>생성된 IMessage</returns>
        public static IMessage BuildMessage(DffValue value, MessageDescriptor descriptor, DffOptions? options = null)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            return DffProtobufBuilder.BuildMessage(descriptor, value, options ?? DffOptions.Default);
        }

        #region Private Helpers

        private static string IndentJson(string json)
        {
            // 간단한 JSON 들여쓰기
            var sb = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;

            foreach (char c in json)
            {
                if (c == '"' && (sb.Length == 0 || sb[sb.Length - 1] != '\\'))
                {
                    inString = !inString;
                    sb.Append(c);
                    continue;
                }

                if (inString)
                {
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '{':
                    case '[':
                        sb.Append(c);
                        sb.AppendLine();
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        sb.AppendLine();
                        indent--;
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        sb.AppendLine();
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case ':':
                        sb.Append(c);
                        sb.Append(' ');
                        break;
                    default:
                        if (!char.IsWhiteSpace(c))
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
