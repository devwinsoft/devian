namespace Devian.Domain.Game
{
    /// <summary>
    /// Non-generic result type for Game-domain operations that may fail.
    /// </summary>
    public readonly struct GameResult
    {
        public GameError? Error { get; }
        public bool IsSuccess => Error == null;
        public bool IsFailure => Error != null;

        private GameResult(GameError? error)
        {
            Error = error;
        }

        public static GameResult Ok() => new(null);

        public static GameResult Failure(GameError error) => new(error);

        public static GameResult Failure(GAME_ERROR_TYPE errorType, string message, string? details = null)
            => new(new GameError(errorType, message, details));
    }

    /// <summary>
    /// Generic result type for Game-domain operations that may fail.
    /// </summary>
    public readonly struct GameResult<T>
    {
        public T? Value { get; }
        public GameError? Error { get; }
        public bool IsSuccess => Error == null;
        public bool IsFailure => Error != null;

        private GameResult(T? value, GameError? error)
        {
            Value = value;
            Error = error;
        }

        public static GameResult<T> Success(T value) => new(value, null);

        public static GameResult<T> Failure(GameError error) => new(default, error);

        public static GameResult<T> Failure(GAME_ERROR_TYPE errorType, string message, string? details = null)
            => new(default, new GameError(errorType, message, details));
    }
}
