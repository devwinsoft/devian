using System;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Registry for class cell parsers.
    /// </summary>
    public static class ClassCellParserRegistry
    {
        private static readonly Dictionary<Type, IClassCellParser> _parsers = new();

        public static void Register<T>(IClassCellParser<T> parser)
        {
            _parsers[typeof(T)] = parser;
        }

        public static IClassCellParser? Get(Type type) => _parsers.TryGetValue(type, out var p) ? p : null;

        public static IClassCellParser<T>? Get<T>() => _parsers.TryGetValue(typeof(T), out var p) ? (IClassCellParser<T>)p : null;
    }
}
