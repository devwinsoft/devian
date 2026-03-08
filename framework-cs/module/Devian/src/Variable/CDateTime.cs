// SSOT: skills/devian/10-module/20-core/37-variable-datetime/SKILL.md

using System;
using System.Text;
using Newtonsoft.Json;

namespace Devian
{
    /// <summary>
    /// Simple UTC DateTime wrapper.
    /// dateTime is derived from utcTimeMs.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(CDateTimeUnixMsConverter))]
    public struct CDateTime : IEquatable<CDateTime>
    {
        private const long MinUnixTimeMs = -62135596800000L;   // 0001-01-01T00:00:00.000Z
        private const long MaxUnixTimeMs = 253402300799999L;   // 9999-12-31T23:59:59.999Z

        public long utcTimeMs;
        public DateTime dateTime;

        public CDateTime(long utcTimeMs)
        {
            this.utcTimeMs = 0L;
            dateTime = DateTime.MinValue;
            Initialize(utcTimeMs);
        }

        public CDateTime(DateTime dt)
        {
            utcTimeMs = 0L;
            dateTime = DateTime.MinValue;
            SetDateTime(dt);
        }

        public CDateTime(string raw)
        {
            utcTimeMs = 0L;
            dateTime = DateTime.MinValue;
            SetDateTime(ParseDateTime(raw));
        }

        public void Initialize(long utcTimeMs)
        {
            this.utcTimeMs = ClampUnixTimeMs(utcTimeMs);
            dateTime = ToUtcDateTime(this.utcTimeMs);
        }

        public void SetUtcTimeMs(long utcTimeMs)
        {
            Initialize(utcTimeMs);
        }

        public void SetDateTime(DateTime dateTime)
        {
            var utc = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();

            var utcTimeMs = new DateTimeOffset(utc).ToUnixTimeMilliseconds();
            Initialize(utcTimeMs);
        }

        public static DateTime ToUtcDateTime(long utcTimeMs)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ClampUnixTimeMs(utcTimeMs)).UtcDateTime;
        }

        public bool Equals(CDateTime other)
        {
            return utcTimeMs == other.utcTimeMs;
        }

        public override bool Equals(object obj)
        {
            return obj is CDateTime other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + utcTimeMs.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(CDateTime left, CDateTime right) => left.Equals(right);
        public static bool operator !=(CDateTime left, CDateTime right) => !left.Equals(right);

        public override string ToString()
        {
            return $"utcTimeMs={utcTimeMs}, dateTime={dateTime:O}";
        }

        private static long ClampUnixTimeMs(long value)
        {
            if (value < MinUnixTimeMs)
                return MinUnixTimeMs;
            if (value > MaxUnixTimeMs)
                return MaxUnixTimeMs;
            return value;
        }

        private static DateTime ParseDateTime(string raw)
        {
            var digits = CollectDigits(raw);
            var index = 0;

            var year = ReadPart(digits, ref index, 4);
            var month = ReadPart(digits, ref index, 2);
            var day = ReadPart(digits, ref index, 2);
            var hour = ReadPart(digits, ref index, 2);
            var minute = ReadPart(digits, ref index, 2);
            var second = ReadPart(digits, ref index, 2);
            var millisecond = ReadPart(digits, ref index, 3);

            NormalizeParts(ref year, ref month, ref day, ref hour, ref minute, ref second, ref millisecond);
            return new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
        }

        private static string CollectDigits(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            var sb = new StringBuilder(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (char.IsDigit(c))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static int ReadPart(string digits, ref int index, int length)
        {
            if (index >= digits.Length || length <= 0)
                return 0;

            var remain = digits.Length - index;
            var count = remain < length ? remain : length;
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                value = (value * 10) + (digits[index + i] - '0');
            }

            index += count;
            return value;
        }

        private static void NormalizeParts(
            ref int year,
            ref int month,
            ref int day,
            ref int hour,
            ref int minute,
            ref int second,
            ref int millisecond)
        {
            year = ClampInt(year, 1, 9999);
            month = ClampInt(month, 1, 12);

            var maxDay = DateTime.DaysInMonth(year, month);
            day = ClampInt(day, 1, maxDay);

            hour = ClampInt(hour, 0, 23);
            minute = ClampInt(minute, 0, 59);
            second = ClampInt(second, 0, 59);
            millisecond = ClampInt(millisecond, 0, 999);
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }

    /// <summary>
    /// Serializes CDateTime as utcTimeMs (long) for NDJSON/pb64.
    /// </summary>
    internal sealed class CDateTimeUnixMsConverter : JsonConverter<CDateTime>
    {
        public override CDateTime ReadJson(
            JsonReader reader,
            Type objectType,
            CDateTime existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Integer)
            {
                var utcTimeMs = Convert.ToInt64(reader.Value);
                return new CDateTime(utcTimeMs);
            }

            if (reader.TokenType == JsonToken.String)
            {
                var raw = Convert.ToString(reader.Value);
                if (long.TryParse(raw, out var utcTimeMs))
                    return new CDateTime(utcTimeMs);
                return new CDateTime(raw);
            }

            if (reader.TokenType == JsonToken.Null)
                return new CDateTime(0L);

            throw new JsonSerializationException(
                $"Unexpected token {reader.TokenType} when reading CDateTime.");
        }

        public override void WriteJson(
            JsonWriter writer,
            CDateTime value,
            JsonSerializer serializer)
        {
            writer.WriteValue(value.utcTimeMs);
        }
    }
}
