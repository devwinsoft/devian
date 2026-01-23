namespace Devian
{
    /// <summary>
    /// Error information container.
    /// </summary>
    public sealed class CoreError
    {
        public string Code { get; }
        public string Message { get; }
        public string? Details { get; }

        public CoreError(string code, string message, string? details = null)
        {
            Code = code;
            Message = message;
            Details = details;
        }

        public override string ToString() => $"[{Code}] {Message}";
    }
}
