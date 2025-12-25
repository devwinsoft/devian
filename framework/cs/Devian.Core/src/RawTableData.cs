using System.Collections.Generic;

namespace Devian.Core
{
    /// <summary>
    /// Raw table data before parsing.
    /// </summary>
    public sealed class RawTableData
    {
        public string Name { get; set; } = "";
        public List<string> Headers { get; set; } = new();
        public List<string> Types { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
        public int? KeyColumnIndex { get; set; }
    }
}
