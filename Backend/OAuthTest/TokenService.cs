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

        public async Task<string?> GetToken(string tokenEndpoint, string clientId, string certificateThumbprint, string kid)
        {
            if (string.IsNullOrWhiteSpace(tokenEndpoint) ||
                !Uri.TryCreate(tokenEndpoint, UriKind.Absolute, out var endpointUri) ||
                endpointUri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Enter a valid HTTPS token endpoint.", nameof(tokenEndpoint));
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID must not be empty.", nameof(clientId));
            if (string.IsNullOrWhiteSpace(certificateThumbprint))
                throw new ArgumentException("Certificate thumbprint must not be empty.", nameof(certificateThumbprint));

            var creds = GetSigningCredentials(certificateThumbprint);
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

            // KID is independent from the certificate thumbprint and has no default.
            // A blank KID means no kid header is sent.
            jwt.Header.Remove("kid");
            if (!string.IsNullOrWhiteSpace(kid))
                jwt.Header["kid"] = kid;
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
                        
            using var response = await _httpClient.Value.PostAsync(tokenEndpoint, payload);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine(responseContent);
            }

            return null;
        }

     private static X509SigningCredentials GetSigningCredentials(string certificateThumbprint)
     {
         // The thumbprint comes from the OAuth Token page, never from the environment.
         using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
         store.Open(OpenFlags.ReadOnly);
         var signingCert = store.Certificates
             .OfType<X509Certificate2>()
             .FirstOrDefault(x => string.Equals(
                 x.Thumbprint?.Replace(" ", ""),
                 certificateThumbprint.Replace(" ", ""),
                 StringComparison.OrdinalIgnoreCase));

         if (signingCert is null)
             throw new InvalidOperationException(
                 $"Signing certificate {certificateThumbprint} not found in LocalMachine/My.");
         if (!signingCert.HasPrivateKey)
             throw new InvalidOperationException("Signing certificate has no private key.");

         return new X509SigningCredentials(signingCert, SecurityAlgorithms.RsaSha256);
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
