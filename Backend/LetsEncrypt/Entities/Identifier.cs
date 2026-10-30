using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CertTool.Backend.LetsEncrypt.Entities
{
    public class Identifier
    {
        [JsonProperty("type")]
        public IdentifierType Type { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; } = string.Empty;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum IdentifierType
    {
        [EnumMember(Value = "dns")]
        Dns
    }
}