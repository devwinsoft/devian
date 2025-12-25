using System;

namespace Devian.Tools.Generators
{
    public static class TypeMapper
    {
        public static string ToCSharp(string idlType, bool optional)
        {
            string baseType;
            bool isArray;
            ExtractArrayInfo(idlType, out baseType, out isArray);
            
            string csType;
            if (baseType.StartsWith("class:"))
                csType = baseType.Substring(6);
            else if (baseType.StartsWith("enum:"))
                csType = baseType.Substring(5);
            else if (baseType.StartsWith("map<"))
                csType = ParseMapType(baseType, ToCSharpMapType);
            else
            {
                switch (baseType)
                {
                    case "string": csType = "string"; break;
                    case "int":
                    case "int32": csType = "int"; break;
                    case "int64": csType = "long"; break;
                    case "float": csType = "float"; break;
                    case "double": csType = "double"; break;
                    case "bool": csType = "bool"; break;
                    case "json": csType = "object"; break;
                    default: csType = baseType; break;
                }
            }
            
            if (isArray) csType += "[]";
            return csType;
        }
        
        public static string ToTypeScript(string idlType, bool optional)
        {
            string baseType;
            bool isArray;
            ExtractArrayInfo(idlType, out baseType, out isArray);
            
            string tsType;
            if (baseType.StartsWith("class:"))
                tsType = baseType.Substring(6);
            else if (baseType.StartsWith("enum:"))
                tsType = baseType.Substring(5);
            else if (baseType.StartsWith("map<"))
                tsType = ParseMapType(baseType, ToTypeScriptMapType);
            else
            {
                switch (baseType)
                {
                    case "string": tsType = "string"; break;
                    case "int":
                    case "int32":
                    case "int64":
                    case "float":
                    case "double": tsType = "number"; break;
                    case "bool": tsType = "boolean"; break;
                    case "json": tsType = "any"; break;
                    default: tsType = baseType; break;
                }
            }
            
            if (isArray) tsType += "[]";
            return tsType;
        }
        
        private static void ExtractArrayInfo(string type, out string baseType, out bool isArray)
        {
            if (type.EndsWith("[]"))
            {
                baseType = type.Substring(0, type.Length - 2);
                isArray = true;
            }
            else
            {
                baseType = type;
                isArray = false;
            }
        }
        
        private static string ParseMapType(string mapType, Func<string, string, string> formatter)
        {
            var inner = mapType.Substring(4, mapType.Length - 5);
            var parts = inner.Split(new char[] { ',' }, 2);
            if (parts.Length == 2)
                return formatter(parts[0].Trim(), parts[1].Trim());
            return mapType;
        }
        
        private static string ToCSharpMapType(string k, string v) 
        { 
            return string.Format("Dictionary<{0}, {1}>", ToCSharp(k, false), ToCSharp(v, false)); 
        }
        
        private static string ToTypeScriptMapType(string k, string v) 
        { 
            return string.Format("Record<{0}, {1}>", ToTypeScript(k, false), ToTypeScript(v, false)); 
        }
        
        public static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
        
        public static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
