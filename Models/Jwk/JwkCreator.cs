using System.IO;

using Microsoft.IdentityModel.Tokens;

using NavigationMVVM.Models.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using CertTool.Models.Dto;
using Formatting = Newtonsoft.Json.Formatting;

namespace CertTool.Models.Jwk
{
    public class JwkCreator
    {
        private readonly JsonSerializerSettings _jsonFormatter;
        public JwkCreator()
        {
            _jsonFormatter = JsonFormatter();
        }

        public JwkResponse CreateJwk(string pubKey, string kid, string rsaKey)
        {

            var jwk = "";

            try
            {
                using (var textReader = new StringReader(pubKey))
                {
                    var pubKeyPemReader = new PemReader(textReader);
                    var pemObject = pubKeyPemReader.ReadObject();
                    var keyParameters = (RsaKeyParameters)pemObject;
                    var e = Base64UrlEncoder.Encode(keyParameters.Exponent.ToByteArrayUnsigned());
                    var n = Base64UrlEncoder.Encode(keyParameters.Modulus.ToByteArrayUnsigned());

                    var jsonWebKey = new DigDirJwk()
                    {

                        Kid = kid.ToUpper(),
                        Kty = getKty(pemObject),
                        E = e,
                        N = n,
                        Use = "sig",
                        Alg = rsaKey
                    };

                    jwk = JsonConvert.SerializeObject(jsonWebKey, _jsonFormatter);

                    return new JwkResponse { DigDirJwkString = jwk, Success = true, ErrorCode = null };

                }
            }
            catch (Exception)
            {
                return new JwkResponse { DigDirJwkString = null, Success = false, ErrorCode = "Kunne ikke konvertere PEM fil til JWK. \n Sjekk format på fil!" };
            }
        }


        public bool SaveToFile(string content, string filePath)
        {
            File.WriteAllText(filePath, content);
            return true;
        }

        private string getKty(object pemObject)
        {
            if (pemObject is RsaKeyParameters)
                return "RSA";
            if (pemObject is ECPublicKeyParameters)
                return "EC (Elliptic Curve)";
            if (pemObject is DsaPublicKeyParameters)
                return "DSA";

            return "None";
        }

        private JsonSerializerSettings JsonFormatter()
        {
            var formatter = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
            };

            return formatter;
        }
    }
}
