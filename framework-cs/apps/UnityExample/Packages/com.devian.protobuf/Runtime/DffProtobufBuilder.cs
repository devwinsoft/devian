using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace Devian.Protobuf
{
    /// <summary>
    /// DffValue를 Proto Descriptor 기반으로 IMessage로 변환.
    /// 
    /// 책임:
    /// - Descriptor로 필드 존재 검증
    /// - Descriptor로 타입 결정 (scalar/enum/message/repeated/map/oneof)
    /// - 필드명 매칭 (jsonName, name, normalized)
    /// - 타입 변환 (bool, int, float, string, bytes, enum, message)
    /// - Well-known types 처리 (Timestamp, Duration)
    /// 
    /// 에러 정책 (기본):
    /// - Unknown key: 에러
    /// - 타입 변환 실패: 에러
    /// - 범위 초과: 에러
    /// - oneof 충돌: 에러
    /// </summary>
    public static class DffProtobufBuilder
    {
        /// <summary>
        /// DffValue를 IMessage로 변환.
        /// </summary>
        /// <param name="descriptor">Protobuf message descriptor</param>
        /// <param name="value">DffValue (Object 타입이어야 함)</param>
        /// <param name="options">변환 옵션</param>
        /// <returns>생성된 IMessage</returns>
        public static IMessage BuildMessage(MessageDescriptor descriptor, DffValue value, DffOptions? options = null)
        {
            options ??= DffOptions.Default;

            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            if (value == null || value.IsUnset)
            {
                // 빈 메시지 생성
                return CreateEmptyMessage(descriptor);
            }

            if (!value.IsObject)
            {
                throw new DffBuildException($"Expected object value for message '{descriptor.FullName}', got {value.ValueKind}");
            }

            var msg = CreateEmptyMessage(descriptor);
            var setOneofs = new HashSet<OneofDescriptor>();

            foreach (var kvp in value.ObjectValue!)
            {
                var fieldName = kvp.Key;
                var fieldValue = kvp.Value;

                // 필드 찾기
                var field = FindField(descriptor, fieldName);
                if (field == null)
                {
                    if (!options.AllowUnknownFields)
                    {
                        throw new DffBuildException($"Unknown field '{fieldName}' in message '{descriptor.FullName}'");
                    }
                    continue;
                }

                // oneof 충돌 검사
                if (field.ContainingOneof != null && options.StrictOneofValidation)
                {
                    if (setOneofs.Contains(field.ContainingOneof))
                    {
                        throw new DffBuildException($"Multiple values set for oneof '{field.ContainingOneof.Name}' in message '{descriptor.FullName}'");
                    }
                    setOneofs.Add(field.ContainingOneof);
                }

                // 값 설정
                SetFieldValue(msg, field, fieldValue, options);
            }

            return msg;
        }

        #region Field Resolution

        private static FieldDescriptor? FindField(MessageDescriptor descriptor, string name)
        {
            // 1) jsonName exact
            foreach (var field in descriptor.Fields.InDeclarationOrder())
            {
                if (field.JsonName == name)
                    return field;
            }

            // 2) name exact
            foreach (var field in descriptor.Fields.InDeclarationOrder())
            {
                if (field.Name == name)
                    return field;
            }

            // 3) normalized (case-insensitive, snake/camel conversion)
            var normalized = NormalizeName(name);
            foreach (var field in descriptor.Fields.InDeclarationOrder())
            {
                if (NormalizeName(field.JsonName) == normalized ||
                    NormalizeName(field.Name) == normalized)
                {
                    return field;
                }
            }

            return null;
        }

        private static string NormalizeName(string name)
        {
            // snake_case, camelCase, PascalCase를 모두 소문자로 정규화
            return Regex.Replace(name, "([a-z])([A-Z])", "$1_$2")
                       .Replace("_", "")
                       .ToLowerInvariant();
        }

        #endregion

        #region Field Value Setting

        private static void SetFieldValue(IMessage msg, FieldDescriptor field, DffValue value, DffOptions options)
        {
            if (value.IsUnset)
            {
                // 기본값 유지 (설정하지 않음)
                return;
            }

            if (field.IsMap)
            {
                SetMapFieldValue(msg, field, value, options);
            }
            else if (field.IsRepeated)
            {
                SetRepeatedFieldValue(msg, field, value, options);
            }
            else
            {
                SetSingularFieldValue(msg, field, value, options);
            }
        }

        private static void SetSingularFieldValue(IMessage msg, FieldDescriptor field, DffValue value, DffOptions options)
        {
            var converted = ConvertValue(field, value, options);
            field.Accessor.SetValue(msg, converted);
        }

        private static void SetRepeatedFieldValue(IMessage msg, FieldDescriptor field, DffValue value, DffOptions options)
        {
            var list = (IList)field.Accessor.GetValue(msg);

            IReadOnlyList<DffValue> items;
            if (value.IsList)
            {
                items = value.ListValue!;
            }
            else if (value.IsScalar)
            {
                // 단일 값을 리스트로 처리 (콤마 구분)
                var parsed = DffParser.ParseList(value.ScalarValue, options);
                items = parsed.ListValue ?? Array.Empty<DffValue>();
            }
            else
            {
                throw new DffBuildException($"Expected list value for repeated field '{field.Name}'");
            }

            foreach (var item in items)
            {
                if (item.IsUnset)
                    continue;

                var converted = ConvertValue(field, item, options);
                list.Add(converted);
            }
        }

        private static void SetMapFieldValue(IMessage msg, FieldDescriptor field, DffValue value, DffOptions options)
        {
            var dict = (IDictionary)field.Accessor.GetValue(msg);
            var keyField = field.MessageType.FindFieldByName("key");
            var valueField = field.MessageType.FindFieldByName("value");

            if (!value.IsObject)
            {
                throw new DffBuildException($"Expected object value for map field '{field.Name}'");
            }

            foreach (var kvp in value.ObjectValue!)
            {
                var keyValue = DffValue.FromScalar(kvp.Key);
                var convertedKey = ConvertValue(keyField, keyValue, options);
                var convertedValue = ConvertValue(valueField, kvp.Value, options);
                dict[convertedKey] = convertedValue;
            }
        }

        #endregion

        #region Value Conversion

        private static object ConvertValue(FieldDescriptor field, DffValue value, DffOptions options)
        {
            if (value.IsUnset)
            {
                return GetDefaultValue(field);
            }

            // Well-known types 처리
            if (field.FieldType == FieldType.Message)
            {
                var typeName = field.MessageType.FullName;
                if (typeName == "google.protobuf.Timestamp")
                {
                    return ConvertToTimestamp(value, options);
                }
                if (typeName == "google.protobuf.Duration")
                {
                    return ConvertToDuration(value, options);
                }

                // 일반 message
                return BuildMessage(field.MessageType, value, options);
            }

            // Scalar 값이 아니면 에러
            if (!value.IsScalar)
            {
                throw new DffBuildException($"Expected scalar value for field '{field.Name}', got {value.ValueKind}");
            }

            var raw = value.ScalarValue!;

            return field.FieldType switch
            {
                FieldType.Bool => ConvertToBool(raw, field.Name),
                FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 => ConvertToInt32(raw, field.Name, options),
                FieldType.UInt32 or FieldType.Fixed32 => ConvertToUInt32(raw, field.Name, options),
                FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64 => ConvertToInt64(raw, field.Name, options),
                FieldType.UInt64 or FieldType.Fixed64 => ConvertToUInt64(raw, field.Name, options),
                FieldType.Float => ConvertToFloat(raw, field.Name, options),
                FieldType.Double => ConvertToDouble(raw, field.Name, options),
                FieldType.String => raw,
                FieldType.Bytes => ConvertToBytes(raw, field.Name),
                FieldType.Enum => ConvertToEnum(raw, field.EnumType, field.Name, options),
                _ => throw new DffBuildException($"Unsupported field type '{field.FieldType}' for field '{field.Name}'")
            };
        }

        private static object GetDefaultValue(FieldDescriptor field)
        {
            return field.FieldType switch
            {
                FieldType.Bool => false,
                FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 => 0,
                FieldType.UInt32 or FieldType.Fixed32 => 0u,
                FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64 => 0L,
                FieldType.UInt64 or FieldType.Fixed64 => 0UL,
                FieldType.Float => 0f,
                FieldType.Double => 0d,
                FieldType.String => "",
                FieldType.Bytes => ByteString.Empty,
                FieldType.Enum => field.EnumType.Values[0].Number,
                FieldType.Message => CreateEmptyMessage(field.MessageType),
                _ => throw new DffBuildException($"Unsupported field type '{field.FieldType}'")
            };
        }

        #endregion

        #region Type Conversions

        private static bool ConvertToBool(string raw, string fieldName)
        {
            var lower = raw.ToLowerInvariant();
            return lower switch
            {
                "true" or "t" or "yes" or "y" or "1" => true,
                "false" or "f" or "no" or "n" or "0" => false,
                _ => throw new DffBuildException($"Invalid bool value '{raw}' for field '{fieldName}'")
            };
        }

        private static int ConvertToInt32(string raw, string fieldName, DffOptions options)
        {
            if (raw.Contains(".") || raw.Contains("e") || raw.Contains("E"))
            {
                throw new DffBuildException($"Integer field '{fieldName}' cannot have decimal or exponent: '{raw}'");
            }

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                throw new DffBuildException($"Invalid int32 value '{raw}' for field '{fieldName}'");
            }

            return result;
        }

        private static uint ConvertToUInt32(string raw, string fieldName, DffOptions options)
        {
            if (raw.Contains(".") || raw.Contains("e") || raw.Contains("E"))
            {
                throw new DffBuildException($"Integer field '{fieldName}' cannot have decimal or exponent: '{raw}'");
            }

            if (!uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                throw new DffBuildException($"Invalid uint32 value '{raw}' for field '{fieldName}'");
            }

            return result;
        }

        private static long ConvertToInt64(string raw, string fieldName, DffOptions options)
        {
            if (raw.Contains("e") || raw.Contains("E"))
            {
                throw new DffBuildException($"int64 field '{fieldName}' cannot have exponent: '{raw}'");
            }

            if (raw.Contains("."))
            {
                throw new DffBuildException($"int64 field '{fieldName}' cannot have decimal: '{raw}'");
            }

            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                throw new DffBuildException($"Invalid int64 value '{raw}' for field '{fieldName}'");
            }

            // 2^53-1 초과 검사 (Excel 숫자 셀 정밀도 손실 방지)
            const long MaxSafeInteger = 9007199254740991L; // 2^53 - 1
            if (!options.AllowLargeInt64 && (result > MaxSafeInteger || result < -MaxSafeInteger))
            {
                throw new DffBuildException($"int64 value '{raw}' for field '{fieldName}' exceeds safe integer range (2^53-1). Use text cell or enable AllowLargeInt64 option.");
            }

            return result;
        }

        private static ulong ConvertToUInt64(string raw, string fieldName, DffOptions options)
        {
            if (raw.Contains("e") || raw.Contains("E"))
            {
                throw new DffBuildException($"uint64 field '{fieldName}' cannot have exponent: '{raw}'");
            }

            if (raw.Contains("."))
            {
                throw new DffBuildException($"uint64 field '{fieldName}' cannot have decimal: '{raw}'");
            }

            if (!ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                throw new DffBuildException($"Invalid uint64 value '{raw}' for field '{fieldName}'");
            }

            // 2^53-1 초과 검사
            const ulong MaxSafeInteger = 9007199254740991UL;
            if (!options.AllowLargeInt64 && result > MaxSafeInteger)
            {
                throw new DffBuildException($"uint64 value '{raw}' for field '{fieldName}' exceeds safe integer range (2^53-1). Use text cell or enable AllowLargeInt64 option.");
            }

            return result;
        }

        private static float ConvertToFloat(string raw, string fieldName, DffOptions options)
        {
            if (!options.AllowNanInf)
            {
                var lower = raw.ToLowerInvariant();
                if (lower == "nan" || lower == "inf" || lower == "-inf" || lower == "+inf" || lower == "infinity" || lower == "-infinity")
                {
                    throw new DffBuildException($"NaN/Inf not allowed for field '{fieldName}'. Enable AllowNanInf option if needed.");
                }
            }

            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                throw new DffBuildException($"Invalid float value '{raw}' for field '{fieldName}'");
            }

            return result;
        }

        private static double ConvertToDouble(string raw, string fieldName, DffOptions options)
        {
            if (!options.AllowNanInf)
            {
                var lower = raw.ToLowerInvariant();
                if (lower == "nan" || lower == "inf" || lower == "-inf" || lower == "+inf" || lower == "infinity" || lower == "-infinity")
                {
                    throw new DffBuildException($"NaN/Inf not allowed for field '{fieldName}'. Enable AllowNanInf option if needed.");
                }
            }

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                throw new DffBuildException($"Invalid double value '{raw}' for field '{fieldName}'");
            }

            return result;
        }

        private static ByteString ConvertToBytes(string raw, string fieldName)
        {
            if (raw.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
            {
                var hex = raw.Substring(4);
                return ByteString.CopyFrom(HexToBytes(hex, fieldName));
            }

            if (raw.StartsWith("b64:", StringComparison.OrdinalIgnoreCase))
            {
                var b64 = raw.Substring(4);
                try
                {
                    return ByteString.CopyFrom(Convert.FromBase64String(b64));
                }
                catch (FormatException ex)
                {
                    throw new DffBuildException($"Invalid base64 value for field '{fieldName}': {ex.Message}");
                }
            }

            throw new DffBuildException($"Bytes field '{fieldName}' requires 'hex:' or 'b64:' prefix: '{raw}'");
        }

        private static byte[] HexToBytes(string hex, string fieldName)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            if (hex.Length % 2 != 0)
            {
                throw new DffBuildException($"Invalid hex string length for field '{fieldName}'");
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                {
                    throw new DffBuildException($"Invalid hex value for field '{fieldName}'");
                }
            }
            return bytes;
        }

        private static int ConvertToEnum(string raw, EnumDescriptor enumType, string fieldName, DffOptions options)
        {
            // 이름으로 찾기 (case-insensitive)
            foreach (var value in enumType.Values)
            {
                if (string.Equals(value.Name, raw, StringComparison.OrdinalIgnoreCase))
                {
                    return value.Number;
                }
            }

            // 숫자로 시도 (옵션에 따라)
            if (options.AllowEnumNumbers && int.TryParse(raw, out var number))
            {
                var found = enumType.FindValueByNumber(number);
                if (found != null)
                {
                    return number;
                }
                throw new DffBuildException($"Unknown enum number '{number}' for enum '{enumType.FullName}' in field '{fieldName}'");
            }

            throw new DffBuildException($"Unknown enum value '{raw}' for enum '{enumType.FullName}' in field '{fieldName}'");
        }

        #endregion

        #region Well-known Types

        private static Timestamp ConvertToTimestamp(DffValue value, DffOptions options)
        {
            if (!value.IsScalar)
            {
                throw new DffBuildException($"Expected scalar value for Timestamp, got {value.ValueKind}");
            }

            var raw = value.ScalarValue!;
            DateTimeOffset dto;

            // RFC3339 형식 시도
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dto))
            {
                // timezone 정보가 있으면 그대로 사용
                if (raw.Contains("+") || raw.Contains("Z") || raw.EndsWith("z", StringComparison.OrdinalIgnoreCase))
                {
                    return Timestamp.FromDateTimeOffset(dto);
                }
                // timezone 없으면 기본 timezone 적용
                dto = new DateTimeOffset(dto.DateTime, options.DefaultTimezoneOffset);
                return Timestamp.FromDateTimeOffset(dto);
            }

            // YYYY-MM-DD, YYYY-MM-DD HH:MM:SS, YYYY/MM/DD 등 시도
            string[] formats = {
                "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy/MM/dd",
                "yyyy/MM/dd HH:mm",
                "yyyy/MM/dd HH:mm:ss"
            };

            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(raw, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    dto = new DateTimeOffset(dt, options.DefaultTimezoneOffset);
                    return Timestamp.FromDateTimeOffset(dto);
                }
            }

            throw new DffBuildException($"Invalid Timestamp format: '{raw}'");
        }

        private static Duration ConvertToDuration(DffValue value, DffOptions options)
        {
            if (!value.IsScalar)
            {
                throw new DffBuildException($"Expected scalar value for Duration, got {value.ValueKind}");
            }

            var raw = value.ScalarValue!.Trim();

            // 순수 숫자 (초 단위)
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                return Duration.FromTimeSpan(TimeSpan.FromSeconds(seconds));
            }

            // HH:MM:SS 형식
            if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts))
            {
                return Duration.FromTimeSpan(ts);
            }

            // 1h30m, 2h, 30m, 45s 등 형식
            var match = Regex.Match(raw, @"^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$", RegexOptions.IgnoreCase);
            if (match.Success && match.Length > 0)
            {
                int hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                int minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                int secs = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                return Duration.FromTimeSpan(new TimeSpan(hours, minutes, secs));
            }

            throw new DffBuildException($"Invalid Duration format: '{raw}'");
        }

        #endregion

        #region Message Creation

        private static IMessage CreateEmptyMessage(MessageDescriptor descriptor)
        {
            // MessageParser를 통해 빈 메시지 생성
            var parser = descriptor.Parser;
            return parser.ParseFrom(ByteString.Empty);
        }

        #endregion
    }

    /// <summary>
    /// DFF → IMessage 변환 예외
    /// </summary>
    public class DffBuildException : Exception
    {
        public DffBuildException(string message) : base(message) { }
        public DffBuildException(string message, Exception inner) : base(message, inner) { }
    }
}
