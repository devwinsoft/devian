namespace Devian.Domain.Game
{
    /// <summary>
    /// Game-domain error information container.
    /// </summary>
    public sealed class GameError
    {
        public GAME_ERROR_TYPE Code { get; }
        public string Message { get; }
        public string? Details { get; }

        public GameError(GAME_ERROR_TYPE errorType, string message, string? details = null)
        {
            Code = errorType;
            Message = message;
            Details = details;
        }

        public override string ToString() => $"[{Code}] {Message}";
    }
}
