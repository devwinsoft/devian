namespace Devian.Domain.Common
{
    /// <summary>
    /// Error information container.
    /// Basic design: Domain.Common owns error/result primitives.
    /// </summary>
    public sealed class CoreError
    {
        public string Code { get; }
        public string Message { get; }
        public string? Details { get; }

        public CoreError(CommonErrorType errorType, string message, string? details = null)
        {
            Code = errorType.ToString();
            Message = message;
            Details = details;
        }

        [System.Obsolete("Use CoreError(CommonErrorType, ...) instead.")]
        public CoreError(string code, string message, string? details = null)
        {
            Code = code;
            Message = message;
            Details = details;
        }

        public override string ToString() => $"[{Code}] {Message}";
    }
}
