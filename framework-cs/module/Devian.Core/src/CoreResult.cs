namespace Devian.Core
{
    /// <summary>
    /// Generic result type for operations that may fail.
    /// </summary>
    public readonly struct CoreResult<T>
    {
        public T? Value { get; }
        public CoreError? Error { get; }
        public bool IsSuccess => Error == null;
        public bool IsFailure => Error != null;

        private CoreResult(T? value, CoreError? error)
        {
            Value = value;
            Error = error;
        }

        public static CoreResult<T> Success(T value) => new(value, null);
        public static CoreResult<T> Failure(CoreError error) => new(default, error);
        public static CoreResult<T> Failure(string code, string message) => new(default, new CoreError(code, message));
    }
}
