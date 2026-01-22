namespace Devian.Core
{
    /// <summary>
    /// Context for parsing operations.
    /// </summary>
    public sealed class ParseContext
    {
        public string TableName { get; }
        public int RowIndex { get; set; }
        public int ColumnIndex { get; set; }
        public string? ColumnName { get; set; }
        public string ArraySeparator { get; set; } = ",";

        public ParseContext(string tableName)
        {
            TableName = tableName;
        }

        public string Location => $"{TableName}[{RowIndex},{ColumnIndex}]";
    }
}
