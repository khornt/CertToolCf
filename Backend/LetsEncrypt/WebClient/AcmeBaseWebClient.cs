using System.Net.Http;
using System.Text;

using LetsEncrypt.Client.Json;
using Newtonsoft.Json;
using CertTool.Backend.LetsEncrypt.Entities;
using AcmeDirectory = CertTool.Backend.LetsEncrypt.Entities.Directory;



namespace CertTool.Backend.LetsEncrypt.WebClient
{
    public class AcmeBaseWebClient
    {
        #region Consts + Fields + Properties

        private const string RESPONCE_HEADER_KEY_NONCE = "Replay-Nonce";
        private const string MIME_TYPE_JOSE_JSON = "application/jose+json";

        private readonly static JsonSerializerSettings jsonSettings = JsonSettings.CreateSettings();
        private readonly static Lazy<HttpClient> _httpClient = new Lazy<HttpClient>(() => new HttpClient());

        private readonly Uri _directoryUri;

        private HttpClient Http { get => _httpClient.Value; }

        private AcmeDirectory? Directory { get; set; }
        private string? Nonce { get; set; }

        #endregion Consts + Fields + Properties

        // Ctor

        public AcmeBaseWebClient(Uri directoryUri)
        {
            _directoryUri = directoryUri;
        }

        // Protected Methods

        protected async Task<T> GetAsync<T>(Uri uri) where T : BaseEntity, new()
        {
            
            using (var response = await Http.GetAsync(uri))
            {
                return await ProcessResponseAsync<T>(response);
            }
        }

        protected async Task<T> PostAsync<T>(Uri uri, object data) where T : BaseEntity, new()
        {

            var dataJson = JsonConvert.SerializeObject(data, Formatting.None, jsonSettings);
            var content = new StringContent(dataJson, Encoding.UTF8, MIME_TYPE_JOSE_JSON);


            if (content.Headers.ContentType is not null) content.Headers.ContentType.CharSet = null;
            using (var response = await Http.PostAsync(uri, content))
            {
                return await ProcessResponseAsync<T>(response);
            }
        }

        protected async Task<AcmeDirectory> GetDirectoryAsync()
        {
            if (Directory == null)
            {
                await InitAsync();
            }
            return Directory ?? throw new InvalidOperationException("ACME directory not initialized");
        }

        protected async Task<string> GetNonceAsync()
        {
            if (string.IsNullOrEmpty(Nonce))
            {
                await InitAsync();
            }
            return Nonce ?? throw new InvalidOperationException("ACME nonce not initialized");
        }

        // Private Methods

        private async Task InitAsync()
        {
            if (Directory == null)
            {
                Directory = await GetDirectoryInternalAsync();
            }

            if (Nonce == null)
            {
                Nonce = await GetNonceInternalAsync();
            }
        }   

        private async Task<AcmeDirectory> GetDirectoryInternalAsync()
        {
            using (var response = await Http.GetAsync(_directoryUri))
            {
                var directory = await ProcessResponseAsync<AcmeDirectory>(response);
                if (directory.NewNonce is null || directory.NewAccount is null ||
                    directory.NewOrder is null || directory.RevokeCert is null ||
                    directory.KeyChange is null)
                    throw new InvalidOperationException("ACME directory response is missing required endpoints.");
                return directory;
            }
        }

        private async Task<string> GetNonceInternalAsync()
        {
            
            using var response = await Http.SendAsync(new HttpRequestMessage
            {
                RequestUri = (await GetDirectoryAsync()).NewNonce,
                Method = HttpMethod.Head,
            });

            if (!response.Headers.TryGetValues(RESPONCE_HEADER_KEY_NONCE, out var values))
            {
                throw new Exception("Retrieval of new nonce failed! Url: " + (await GetDirectoryAsync()).NewNonce);
            }

            return values.FirstOrDefault() ?? throw new InvalidOperationException("Empty ACME nonce");
        }

        private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response) where T : BaseEntity, new()
        {
            var entity = new T();
            var error = default(AcmeError);

            if (response.Headers.Contains(RESPONCE_HEADER_KEY_NONCE))
            {
                Nonce = response.Headers.GetValues(RESPONCE_HEADER_KEY_NONCE).FirstOrDefault();
            }

            var content = await response.Content.ReadAsStringAsync();

            if (IsJsonMediaType(response.Content?.Headers.ContentType?.MediaType))
            {
                if (response.IsSuccessStatusCode)
                {
                    entity = JsonConvert.DeserializeObject<T>(content)
                        ?? throw new JsonSerializationException("Empty ACME response entity.");
                }
                else
                {
                    error = JsonConvert.DeserializeObject<AcmeError>(content);
                }
            }
            else
            {
                entity.UnknownContent = content;
            }

            

            entity.Location = response.Headers.Location;
            entity.Error = error;

            if (entity.Error != null)
            {
                throw new Exception("Error: " + entity.Error.Detail);
            }

            return entity;
        }

        private static bool IsJsonMediaType(string? mediaType)
        {
            if (mediaType != null && mediaType.StartsWith("application/"))
            {
                return mediaType
                    .Substring("application/".Length)
                    .Split('+')
                    .Any(t => t == "json");
            }

            return false;
        }
    }
}