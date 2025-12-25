using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Devian.Tools.Models;
using Devian.Tools.Generators;

namespace Devian.Tools
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Devian.Tools v5");
            
            if (args.Length == 0)
            {
                PrintUsage();
                return 0;
            }
            
            var command = args[0].ToLowerInvariant();
            
            try
            {
                switch (command)
                {
                    case "build":
                        RunBuild(args.Skip(1).ToArray());
                        return 0;
                    case "validate":
                        RunValidate(args.Skip(1).ToArray());
                        return 0;
                    case "help":
                        PrintUsage();
                        return 0;
                    default:
                        Console.WriteLine(string.Format("Unknown command: {0}", command));
                        PrintUsage();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("ERROR: {0}", ex.Message));
                return 1;
            }
        }
        
        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  devian build [--config path]   Build all domains");
            Console.WriteLine("  devian validate [--dir path]   Validate generated files");
            Console.WriteLine("  devian help                    Show this help");
        }
        
        private static void RunValidate(string[] args)
        {
            var dir = "modules/cs";
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--dir" && i + 1 < args.Length)
                {
                    dir = args[i + 1];
                    i++;
                }
            }
            
            Console.WriteLine(string.Format("Validating generated files in: {0}", dir));
            NamespaceGuard.ValidateGeneratedFiles(dir);
        }
        
        private static void RunBuild(string[] args)
        {
            var configPath = "input/build/build.json";
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--config" && i + 1 < args.Length)
                {
                    configPath = args[i + 1];
                    i++;
                }
            }
            
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(string.Format("Config not found: {0}", configPath));
            }
            
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<BuildConfig>(json);
            
            if (config == null)
            {
                throw new InvalidOperationException("Failed to parse config");
            }
            
            var deprecated = config.GetDeprecatedFieldsUsed();
            if (deprecated.Count > 0)
            {
                throw new InvalidOperationException(
                    string.Format("Deprecated fields used: {0}", string.Join(", ", deprecated)));
            }
            
            Console.WriteLine(string.Format("Build config v{0}", config.Version));
            Console.WriteLine(string.Format("InputDir: {0}", config.InputDir));
            Console.WriteLine(string.Format("TempDir: {0}", config.TempDir));
            Console.WriteLine(string.Format("Domains: {0}", string.Join(", ", config.Domains.Keys)));
            
            foreach (var kvp in config.Domains)
            {
                var domainName = kvp.Key;
                var domainConfig = kvp.Value;
                
                Console.WriteLine(string.Format("\n=== Building domain: {0} ===", domainName));
                Console.WriteLine(string.Format("    C# Namespace: {0}", DomainNamespace.GetCSharpNamespace(domainName)));
                
                var domainDeprecated = domainConfig.GetDeprecatedFieldsUsed();
                if (domainDeprecated.Count > 0)
                {
                    throw new InvalidOperationException(
                        string.Format("Deprecated fields in domain '{0}': {1}", domainName, string.Join(", ", domainDeprecated)));
                }
                
                var inputBase = Path.Combine(config.InputDir, domainName);
                
                // Contracts 생성
                var contractsDir = Path.Combine(inputBase, "contracts");
                if (Directory.Exists(contractsDir))
                {
                    BuildContracts(contractsDir, domainName, domainConfig);
                }
                
                // Protocols 생성
                var protocolsDir = Path.Combine(inputBase, "protocols");
                if (Directory.Exists(protocolsDir))
                {
                    BuildProtocols(protocolsDir, domainName, domainConfig);
                }
                
                // Tables 생성
                var tablesDir = Path.Combine(inputBase, "tables");
                if (Directory.Exists(tablesDir))
                {
                    BuildTables(tablesDir, domainName, domainConfig);
                }
            }
            
            // 빌드 후 가드레일 검사
            Console.WriteLine("\n=== Post-build validation ===");
            foreach (var kvp in config.Domains)
            {
                foreach (var csDir in kvp.Value.CsTargetDirs)
                {
                    if (Directory.Exists(csDir))
                    {
                        NamespaceGuard.ValidateGeneratedFiles(csDir);
                    }
                }
            }
            
            Console.WriteLine("\nBuild completed successfully.");
        }
        
        private static void BuildContracts(string contractsDir, string domain, DomainConfig domainConfig)
        {
            Console.WriteLine(string.Format("  Contracts: {0}", contractsDir));
            
            var files = Directory.GetFiles(contractsDir, "*.json");
            foreach (var file in files)
            {
                var json = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<ContractSpec>(json);
                
                if (spec == null) continue;
                
                // Enum 생성
                foreach (var enumSpec in spec.Enums)
                {
                    var csCode = CSharpContractGenerator.GenerateEnum(enumSpec, domain);
                    var tsCode = TypeScriptContractGenerator.GenerateEnum(enumSpec);
                    
                    WriteToTargets(domainConfig.CsTargetDirs, enumSpec.Name + ".g.cs", csCode);
                    WriteToTargets(domainConfig.TsTargetDirs, enumSpec.Name + ".g.ts", tsCode);
                    
                    Console.WriteLine(string.Format("    Generated: {0} (enum)", enumSpec.Name));
                }
                
                // Class 생성
                foreach (var classSpec in spec.Classes)
                {
                    var csCode = CSharpContractGenerator.GenerateClass(classSpec, domain);
                    var tsCode = TypeScriptContractGenerator.GenerateInterface(classSpec);
                    
                    WriteToTargets(domainConfig.CsTargetDirs, classSpec.Name + ".g.cs", csCode);
                    WriteToTargets(domainConfig.TsTargetDirs, classSpec.Name + ".g.ts", tsCode);
                    
                    Console.WriteLine(string.Format("    Generated: {0} (class)", classSpec.Name));
                }
            }
        }
        
        private static void BuildProtocols(string protocolsDir, string domain, DomainConfig domainConfig)
        {
            Console.WriteLine(string.Format("  Protocols: {0}", protocolsDir));
            
            var files = Directory.GetFiles(protocolsDir, "*.json");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<ProtocolSpec>(json);
                
                if (spec == null) continue;
                
                // 파일명에서 namespace 결정
                spec.Namespace = fileName;
                
                var csCode = CSharpProtocolGenerator.Generate(spec, domain);
                var tsCode = TypeScriptProtocolGenerator.Generate(spec);
                
                WriteToTargets(domainConfig.CsTargetDirs, fileName + ".g.cs", csCode);
                WriteToTargets(domainConfig.TsTargetDirs, fileName + ".g.ts", tsCode);
                
                Console.WriteLine(string.Format("    Generated: {0} (protocol, {1} messages)", fileName, spec.Messages.Count));
            }
        }
        
        private static void WriteToTargets(List<string> targetDirs, string fileName, string content)
        {
            foreach (var dir in targetDirs)
            {
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, fileName);
                File.WriteAllText(path, content, Encoding.UTF8);
            }
        }
        
        private static void BuildTables(string tablesDir, string domain, DomainConfig domainConfig)
        {
            Console.WriteLine(string.Format("  Tables: {0}", tablesDir));
            
            // tables.json 메타데이터 파일 찾기
            var tablesJsonPath = Path.Combine(tablesDir, "tables.json");
            if (!File.Exists(tablesJsonPath))
            {
                Console.WriteLine("    [SKIP] No tables.json found");
                return;
            }
            
            var json = File.ReadAllText(tablesJsonPath);
            var tablesConfig = JsonSerializer.Deserialize<TablesMetaConfig>(json);
            
            if (tablesConfig == null || tablesConfig.Tables == null || tablesConfig.Tables.Count == 0)
            {
                Console.WriteLine("    [SKIP] No tables defined in tables.json");
                return;
            }
            
            // TableSchema 목록 생성
            var schemas = new List<TableSchema>();
            foreach (var tableMeta in tablesConfig.Tables)
            {
                var schema = new TableSchema
                {
                    FileName = tableMeta.FileName,
                    SheetName = tableMeta.Name,
                    KeyColumnName = tableMeta.KeyColumn,
                    HasKey = !string.IsNullOrEmpty(tableMeta.KeyColumn),
                    Columns = tableMeta.Columns.Select(c => new ColumnSpec
                    {
                        Name = c.Name,
                        Type = c.Type,
                        IsKey = c.IsKey,
                        Optional = c.Optional
                    }).ToList()
                };
                schemas.Add(schema);
                
                // Row 타입 생성 (C# + TS)
                var csRowCode = CSharpTableGenerator.Generate(schema, domain);
                var tsRowCode = TypeScriptTableGenerator.Generate(schema);
                
                WriteToTargets(domainConfig.CsTargetDirs, tableMeta.Name + "Row.g.cs", csRowCode);
                WriteToTargets(domainConfig.TsTargetDirs, tableMeta.Name + "Row.g.ts", tsRowCode);
                
                Console.WriteLine(string.Format("    Generated: {0}Row (table row)", tableMeta.Name));
            }
            
            // 테이블 로더 생성 (C#)
            var csLoaderCode = TableLoaderCodegen.GenerateCSharpLoader(schemas, domain);
            WriteToTargets(domainConfig.CsTargetDirs, "Table.g.cs", csLoaderCode);
            
            // 테이블 로더 생성 (TS - legacy)
            var tsLoaderCode = TableLoaderCodegen.GenerateTypeScriptLoader(schemas, domain);
            WriteToTargets(domainConfig.TsTargetDirs, "Table.g.ts", tsLoaderCode);
            
            // 테이블 캐시 로더 생성 (TS - 신규)
            var tsCacheLoaderCode = TableLoaderCodegen.GenerateTypeScriptCacheLoader(schemas, domain);
            WriteToTargets(domainConfig.TsTargetDirs, "tables.loader.g.ts", tsCacheLoaderCode);
            
            Console.WriteLine(string.Format("    Generated: Table.g (loader, {0} tables)", schemas.Count));
            Console.WriteLine("    Generated: tables.loader.g.ts (cache loader)");
        }
    }
    
    // 테이블 메타데이터 JSON 모델
    public sealed class TablesMetaConfig
    {
        [JsonPropertyName("tables")]
        public List<TableMetaItem> Tables { get; set; }
        
        public TablesMetaConfig()
        {
            Tables = new List<TableMetaItem>();
        }
    }
    
    public sealed class TableMetaItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("fileName")]
        public string FileName { get; set; }
        
        [JsonPropertyName("keyColumn")]
        public string KeyColumn { get; set; }
        
        [JsonPropertyName("columns")]
        public List<ColumnMetaItem> Columns { get; set; }
        
        public TableMetaItem()
        {
            Name = "";
            FileName = "";
            Columns = new List<ColumnMetaItem>();
        }
    }
    
    public sealed class ColumnMetaItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("isKey")]
        public bool IsKey { get; set; }
        
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }
        
        public ColumnMetaItem()
        {
            Name = "";
            Type = "";
        }
    }
}
