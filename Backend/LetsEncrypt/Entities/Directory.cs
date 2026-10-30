using Newtonsoft.Json;

namespace CertTool.Backend.LetsEncrypt.Entities
{
    public class Directory : BaseEntity
    {
        [JsonProperty("newNonce")]
        public Uri NewNonce { get; set; } = null!;

        [JsonProperty("newAccount")]
        public Uri NewAccount { get; set; } = null!;

        [JsonProperty("newOrder")]
        public Uri NewOrder { get; set; } = null!;

        [JsonProperty("revokeCert")]
        public Uri RevokeCert { get; set; } = null!;

        [JsonProperty("keyChange")]
        public Uri KeyChange { get; set; } = null!;

        [JsonProperty("meta")]
        public DirectoryMeta? Meta { get; set; }
    }

    public class DirectoryMeta
    {
        [JsonProperty("termsOfService")]
        public Uri? TermsOfService { get; set; }

        [JsonProperty("website")]
        public Uri? Website { get; set; }

        [JsonProperty("caaIdentities")]
        public List<string> CaaIdentities { get; set; } = new List<string>();

        [JsonProperty("externalAccountRequired")]
        public bool? ExternalAccountRequired { get; set; }
    }
}