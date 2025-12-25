using System.Collections.Generic;

namespace Devian.Tools.Models
{
    public sealed class TableMeta
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public List<ColumnMeta> Columns { get; set; }
        public string KeyColumn { get; set; }
        
        public TableMeta()
        {
            Name = "";
            FileName = "";
            Columns = new List<ColumnMeta>();
        }
    }

    public sealed class ColumnMeta
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsKey { get; set; }
        public bool IsOptional { get; set; }
        
        public ColumnMeta()
        {
            Name = "";
            Type = "";
        }
    }
}
