using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Devian.Tools.Models;

namespace Devian.Tools.Generators
{
    public static class TableJsonGenerator
    {
        public static string Generate(TableSchema schema, IReadOnlyList<Dictionary<string, object>> rows, TablesConfig options)
        {
            JsonNode output;
            
            if (options.OutputShape == "map" && schema.KeyColumnName != null)
            {
                var map = new JsonObject();
                var sortedRows = rows.OrderBy(r => r[schema.KeyColumnName] != null ? r[schema.KeyColumnName].ToString() : "").ToList();
                foreach (var row in sortedRows)
                {
                    var key = row[schema.KeyColumnName] != null ? row[schema.KeyColumnName].ToString() : "";
                    map[key] = RowToJsonObject(row, schema);
                }
                output = map;
            }
            else
            {
                var array = new JsonArray();
                foreach (var row in rows)
                    array.Add(RowToJsonObject(row, schema));
                output = array;
            }
            
            var jsonOptions = new JsonSerializerOptions { WriteIndented = options.Json.Pretty };
            return output.ToJsonString(jsonOptions);
        }
        
        private static JsonObject RowToJsonObject(Dictionary<string, object> row, TableSchema schema)
        {
            var obj = new JsonObject();
            foreach (var col in schema.Columns)
            {
                object value;
                if (row.TryGetValue(col.Name, out value))
                    obj[col.Name] = ValueToJsonNode(value, col.Type);
            }
            return obj;
        }
        
        private static JsonNode ValueToJsonNode(object value, string type)
        {
            if (value == null) return null;
            
            if (type.StartsWith("class:"))
            {
                var jsonStr = value.ToString();
                if (string.IsNullOrWhiteSpace(jsonStr)) return null;
                return JsonNode.Parse(jsonStr);
            }
            
            if (type.StartsWith("enum:"))
                return JsonValue.Create(value.ToString());
            
            switch (type)
            {
                case "string": return JsonValue.Create(value.ToString());
                case "int": return JsonValue.Create(Convert.ToInt32(value));
                case "float": return JsonValue.Create(Convert.ToSingle(value));
                case "bool": return JsonValue.Create(Convert.ToBoolean(value));
                case "json": return JsonNode.Parse(value.ToString());
                case "string[]":
                    var strArr = value as string[];
                    if (strArr != null) return new JsonArray(strArr.Select(s => JsonValue.Create(s)).ToArray());
                    return null;
                case "int[]":
                    var intArr = value as int[];
                    if (intArr != null) return new JsonArray(intArr.Select(i => JsonValue.Create(i)).ToArray());
                    return null;
                case "float[]":
                    var floatArr = value as float[];
                    if (floatArr != null) return new JsonArray(floatArr.Select(f => JsonValue.Create(f)).ToArray());
                    return null;
                case "bool[]":
                    var boolArr = value as bool[];
                    if (boolArr != null) return new JsonArray(boolArr.Select(b => JsonValue.Create(b)).ToArray());
                    return null;
                default: return JsonValue.Create(value.ToString());
            }
        }
        
        public static object ParseCellValue(string raw, ColumnSpec col, TablesConfig options, string fileName, string sheetName, int rowIndex)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (col.Optional) return null;
                if (options.EmptyPolicy == "strict")
                    throw new InvalidOperationException(string.Format("Missing required value at row {0}, column '{1}' in {2}/{3}", rowIndex, col.Name, fileName, sheetName));
                return GetDefaultValue(col.Type);
            }
            
            if (col.Type.StartsWith("class:"))
                return ParseClassCell(raw, col.Type, fileName, sheetName, rowIndex, col.Name);
            
            if (col.Type.StartsWith("enum:"))
                return raw.Trim();
            
            try
            {
                switch (col.Type)
                {
                    case "string": return raw;
                    case "int": return int.Parse(raw);
                    case "float": return float.Parse(raw, CultureInfo.InvariantCulture);
                    case "bool": return ParseBool(raw);
                    case "json": return raw;
                    case "string[]": return raw.Split(new string[] { options.ArraySeparator }, StringSplitOptions.None).Select(s => s.Trim()).ToArray();
                    case "int[]": return ParseIntArray(raw, options.ArraySeparator);
                    case "float[]": return ParseFloatArray(raw, options.ArraySeparator);
                    case "bool[]": return raw.Split(new string[] { options.ArraySeparator }, StringSplitOptions.None).Select(s => ParseBool(s.Trim())).ToArray();
                    default: return raw;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Parse error at row {0}, column '{1}' in {2}/{3}: {4}", rowIndex, col.Name, fileName, sheetName, ex.Message));
            }
        }
        
        private static object ParseClassCell(string raw, string type, string fileName, string sheetName, int rowIndex, string colName)
        {
            var trimmed = raw.Trim();
            var isArray = type.EndsWith("[]");
            
            if (isArray)
            {
                if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
                    throw new InvalidOperationException(string.Format("class array must be JSON array at row {0}, column '{1}' in {2}/{3}", rowIndex, colName, fileName, sheetName));
            }
            else
            {
                if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                    throw new InvalidOperationException(string.Format("class must be JSON object at row {0}, column '{1}' in {2}/{3}", rowIndex, colName, fileName, sheetName));
            }
            
            try
            {
                using (var doc = JsonDocument.Parse(trimmed))
                {
                    return trimmed;
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("Invalid JSON at row {0}, column '{1}' in {2}/{3}: {4}", rowIndex, colName, fileName, sheetName, ex.Message));
            }
        }
        
        private static int[] ParseIntArray(string raw, string separator)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try { return JsonSerializer.Deserialize<int[]>(trimmed); }
                catch { }
            }
            return raw.Split(new string[] { separator }, StringSplitOptions.None).Select(s => int.Parse(s.Trim())).ToArray();
        }
        
        private static float[] ParseFloatArray(string raw, string separator)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try { return JsonSerializer.Deserialize<float[]>(trimmed); }
                catch { }
            }
            return raw.Split(new string[] { separator }, StringSplitOptions.None).Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
        }
        
        private static bool ParseBool(string raw)
        {
            var lower = raw.ToLowerInvariant();
            switch (lower)
            {
                case "true":
                case "1":
                case "yes": return true;
                case "false":
                case "0":
                case "no": return false;
                default: throw new FormatException(string.Format("Cannot parse '{0}' as bool", raw));
            }
        }
        
        private static object GetDefaultValue(string type)
        {
            if (type.StartsWith("class:") || type.StartsWith("enum:")) return null;
            switch (type)
            {
                case "string": return "";
                case "int": return 0;
                case "float": return 0f;
                case "bool": return false;
                case "json": return null;
                case "string[]": return Array.Empty<string>();
                case "int[]": return Array.Empty<int>();
                case "float[]": return Array.Empty<float>();
                case "bool[]": return Array.Empty<bool>();
                default: return null;
            }
        }
    }
}
