using System.Net;
using Newtonsoft.Json;

namespace CertTool.Backend.LetsEncrypt.Entities
{
    public class BaseEntity
    {
        [JsonIgnore]
        public virtual string UnknownContent { get; set; } = string.Empty;

        [JsonIgnore]
        public virtual Uri Location { get; set; }

        [JsonIgnore]
        public virtual AcmeError? Error { get; set; }
    }

    public class AcmeError
    {
        public string Type { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public HttpStatusCode Status { get; set; }
    }
}