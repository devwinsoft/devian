using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Devian.Tools.Models
{
    public sealed class ProtocolSpec
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; }
        
        [JsonPropertyName("direction")]
        public string Direction { get; set; }
        
        [JsonPropertyName("messages")]
        public List<MessageSpec> Messages { get; set; }
        
        public ProtocolSpec()
        {
            Direction = "bidirectional";
            Messages = new List<MessageSpec>();
        }
    }

    public sealed class MessageSpec
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("opcode")]
        public int? Opcode { get; set; }
        
        [JsonPropertyName("fields")]
        public List<FieldSpec> Fields { get; set; }
        
        public MessageSpec()
        {
            Name = "";
            Fields = new List<FieldSpec>();
        }
    }

    public sealed class FieldSpec
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }
        
        public FieldSpec()
        {
            Name = "";
            Type = "";
        }
    }
}
