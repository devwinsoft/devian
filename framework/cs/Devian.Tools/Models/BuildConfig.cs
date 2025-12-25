using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Devian.Tools.Models
{
    public sealed class BuildConfig
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }
        
        [JsonPropertyName("inputDir")]
        public string InputDir { get; set; }
        
        [JsonPropertyName("tempDir")]
        public string TempDir { get; set; }
        
        [JsonPropertyName("domains")]
        public Dictionary<string, DomainConfig> Domains { get; set; }
        
        [JsonPropertyName("inputDirs")]
        public object InputDirs_DEPRECATED { get; set; }
        
        [JsonPropertyName("inputs")]
        public object Inputs_DEPRECATED { get; set; }
        
        [JsonPropertyName("targets")]
        public object Targets_DEPRECATED { get; set; }
        
        [JsonPropertyName("variables")]
        public object Variables_DEPRECATED { get; set; }
        
        [JsonPropertyName("workspace")]
        public object Workspace_DEPRECATED { get; set; }
        
        public BuildConfig()
        {
            Version = "5";
            InputDir = "input";
            TempDir = "temp/devian";
            Domains = new Dictionary<string, DomainConfig>();
        }
        
        public List<string> GetDeprecatedFieldsUsed()
        {
            var deprecated = new List<string>();
            if (InputDirs_DEPRECATED != null) deprecated.Add("inputDirs");
            if (Inputs_DEPRECATED != null) deprecated.Add("inputs");
            if (Targets_DEPRECATED != null) deprecated.Add("targets");
            if (Variables_DEPRECATED != null) deprecated.Add("variables");
            if (Workspace_DEPRECATED != null) deprecated.Add("workspace");
            return deprecated;
        }
    }

    public sealed class DomainConfig
    {
        [JsonPropertyName("dependsOnCommon")]
        public bool DependsOnCommon { get; set; }
        
        [JsonPropertyName("csTargetDirs")]
        public List<string> CsTargetDirs { get; set; }
        
        [JsonPropertyName("tsTargetDirs")]
        public List<string> TsTargetDirs { get; set; }
        
        [JsonPropertyName("dataTargetDirs")]
        public List<string> DataTargetDirs { get; set; }
        
        [JsonPropertyName("contractsFiles")]
        public object ContractsFiles_DEPRECATED { get; set; }
        
        [JsonPropertyName("tablesFiles")]
        public object TablesFiles_DEPRECATED { get; set; }
        
        [JsonPropertyName("protocolFiles")]
        public object ProtocolFiles_DEPRECATED { get; set; }
        
        [JsonPropertyName("protocolNamespaces")]
        public object ProtocolNamespaces_DEPRECATED { get; set; }
        
        public DomainConfig()
        {
            DependsOnCommon = true;
            CsTargetDirs = new List<string>();
            TsTargetDirs = new List<string>();
            DataTargetDirs = new List<string>();
        }
        
        public List<string> GetDeprecatedFieldsUsed()
        {
            var deprecated = new List<string>();
            if (ContractsFiles_DEPRECATED != null) deprecated.Add("contractsFiles");
            if (TablesFiles_DEPRECATED != null) deprecated.Add("tablesFiles");
            if (ProtocolFiles_DEPRECATED != null) deprecated.Add("protocolFiles");
            if (ProtocolNamespaces_DEPRECATED != null) deprecated.Add("protocolNamespaces");
            return deprecated;
        }
    }
}
