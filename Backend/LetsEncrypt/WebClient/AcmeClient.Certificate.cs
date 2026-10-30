
using CertTool.Backend.LetsEncrypt.Entities;
using CertTool.Backend.LetsEncrypt.Jws;


namespace CertTool.Backend.LetsEncrypt.WebClient
{
    public partial class AcmeClient
    {
        // Public Methods

        public async Task<Certificate> GenerateCertificateAsync(
            Account account,
            Order order,
            string certificateCommonName)
        {
            // Preserve the original order URL before refreshing the order.
            var orderLocation = order.Location;

            // Load fresh order.
            order = await GetOrderAsync(account, orderLocation);

            // Verify status.
            if (order.Status != OrderStatus.Ready)
            {
                throw new InvalidOperationException(
                    "Order must be Ready before finalization!");
            }

            // Initialize builder
            var cert = new Certificate();


            var ccc = order.Identifiers.Select(i => i.Value).ToList();

            // Generate certificate request
            byte[] request = cert.CreateSigningRequest(certificateCommonName, order.Identifiers.Select(i => i.Value).ToList());

            // Preserve the order URL: ACME finalize responses need not include Location.
            order = await Finalize(account, order, request);

            for (var attempt = 0; attempt < 40 && order.Status != OrderStatus.Valid; attempt++)
            {
                if (order.Status == OrderStatus.Invalid)
                    throw new InvalidOperationException("ACME rejected the certificate order.");
                await Task.Delay(TimeSpan.FromSeconds(3));
                order = await GetOrderAsync(account, orderLocation);
            }
            if (order.Status != OrderStatus.Valid || order.Certificate == null)
                throw new TimeoutException("ACME certificate order did not become valid within 2 minutes.");

            // Download signed certificate
            var certificateChainPem = await Download(account, order);

            cert.AddChain(certificateChainPem);

            return cert;
        }

        public async Task RevokeCertificateAsync(Certificate certificate, RevocationReason reason = RevocationReason.Unspecified)
        {
            var certificateRevocation = new CertificateRevocation
            {
                Certificate = JwsConvert.ToBase64String(certificate.GetOriginalCertificate()),
                Reason = reason
            };

            var directory = await GetDirectoryAsync();
            var nonce = await GetNonceAsync();

            var signedData = new JwsSigner(certificate.Key).Sign(certificateRevocation, url: directory.RevokeCert, nonce: nonce);
            var result = await PostAsync<Empty>(directory.RevokeCert, signedData);
        }

        // Private Methods

        private async Task<Order> Finalize(Account account, Order order, byte[] cert)
        {
            var nonce = await GetNonceAsync();

            var orderCert = new OrderCertificate() { Csr = JwsConvert.ToBase64String(cert) };
            var signedData = account.Signer.Sign(orderCert, account.Location, order.Finalize, nonce);
            return await PostAsync<Order>(order.Finalize, signedData);
        }

        private async Task<CertificateChain> Download(Account account, Order order)
        {
            var nonce = await GetNonceAsync();

            var signedData = account.Signer.Sign(null, account.Location, order.Certificate, nonce);

            try
            {
                var result = await PostAsync<CertificateChain>(order.Certificate, signedData);
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to download certificate chain.", ex);
            }
            //return await PostAsync<CertificateChain>(order.Certificate, signedData);
        }
    }
}