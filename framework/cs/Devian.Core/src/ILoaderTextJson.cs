using System;

namespace Devian.Core
{
    /// <summary>
    /// JSON-based table loader interface.
    /// </summary>
    public interface ILoaderTextJson
    {
        Table<TKey, TRow> Load<TRow, TKey>(string tableName, Func<TRow, TKey> keySelector) where TKey : notnull;
    }

    public static class LoaderExtensions
    {
        public static Table<TKey, TRow> Load<TRow, TKey>(this ILoaderTextJson loader, string tableName, Func<TRow, TKey> keySelector) where TKey : notnull
        {
            return loader.Load<TRow, TKey>(tableName, keySelector);
        }
    }
}
