using System.Collections.Generic;

namespace Devian.Tools.Models
{
    public sealed class TableSchema
    {
        public string FileName { get; set; }
        public string SheetName { get; set; }
        public List<ColumnSpec> Columns { get; set; }
        public string KeyColumnName { get; set; }
        public bool HasKey { get; set; }
        
        public TableSchema()
        {
            FileName = "";
            SheetName = "";
            Columns = new List<ColumnSpec>();
        }
    }

    public sealed class ColumnSpec
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsKey { get; set; }
        public bool Optional { get; set; }
        public string ParserType { get; set; }
        public int ColIndex { get; set; }
        
        public ColumnSpec()
        {
            Name = "";
            Type = "";
        }
    }

    public sealed class TablesConfig
    {
        public string ArraySeparator { get; set; }
        public string EmptyPolicy { get; set; }
        public string OutputShape { get; set; }
        public JsonOutputConfig Json { get; set; }
        
        public TablesConfig()
        {
            ArraySeparator = ",";
            EmptyPolicy = "lenient";
            OutputShape = "array";
            Json = new JsonOutputConfig();
        }
    }

    public sealed class JsonOutputConfig
    {
        public bool Pretty { get; set; }
        
        public JsonOutputConfig()
        {
            Pretty = true;
        }
    }
}
