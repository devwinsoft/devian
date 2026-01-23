namespace Devian
{
    /// <summary>
    /// Value parser interface for table cells.
    /// </summary>
    public interface IValueParser
    {
        ParseResult<object> Parse(string value, ParseContext context);
    }

    public interface IValueParser<T> : IValueParser
    {
        new ParseResult<T> Parse(string value, ParseContext context);
    }
}
