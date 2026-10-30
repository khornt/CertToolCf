using Newtonsoft.Json;

namespace LetsEncrypt.Client.Jws
{
    public class JwsData
    {
        [JsonProperty("protected")]
        public string Protected { get; set; } = string.Empty;

        [JsonProperty("payload")]
        public string Payload { get; set; } = string.Empty;

        [JsonProperty("signature")]
        public string Signature { get; set; } = string.Empty;
    }
}