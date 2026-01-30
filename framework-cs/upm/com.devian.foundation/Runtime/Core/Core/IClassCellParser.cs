namespace Devian
{
    /// <summary>
    /// Class cell parser for embedded objects in table cells.
    /// </summary>
    public interface IClassCellParser
    {
        ParseResult<object> Parse(string value, ParseContext context);
    }

    public interface IClassCellParser<T> : IClassCellParser
    {
        new ParseResult<T> Parse(string value, ParseContext context);
    }
}
