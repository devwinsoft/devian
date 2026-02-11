// SSOT: skills/devian-core/32-variable-variant/SKILL.md

using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Devian
{
    public enum VariantKind : byte
    {
        Int = 1,
        Float = 2,
        String = 3,
    }

    /// <summary>
    /// Immutable tagged union for Int/Float/String values.
    /// JSON format: {"i": number} | {"f": number} | {"s": string}
    /// Exactly one key per object.
    /// </summary>
    [JsonConverter(typeof(VariantJsonConverter))]
    public readonly struct Variant : IEquatable<Variant>
    {
        private readonly VariantKind _kind;
        private readonly int _intValue;
        private readonly float _floatValue;
        private readonly string? _stringValue;

        public VariantKind Kind => _kind;

        // Private constructors
        private Variant(VariantKind kind, int intValue, float floatValue, string? stringValue)
        {
            _kind = kind;
            _intValue = intValue;
            _floatValue = floatValue;
            _stringValue = stringValue;
        }

        // Factory methods
        public static Variant FromInt(int value)
        {
            return new Variant(VariantKind.Int, value, 0f, null);
        }

        public static Variant FromFloat(float value)
        {
            return new Variant(VariantKind.Float, 0, value, null);
        }

        public static Variant FromString(string value)
        {
            return new Variant(VariantKind.String, 0, 0f, value ?? throw new ArgumentNullException(nameof(value)));
        }

        // Strict accessors
        public bool TryGetInt(out int value)
        {
            if (_kind == VariantKind.Int)
            {
                value = _intValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetFloat(out float value)
        {
            if (_kind == VariantKind.Float)
            {
                value = _floatValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetString(out string? value)
        {
            if (_kind == VariantKind.String)
            {
                value = _stringValue;
                return true;
            }
            value = null;
            return false;
        }

        // Throwing accessors (convenience)
        public int AsInt() => _kind == VariantKind.Int
            ? _intValue
            : throw new InvalidOperationException($"Variant is {_kind}, not Int");

        public float AsFloat() => _kind == VariantKind.Float
            ? _floatValue
            : throw new InvalidOperationException($"Variant is {_kind}, not Float");

        public string AsString() => _kind == VariantKind.String
            ? _stringValue!
            : throw new InvalidOperationException($"Variant is {_kind}, not String");

        // Table input parser: "i:123", "f:3.5", "s:Hello"
        public static Variant Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new FormatException("Variant input cannot be null or empty");

            var trimmed = input.Trim();
            if (trimmed.Length < 2 || trimmed[1] != ':')
                throw new FormatException($"Invalid Variant format: '{input}'. Expected 'i:', 'f:', or 's:' prefix.");

            var prefix = trimmed[0];
            var body = trimmed.Substring(2);

            return prefix switch
            {
                'i' => ParseInt(body, input),
                'f' => ParseFloat(body, input),
                's' => FromString(body),
                _ => throw new FormatException($"Invalid Variant prefix '{prefix}' in '{input}'. Expected 'i', 'f', or 's'.")
            };
        }

        public static bool TryParse(string input, out Variant result)
        {
            try
            {
                result = Parse(input);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        private static Variant ParseInt(string body, string original)
        {
            // Check for decimal point (not allowed for int)
            if (body.Contains("."))
                throw new FormatException($"Integer value cannot have decimal: '{original}'");
            if (!int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Invalid integer value in Variant: '{original}'");
            return FromInt(value);
        }

        private static Variant ParseFloat(string body, string original)
        {
            if (!float.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Invalid float value in Variant: '{original}'");
            return FromFloat(value);
        }

        // IEquatable
        public bool Equals(Variant other)
        {
            if (_kind != other._kind) return false;
            return _kind switch
            {
                VariantKind.Int => _intValue == other._intValue,
                VariantKind.Float => _floatValue == other._floatValue,
                VariantKind.String => _stringValue == other._stringValue,
                _ => false
            };
        }

        public override bool Equals(object? obj) => obj is Variant v && Equals(v);

        public override int GetHashCode()
        {
            return _kind switch
            {
                VariantKind.Int => HashCode.Combine(_kind, _intValue),
                VariantKind.Float => HashCode.Combine(_kind, _floatValue),
                VariantKind.String => HashCode.Combine(_kind, _stringValue),
                _ => _kind.GetHashCode()
            };
        }

        public static bool operator ==(Variant left, Variant right) => left.Equals(right);
        public static bool operator !=(Variant left, Variant right) => !left.Equals(right);

        public override string ToString()
        {
            return _kind switch
            {
                VariantKind.Int => $"i:{_intValue}",
                VariantKind.Float => $"f:{_floatValue.ToString(CultureInfo.InvariantCulture)}",
                VariantKind.String => $"s:{_stringValue}",
                _ => $"<unknown:{_kind}>"
            };
        }
    }

    /// <summary>
    /// Strict JsonConverter for Variant.
    /// Format: {"i": number} | {"f": number} | {"s": string}
    /// Exactly one key per object.
    /// </summary>
    public class VariantJsonConverter : JsonConverter<Variant>
    {
        public override void WriteJson(JsonWriter writer, Variant value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            
            switch (value.Kind)
            {
                case VariantKind.Int:
                    writer.WritePropertyName("i");
                    writer.WriteValue(value.AsInt());
                    break;
                    
                case VariantKind.Float:
                    writer.WritePropertyName("f");
                    writer.WriteValue(value.AsFloat());
                    break;
                    
                case VariantKind.String:
                    writer.WritePropertyName("s");
                    writer.WriteValue(value.AsString());
                    break;
                    
                default:
                    throw new JsonSerializationException($"Unknown VariantKind: {value.Kind}");
            }
            
            writer.WriteEndObject();
        }

        public override Variant ReadJson(JsonReader reader, Type objectType, Variant existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                throw new JsonSerializationException("Variant cannot be null");

            var obj = JObject.Load(reader);
            var properties = obj.Properties().ToList();

            // Strict: exactly one key
            if (properties.Count != 1)
                throw new JsonSerializationException($"Variant must have exactly one key (i, f, or s), got {properties.Count} keys");

            var prop = properties[0];
            var key = prop.Name;

            switch (key)
            {
                case "i":
                    if (prop.Value.Type != JTokenType.Integer)
                        throw new JsonSerializationException($"Variant 'i' value must be integer, got: {prop.Value.Type}");
                    return Variant.FromInt(prop.Value.Value<int>());
                    
                case "f":
                    if (prop.Value.Type != JTokenType.Float && prop.Value.Type != JTokenType.Integer)
                        throw new JsonSerializationException($"Variant 'f' value must be number, got: {prop.Value.Type}");
                    return Variant.FromFloat(prop.Value.Value<float>());
                    
                case "s":
                    if (prop.Value.Type != JTokenType.String)
                        throw new JsonSerializationException($"Variant 's' value must be string, got: {prop.Value.Type}");
                    return Variant.FromString(prop.Value.Value<string>() ?? "");
                    
                default:
                    throw new JsonSerializationException($"Invalid Variant key '{key}'. Expected 'i', 'f', or 's'.");
            }
        }
    }
}
