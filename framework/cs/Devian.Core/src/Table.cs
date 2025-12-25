using System;
using System.Collections.Generic;

namespace Devian.Core
{
    /// <summary>
    /// Generic table container with key-based lookup.
    /// </summary>
    public sealed class Table<TKey, TRow> where TKey : notnull
    {
        private readonly Dictionary<TKey, TRow> _map;
        private readonly List<TRow> _rows;

        public Table(IEnumerable<TRow> rows, Func<TRow, TKey> keySelector)
        {
            _rows = new List<TRow>(rows);
            _map = new Dictionary<TKey, TRow>();
            foreach (var row in _rows)
            {
                _map[keySelector(row)] = row;
            }
        }

        public TRow? Get(TKey key) => _map.TryGetValue(key, out var row) ? row : default;
        public TRow this[TKey key] => _map[key];
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);
        public IReadOnlyList<TRow> Rows => _rows;
        public int Count => _rows.Count;
    }
}
