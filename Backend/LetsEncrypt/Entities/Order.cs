using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CertTool.Backend.LetsEncrypt.Entities
{
    public class Order : BaseEntity
    {
        [JsonProperty("status")]
        public OrderStatus? Status { get; set; }

        [JsonProperty("expires")]
        public DateTime? Expires { get; set; }

        public IList<Identifier> Identifiers { get; set; } = new List<Identifier>();

        [JsonProperty("notBefore")]
        public DateTime? NotBefore { get; set; }

        [JsonProperty("notAfter")]
        public DateTime? NotAfter { get; set; }

        [JsonProperty("authorizations")]
        public List<Uri> Authorizations { get; set; } = new List<Uri>();

        [JsonProperty("finalize")]
        public Uri Finalize { get; set; } = null!;

        [JsonProperty("certificate")]
        public Uri? Certificate { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OrderStatus
    {
        [EnumMember(Value = "pending")]
        Pending,

        [EnumMember(Value = "ready")]
        Ready,

        [EnumMember(Value = "processing")]
        Processing,

        [EnumMember(Value = "valid")]
        Valid,

        [EnumMember(Value = "invalid")]
        Invalid,
    }

    public class OrderCertificate : Order
    {
        [JsonProperty("csr")]
        public string Csr { get; set; } = string.Empty;
    }
}