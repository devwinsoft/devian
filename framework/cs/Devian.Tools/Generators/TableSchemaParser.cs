using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Devian.Tools.Models;

namespace Devian.Tools.Generators
{
    public static class TableSchemaParser
    {
        private static readonly HashSet<string> PrimitiveTypes = new HashSet<string>
        {
            "string", "int", "float", "bool", "json",
            "string[]", "int[]", "float[]", "bool[]"
        };
        
        public static TableSchema ParseFourRowHeader(
            IReadOnlyList<string> row1,
            IReadOnlyList<string> row2,
            IReadOnlyList<string> row3,
            IReadOnlyList<string> row4,
            string fileName,
            string sheetName)
        {
            // row4 = comment, 무시
            
            var schema = new TableSchema();
            schema.FileName = fileName;
            schema.SheetName = sheetName;
            
            var colCount = Math.Min(row1.Count, Math.Min(row2.Count, row3.Count));
            int keyCount = 0;
            
            for (int i = 0; i < colCount; i++)
            {
                var name = row1[i] != null ? row1[i].Trim() : "";
                var type = row2[i] != null ? row2[i].Trim() : "";
                var options = row3[i] != null ? row3[i].Trim() : "";
                
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
                    continue;
                
                if (!IsValidIdentifier(name))
                    throw new InvalidOperationException(string.Format("Invalid column name '{0}' at column {1} in {2}/{3}", name, i + 1, fileName, sheetName));
                
                if (!IsValidType(type))
                    throw new InvalidOperationException(string.Format("Unknown type '{0}' for column '{1}' at column {2} in {3}/{4}", type, name, i + 1, fileName, sheetName));
                
                bool isKey;
                string parserType;
                bool optional;
                ParseOptions(options, name, fileName, sheetName, i, out isKey, out parserType, out optional);
                
                if (isKey)
                {
                    keyCount++;
                    if (keyCount > 1)
                        throw new InvalidOperationException(string.Format("Multiple key columns found in {0}/{1}. Composite keys are not supported.", fileName, sheetName));
                }
                
                var col = new ColumnSpec();
                col.Name = name;
                col.Type = type;
                col.Optional = optional;
                col.IsKey = isKey;
                col.ParserType = parserType;
                col.ColIndex = i;
                
                schema.Columns.Add(col);
                if (isKey) schema.KeyColumnName = name;
            }
            
            if (schema.Columns.Count == 0)
                throw new InvalidOperationException(string.Format("No valid columns found in header of {0}/{1}", fileName, sheetName));
            
            schema.HasKey = keyCount > 0;
            return schema;
        }
        
        private static void ParseOptions(string options, string colName, string fileName, string sheetName, int colIndex, out bool isKey, out string parserType, out bool optional)
        {
            isKey = false;
            parserType = null;
            optional = false;
            
            if (string.IsNullOrWhiteSpace(options))
                return;
            
            var parts = options.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Trim().Split(new char[] { ':' }, 2);
                if (kv.Length != 2)
                    throw new InvalidOperationException(string.Format("Invalid option format '{0}' at column {1} ({2}) in {3}/{4}", part, colIndex + 1, colName, fileName, sheetName));
                
                var key = kv[0].Trim().ToLowerInvariant();
                var value = kv[1].Trim().ToLowerInvariant();
                
                switch (key)
                {
                    case "key":
                        isKey = value == "true";
                        break;
                    case "parser":
                        if (value == "json") parserType = "json";
                        else throw new InvalidOperationException(string.Format("Invalid parser '{0}' at column {1} in {2}/{3}", value, colIndex + 1, fileName, sheetName));
                        break;
                    case "optional":
                        optional = value == "true";
                        break;
                    default:
                        throw new InvalidOperationException(string.Format("Unknown option '{0}' at column {1} in {2}/{3}", key, colIndex + 1, fileName, sheetName));
                }
            }
        }
        
        private static bool IsValidType(string type)
        {
            if (PrimitiveTypes.Contains(type)) return true;
            if (type.StartsWith("enum:") && type.Length > 5) return true;
            if (type.StartsWith("class:") && type.Length > 6) return true;
            return false;
        }
        
        private static bool IsValidIdentifier(string name) 
        { 
            return Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"); 
        }
    }
}
