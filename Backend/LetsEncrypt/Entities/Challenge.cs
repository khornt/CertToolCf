using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CertTool.Backend.LetsEncrypt.Entities
{
    public class Challenge : BaseEntity
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("status")]
        public ChallengeStatus? Status { get; set; }

        [JsonProperty("validated")]
        public DateTime? Validated { get; set; }

        [JsonProperty("url")]
        public Uri Url { get; set; } = null!;

        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        //

        [JsonIgnore]
        public string DnsKey { get; set; } = string.Empty;

        [JsonIgnore]
        public string VerificationKey { get; set; } = string.Empty;

        [JsonIgnore]
        public string VerificationValue { get; set; } = string.Empty;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChallengeStatus
    {
        [JsonProperty("pending")]
        Pending,

        [JsonProperty("processing")]
        Processing,

        [JsonProperty("valid")]
        Valid,

        [JsonProperty("invalid")]
        Invalid,
    }

    public static class ChallengeType
    {
        public const string Http01 = "http-01";

        public const string Dns01 = "dns-01";

        public const string TlsAlpn01 = "tls-alpn-01";
    }
}