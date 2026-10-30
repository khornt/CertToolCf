using System.Security.Cryptography.X509Certificates;
using CertTool.Backend.LetsEncrypt.Cryptography;

namespace CertTool.Backend.LetsEncrypt.Entities
{
    public class Certificate
    {
        #region Consts + Fields + Properties

        private CertificateChain? _certificateChain;

        public RsaKeyPair Key { get; private set; }

        #endregion Consts + Fields + Properties

        // Ctor

        public Certificate(RsaKeyPair? key = null)
        {
            // Generate new RSA key for certificate
            if (key == null)
            {
                Key = RsaKeyPair.New();
            }
            else
            {
                Key = key;
            }
        }

        private CertificateChain RequireChain() => _certificateChain ?? throw new InvalidOperationException("Certificate chain has not been loaded.");

        // Public Methods

        public byte[] CreateSigningRequest(string cn, List<string> subjectAlternativeNames)
        {
            return CertificateBuilder.CreateSigningRequest(Key.ToRSA(), cn, subjectAlternativeNames);
        }

        public void AddChain(CertificateChain certificateChain)
        {
            _certificateChain = certificateChain;
        }

        public byte[] GetOriginalCertificate()
        {
            return RequireChain().CertificateBytes;
        }

        public byte[] GeneratePfx(string password)
        {                      

            return CertificateBuilder.Generate(Key.ToRSA(), RequireChain(), password, X509ContentType.Pfx);
        }

        public byte[] GenerateCrt(string password)
        {
            return CertificateBuilder.Generate(Key.ToRSA(), RequireChain(), password, X509ContentType.Cert);
        }

        public string GenerateCrtPem(string password)
        {
            return string.Format(
                "-----BEGIN CERTIFICATE-----\n{0}\n-----END CERTIFICATE-----",
                Convert.ToBase64String(GenerateCrt(password)));
        }

        public string GenerateKeyPem()
        {
            return Key.ToPrivateKeyPem();
        }

        public string Serialize()
        {
            return RequireChain().Content;
        }

        public static Certificate Deserialize(string data, RsaKeyPair key)
        {
            var result = new Certificate(key);
            result.AddChain(new CertificateChain(data));
            return result;
        }
    }
}