using System;
using System.Collections.Generic;
using System.Globalization;

namespace Devian.Core
{
    /// <summary>
    /// Registry for core value parsers.
    /// </summary>
    public static class CoreValueParserRegistry
    {
        private static readonly Dictionary<Type, IValueParser> _parsers = new();

        static CoreValueParserRegistry()
        {
            Register(new IntParser());
            Register(new FloatParser());
            Register(new BoolParser());
            Register(new StringParser());
        }

        public static void Register<T>(IValueParser<T> parser)
        {
            _parsers[typeof(T)] = parser;
        }

        public static IValueParser? Get(Type type) => _parsers.TryGetValue(type, out var p) ? p : null;

        private sealed class IntParser : IValueParser<int>
        {
            public ParseResult<int> Parse(string value, ParseContext ctx)
            {
                if (int.TryParse(value, out var result)) return ParseResult<int>.Ok(result);
                return ParseResult<int>.Fail($"Invalid int: {value}", ctx.RowIndex, ctx.ColumnIndex);
            }
            ParseResult<object> IValueParser.Parse(string value, ParseContext ctx) => 
                Parse(value, ctx).Success ? ParseResult<object>.Ok(Parse(value, ctx).Value!) : ParseResult<object>.Fail(Parse(value, ctx).ErrorMessage!);
        }

        private sealed class FloatParser : IValueParser<float>
        {
            public ParseResult<float> Parse(string value, ParseContext ctx)
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) 
                    return ParseResult<float>.Ok(result);
                return ParseResult<float>.Fail($"Invalid float: {value}", ctx.RowIndex, ctx.ColumnIndex);
            }
            ParseResult<object> IValueParser.Parse(string value, ParseContext ctx) => 
                Parse(value, ctx).Success ? ParseResult<object>.Ok(Parse(value, ctx).Value!) : ParseResult<object>.Fail(Parse(value, ctx).ErrorMessage!);
        }

        private sealed class BoolParser : IValueParser<bool>
        {
            public ParseResult<bool> Parse(string value, ParseContext ctx)
            {
                if (bool.TryParse(value, out var result)) return ParseResult<bool>.Ok(result);
                if (value == "1" || value.ToLower() == "true") return ParseResult<bool>.Ok(true);
                if (value == "0" || value.ToLower() == "false") return ParseResult<bool>.Ok(false);
                return ParseResult<bool>.Fail($"Invalid bool: {value}", ctx.RowIndex, ctx.ColumnIndex);
            }
            ParseResult<object> IValueParser.Parse(string value, ParseContext ctx) => 
                Parse(value, ctx).Success ? ParseResult<object>.Ok(Parse(value, ctx).Value!) : ParseResult<object>.Fail(Parse(value, ctx).ErrorMessage!);
        }

        private sealed class StringParser : IValueParser<string>
        {
            public ParseResult<string> Parse(string value, ParseContext ctx) => ParseResult<string>.Ok(value);
            ParseResult<object> IValueParser.Parse(string value, ParseContext ctx) => ParseResult<object>.Ok(value);
        }
    }
}
