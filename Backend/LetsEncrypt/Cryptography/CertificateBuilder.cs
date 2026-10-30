using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CertTool.Backend.LetsEncrypt.Entities;

namespace CertTool.Backend.LetsEncrypt.Cryptography
{
    public static class CertificateBuilder
    {
        // Public Methods

        public static byte[] CreateSigningRequest(RSA rsa, string cn, List<string> subjectAlternativeNames)
        {
            CertificateRequest req = new CertificateRequest($"CN={cn}",
                   rsa,
                   HashAlgorithmName.SHA256,
                   RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true));
            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var sanb = new SubjectAlternativeNameBuilder();
            foreach (var subjectAlternativeName in subjectAlternativeNames)
            {
                sanb.AddDnsName(subjectAlternativeName);
            }
            req.CertificateExtensions.Add(sanb.Build());

            return req.CreateSigningRequest();
        }

        public static byte[] Generate(RSA rsa, CertificateChain certificateChain, string password, X509ContentType certificateType)
        {
            var collection = new X509Certificate2Collection();

            // Reverse
            for (int i = certificateChain.Certificates.Count - 1; i >= 0; i--)
            {
                var tempCert = certificateChain.Certificates[i];
                var cert = X509CertificateLoader.LoadCertificate(tempCert.Bytes);
                if (i == 0)
                {
                    cert = cert.CopyWithPrivateKey(rsa);
                }

                collection.Add(cert);
            }

            return collection.Export(certificateType, password) ?? Array.Empty<byte>();
        }
    }
}