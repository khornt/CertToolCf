using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CertTool.Models.Dto.Token
{
    using Newtonsoft.Json;
    using System.Text.Json.Serialization;

    public class TokenResponseDto
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = default!;

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("refresh_expires_in")]
        public int RefreshExpiresIn { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = default!;

        [JsonProperty("not-before-policy")]
        public int NotBeforePolicy { get; set; }        
    }
}
