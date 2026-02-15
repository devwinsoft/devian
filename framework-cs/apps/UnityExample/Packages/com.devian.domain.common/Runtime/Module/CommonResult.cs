namespace Devian.Domain.Common
{
    /// <summary>
    /// Generic result type for operations that may fail.
    /// Basic design: Domain.Common owns error/result primitives.
    /// </summary>
    public readonly struct CommonResult<T>
    {
        public T? Value { get; }
        public CommonError? Error { get; }
        public bool IsSuccess => Error == null;
        public bool IsFailure => Error != null;

        private CommonResult(T? value, CommonError? error)
        {
            Value = value;
            Error = error;
        }

        public static CommonResult<T> Success(T value) => new(value, null);

        public static CommonResult<T> Failure(CommonError error) => new(default, error);

        public static CommonResult<T> Failure(CommonErrorType errorType, string message)
            => new(default, new CommonError(errorType, message));

        [System.Obsolete("Use Failure(CommonErrorType, string) instead.")]
        public static CommonResult<T> Failure(string code, string message)
            => new(default, new CommonError(CommonErrorType.COMMON_UNKNOWN, message, $"legacyCode={code}"));
    }
}
