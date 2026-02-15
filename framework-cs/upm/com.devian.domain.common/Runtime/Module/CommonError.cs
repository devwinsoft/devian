namespace Devian.Domain.Common
{
    /// <summary>
    /// Error information container.
    /// Basic design: Domain.Common owns error/result primitives.
    /// </summary>
    public sealed class CommonError
    {
        public CommonErrorType Code { get; }
        public string Message { get; }
        public string? Details { get; }

        public CommonError(CommonErrorType errorType, string message, string? details = null)
        {
            Code = errorType;
            Message = message;
            Details = details;
        }

        [System.Obsolete("Use CommonError(CommonErrorType, ...) instead.")]
        public CommonError(string code, string message, string? details = null)
        {
            Code = CommonErrorType.COMMON_UNKNOWN;
            Message = message;
            Details = string.IsNullOrEmpty(details) ? $"legacyCode={code}" : $"legacyCode={code}; {details}";
        }

        public override string ToString() => $"[{Code}] {Message}";
    }
}
