using System.IO;
using System.Security.Cryptography.X509Certificates;
using CertTool.Models.Jwk;


namespace CertTool.Models.Jwk
{
    public class PubKeyHandler
    {
        public string GetPublicKey(string filename)
        {
            var pubKey = File.ReadAllText(filename);
            return pubKey;
        }

        public string? GetPublicKeyFromSertificate(string kid)
        {
            // Prefer a portable certificate file when supplied; otherwise use the OS store.
            var certificatePath = Environment.GetEnvironmentVariable("CERTTOOL_CERTIFICATE_FILE");
            if (!string.IsNullOrWhiteSpace(certificatePath) && File.Exists(certificatePath))
            {
                using var imported = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
                if (!string.Equals(imported.Thumbprint, kid, StringComparison.OrdinalIgnoreCase)) return null;
                using var rsa = imported.GetRSAPublicKey();
                return rsa?.ExportRSAPublicKeyPem();
            }
            X509Certificate2 cert;
            
            using (var certificateStore = new X509Store(StoreLocation.LocalMachine))
            {                                               
                certificateStore.Open(OpenFlags.ReadOnly);
                var certificate = certificateStore.Certificates.OfType<X509Certificate2>().FirstOrDefault(x => x.Thumbprint != null && string.Equals(x.Thumbprint, kid, StringComparison.CurrentCultureIgnoreCase));

                if (certificate == null ) return null;

                cert = certificate;
            }
                       

            try
            {
                var rsaPublicKey = cert.PublicKey.GetRSAPublicKey();

                if (rsaPublicKey == null) return null;

                var rsaPublicKeyPem = rsaPublicKey.ExportRSAPublicKeyPem();

                if (!string.IsNullOrEmpty(rsaPublicKeyPem))
                {
                    return rsaPublicKeyPem;
                }
                return "";
            }
            catch (Exception) 
            {
                return null;
            }                                     
        }      
    }
}   

