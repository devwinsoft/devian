namespace Devian.Core
{
    /// <summary>
    /// Result of parsing operation.
    /// </summary>
    public readonly struct ParseResult<T>
    {
        public T? Value { get; }
        public bool Success { get; }
        public string? ErrorMessage { get; }
        public int? ErrorRow { get; }
        public int? ErrorColumn { get; }

        private ParseResult(T? value, bool success, string? errorMessage, int? errorRow, int? errorColumn)
        {
            Value = value;
            Success = success;
            ErrorMessage = errorMessage;
            ErrorRow = errorRow;
            ErrorColumn = errorColumn;
        }

        public static ParseResult<T> Ok(T value) => new(value, true, null, null, null);
        public static ParseResult<T> Fail(string message, int? row = null, int? col = null) 
            => new(default, false, message, row, col);
    }
}
