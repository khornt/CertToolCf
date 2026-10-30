using Newtonsoft.Json;

namespace LetsEncrypt.Client.Jws
{
    public class RsaJsonWebKey
    {
        [JsonProperty("e", Order = 1)]
        public string Exponent { get; set; } = string.Empty;

        [JsonProperty("kty", Order = 2)]
        public string KeyType { get; set; } = string.Empty;

        [JsonProperty("n", Order = 3)]
        public string Modulus { get; set; } = string.Empty;
    }
}