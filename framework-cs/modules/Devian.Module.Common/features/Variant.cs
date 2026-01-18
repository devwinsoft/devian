// SSOT: skills/devian-common/11-feature-variant/SKILL.md

using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Devian.Module.Common
{
    public enum VariantKind : byte
    {
        Int = 1,
        Float = 2,
        String = 3,
    }

    /// <summary>
    /// Immutable tagged union for Int/Float/String values.
    /// Internal representation uses CInt/CFloat/CString (Complex shapes).
    /// JSON serialization uses Tagged Union + Complex shape format.
    /// </summary>
    [JsonConverter(typeof(VariantJsonConverter))]
    public readonly struct Variant : IEquatable<Variant>
    {
        private readonly VariantKind _kind;
        private readonly CInt _i;
        private readonly CFloat _f;
        private readonly CString _s;

        public VariantKind Kind => _kind;

        // Internal CInt/CFloat/CString accessors (for JsonConverter)
        internal CInt RawInt => _i;
        internal CFloat RawFloat => _f;
        internal CString RawString => _s;

        // Private constructors
        private Variant(VariantKind kind, CInt i, CFloat f, CString s)
        {
            _kind = kind;
            _i = i;
            _f = f;
            _s = s;
        }

        // Factory methods (from plain values)
        public static Variant FromInt(int value)
        {
            var ci = new CInt(value);
            return new Variant(VariantKind.Int, ci, default, default);
        }

        public static Variant FromFloat(float value)
        {
            var cf = new CFloat(value);
            return new Variant(VariantKind.Float, default, cf, default);
        }

        public static Variant FromString(string value)
        {
            var cs = new CString(value ?? throw new ArgumentNullException(nameof(value)));
            return new Variant(VariantKind.String, default, default, cs);
        }

        // Raw factory methods (for deserialization - sets raw save1/save2/data)
        public static Variant FromRaw(CInt cint)
        {
            return new Variant(VariantKind.Int, cint, default, default);
        }

        public static Variant FromRaw(CFloat cfloat)
        {
            return new Variant(VariantKind.Float, default, cfloat, default);
        }

        public static Variant FromRaw(CString cstring)
        {
            return new Variant(VariantKind.String, default, default, cstring);
        }

        // Strict accessors
        public bool TryGetInt(out int value)
        {
            if (_kind == VariantKind.Int)
            {
                value = _i.GetValue();
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetFloat(out float value)
        {
            if (_kind == VariantKind.Float)
            {
                value = _f.GetValue();
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetString(out string? value)
        {
            if (_kind == VariantKind.String)
            {
                value = _s.GetValue();
                return true;
            }
            value = null;
            return false;
        }

        // Throwing accessors (convenience)
        public int AsInt() => _kind == VariantKind.Int
            ? _i.GetValue()
            : throw new InvalidOperationException($"Variant is {_kind}, not Int");

        public float AsFloat() => _kind == VariantKind.Float
            ? _f.GetValue()
            : throw new InvalidOperationException($"Variant is {_kind}, not Float");

        public string AsString() => _kind == VariantKind.String
            ? _s.GetValue()
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

        // IEquatable (compare decoded values)
        public bool Equals(Variant other)
        {
            if (_kind != other._kind) return false;
            return _kind switch
            {
                VariantKind.Int => _i.GetValue() == other._i.GetValue(),
                VariantKind.Float => _f.GetValue() == other._f.GetValue(),
                VariantKind.String => _s.GetValue() == other._s.GetValue(),
                _ => false
            };
        }

        public override bool Equals(object? obj) => obj is Variant v && Equals(v);

        public override int GetHashCode()
        {
            return _kind switch
            {
                VariantKind.Int => HashCode.Combine(_kind, _i.GetValue()),
                VariantKind.Float => HashCode.Combine(_kind, _f.GetValue()),
                VariantKind.String => HashCode.Combine(_kind, _s.GetValue()),
                _ => _kind.GetHashCode()
            };
        }

        public static bool operator ==(Variant left, Variant right) => left.Equals(right);
        public static bool operator !=(Variant left, Variant right) => !left.Equals(right);

        public override string ToString()
        {
            return _kind switch
            {
                VariantKind.Int => $"i:{_i.GetValue()}",
                VariantKind.Float => $"f:{_f.GetValue().ToString(CultureInfo.InvariantCulture)}",
                VariantKind.String => $"s:{_s.GetValue()}",
                _ => $"<unknown:{_kind}>"
            };
        }
    }

    /// <summary>
    /// Strict JsonConverter for Variant.
    /// Serializes to Tagged Union + Complex shape format:
    /// - Int:    {"k":"i","i":{"save1":...,"save2":...}}
    /// - Float:  {"k":"f","f":{"save1":...,"save2":...}}
    /// - String: {"k":"s","s":{"data":"..."}}
    /// </summary>
    public class VariantJsonConverter : JsonConverter<Variant>
    {
        public override void WriteJson(JsonWriter writer, Variant value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            
            switch (value.Kind)
            {
                case VariantKind.Int:
                    writer.WritePropertyName("k");
                    writer.WriteValue("i");
                    writer.WritePropertyName("i");
                    writer.WriteStartObject();
                    writer.WritePropertyName("save1");
                    writer.WriteValue(value.RawInt.save1);
                    writer.WritePropertyName("save2");
                    writer.WriteValue(value.RawInt.save2);
                    writer.WriteEndObject();
                    break;
                    
                case VariantKind.Float:
                    writer.WritePropertyName("k");
                    writer.WriteValue("f");
                    writer.WritePropertyName("f");
                    writer.WriteStartObject();
                    writer.WritePropertyName("save1");
                    writer.WriteValue(value.RawFloat.save1);
                    writer.WritePropertyName("save2");
                    writer.WriteValue(value.RawFloat.save2);
                    writer.WriteEndObject();
                    break;
                    
                case VariantKind.String:
                    writer.WritePropertyName("k");
                    writer.WriteValue("s");
                    writer.WritePropertyName("s");
                    writer.WriteStartObject();
                    writer.WritePropertyName("data");
                    writer.WriteValue(value.RawString.data ?? "");
                    writer.WriteEndObject();
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

            // Strict: k must exist and be 'i'|'f'|'s'
            var kToken = obj["k"];
            if (kToken == null)
                throw new JsonSerializationException("Variant JSON must have 'k' property");

            var k = kToken.Value<string>();
            if (k != "i" && k != "f" && k != "s")
                throw new JsonSerializationException($"Variant 'k' must be 'i', 'f', or 's', got '{k}'");

            // Strict: only the matching value property should exist
            var hasI = obj["i"] != null;
            var hasF = obj["f"] != null;
            var hasS = obj["s"] != null;

            switch (k)
            {
                case "i":
                    if (!hasI)
                        throw new JsonSerializationException("Variant k='i' but 'i' property is missing");
                    if (hasF || hasS)
                        throw new JsonSerializationException("Variant k='i' but has extra 'f' or 's' properties");
                    return ParseCInt(obj["i"]!);
                    
                case "f":
                    if (!hasF)
                        throw new JsonSerializationException("Variant k='f' but 'f' property is missing");
                    if (hasI || hasS)
                        throw new JsonSerializationException("Variant k='f' but has extra 'i' or 's' properties");
                    return ParseCFloat(obj["f"]!);
                    
                case "s":
                    if (!hasS)
                        throw new JsonSerializationException("Variant k='s' but 's' property is missing");
                    if (hasI || hasF)
                        throw new JsonSerializationException("Variant k='s' but has extra 'i' or 'f' properties");
                    return ParseCString(obj["s"]!);
                    
                default:
                    throw new JsonSerializationException($"Unknown Variant kind: {k}");
            }
        }

        private static Variant ParseCInt(JToken token)
        {
            if (token.Type != JTokenType.Object)
                throw new JsonSerializationException("Variant 'i' value must be an object with {save1, save2}");

            var save1Token = token["save1"];
            var save2Token = token["save2"];

            if (save1Token == null)
                throw new JsonSerializationException("Variant 'i' object missing 'save1'");
            if (save2Token == null)
                throw new JsonSerializationException("Variant 'i' object missing 'save2'");

            var ci = new CInt();
            ci.SetRaw(save1Token.Value<int>(), save2Token.Value<int>());
            return Variant.FromRaw(ci);
        }

        private static Variant ParseCFloat(JToken token)
        {
            if (token.Type != JTokenType.Object)
                throw new JsonSerializationException("Variant 'f' value must be an object with {save1, save2}");

            var save1Token = token["save1"];
            var save2Token = token["save2"];

            if (save1Token == null)
                throw new JsonSerializationException("Variant 'f' object missing 'save1'");
            if (save2Token == null)
                throw new JsonSerializationException("Variant 'f' object missing 'save2'");

            var cf = new CFloat();
            cf.SetRaw(save1Token.Value<int>(), save2Token.Value<int>());
            return Variant.FromRaw(cf);
        }

        private static Variant ParseCString(JToken token)
        {
            if (token.Type != JTokenType.Object)
                throw new JsonSerializationException("Variant 's' value must be an object with {data}");

            var dataToken = token["data"];
            if (dataToken == null)
                throw new JsonSerializationException("Variant 's' object missing 'data'");

            var cs = new CString();
            cs.SetRaw(dataToken.Value<string>() ?? "");
            return Variant.FromRaw(cs);
        }
    }
}
