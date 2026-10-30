using LetsEncrypt.Client.Json;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using CertTool.Models.Dto.Token;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;


namespace CertTool.Backend.OAuthTest
{
    public class TokenService
    {
        //private readonly static JsonSerializerSettings jsonSettings = JsonSettings.CreateSettings();
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly static Lazy<HttpClient> _httpClient = new Lazy<HttpClient>(() => new HttpClient());
        public TokenService() 
        {
            _jsonSerializerSettings = JsonFormatter();
        }

        public async Task<string?> GetToken()
        {
            var tokenEndpoint = Environment.GetEnvironmentVariable("CERTTOOL_TOKEN_ENDPOINT") ?? "https://auth.horntvedt.com/realms/master/protocol/openid-connect/token";

            var clientId = Environment.GetEnvironmentVariable("CERTTOOL_CLIENT_ID") ?? "my-api-client";
            
            var creds = GetSigningCredentials();
            var now = DateTimeOffset.UtcNow;

            var jwt = new JwtSecurityToken(
                issuer: clientId,
                audience: tokenEndpoint,
                claims: new[]
                {
                    new Claim("sub", clientId),
                    new Claim("jti", Guid.NewGuid().ToString())
                },
                notBefore: now.UtcDateTime,
                expires: now.AddMinutes(5).UtcDateTime,
                signingCredentials: creds
            );

            jwt.Header["kid"] = Environment.GetEnvironmentVariable("CERTTOOL_CERT_THUMBPRINT") ?? "4A6820E3E9CA31C4696E9672C60D0D7E0B7F3CB4";
            jwt.Header.Remove("x5t");

            var handler = new JwtSecurityTokenHandler();

            string clientAssertion = string.Empty;
            try
            {
                clientAssertion = handler.WriteToken(jwt);
            } catch (Exception ex)
            {
                Console.WriteLine($"Error creating JWT: {ex.Message}");
                return null;
            }   


            var payload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                ["client_assertion"] = clientAssertion
            });
                        
            var response = _httpClient.Value.PostAsync(tokenEndpoint, payload).Result;
            var responseContent = response.Content.ReadAsStringAsync().Result;
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponseDto>(responseContent, _jsonSerializerSettings);
                Console.WriteLine($"Access Token: {tokenResponse.AccessToken}"); 
                return responseContent;
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine(responseContent);
            }

            return null;
        }

     private X509SigningCredentials GetSigningCredentials()
        {
            // Portable option: use a PFX file instead of an OS certificate store.
            var pfxPath = Environment.GetEnvironmentVariable("CERTTOOL_SIGNING_PFX");
            if (!string.IsNullOrWhiteSpace(pfxPath))
            {
                var password = Environment.GetEnvironmentVariable("CERTTOOL_SIGNING_PFX_PASSWORD");
                var cert = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password);
                return new X509SigningCredentials(cert, SecurityAlgorithms.RsaSha256);
            }
            X509Certificate2? signingCert;

            using (var certificateStore = new X509Store(StoreLocation.LocalMachine))
            {

                var thumbprint = Environment.GetEnvironmentVariable("CERTTOOL_CERT_THUMBPRINT") ?? "4a6820e3e9ca31c4696e9672c60d0d7e0b7f3cb4";

                certificateStore.Open(OpenFlags.ReadOnly);

                signingCert = certificateStore.Certificates
                    .FirstOrDefault(x => string.Equals(
                        x.Thumbprint, thumbprint,
                        StringComparison.CurrentCultureIgnoreCase));
                
                certificateStore.Close();
            }

            var signingCred = new X509SigningCredentials(signingCert, SecurityAlgorithms.RsaSha256);
            return signingCred;
        }
    

     private JsonSerializerSettings JsonFormatter()
        {
            var formatter = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
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

//var requestBody = new Dictionary<string, string>
//{
//    { "grant_type", grantType },
//    { "client_id", clientId },
//    { "client_secret", clientSecret }                                
//};

// -------------------------------------------------------------------
