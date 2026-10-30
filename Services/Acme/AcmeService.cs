using CertTool.Backend.LetsEncrypt.Entities;
using CertTool.Backend.LetsEncrypt.WebClient;
using CertTool.Models;
using CertTool.Models.Dto;

namespace CertTool.Services.Acme
{
    public class AcmeService : IAcmeService
    {
        private readonly AcmeClient _acmeClient;
        private readonly AppConfig _config;
        private readonly AcmeAccountRepository _accounts;

        public AcmeService(AcmeClient acmeClient, AppConfig config, AcmeAccountRepository accounts)
        {
            _acmeClient = acmeClient;
            _config = config;
            _accounts = accounts;
        }

        public async Task<ChallengeStore> CreateChallengeAsync(string fqdn, string email, string accountPassphrase)
        {
            var labels = fqdn.Split('.');
            var invalidLabels = labels.Where(label =>
                    label.Length == 0 ||
                    label.Length > 63 ||
                    label.StartsWith('-') ||
                    label.EndsWith('-') ||
                    label.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '-')))
                .ToArray();

            if (fqdn.Length > 253 || labels.Length < 2 || invalidLabels.Length > 0)
            {
                throw new ArgumentException(
                    "Enter a valid fully-qualified domain name.",
                    nameof(fqdn));
            }

            //Få denne dumma ned til en enkelt string hele veien
            List<string> fqdnList = new List<string>() { fqdn };


            var account = await _acmeClient.GetOrCreateAccountAsync(
                email, _accounts, ApiEnvironment.LetsEncryptV2, accountPassphrase);
            
            
            var order = await _acmeClient.NewOrderAsync(account, fqdnList);
            var challenges = await _acmeClient.GetDnsChallenges(account, order);

            var challenge = new ChallengeStore
            {
                Challenges = challenges,
                Account = account,
                Order = order,
            };


            return challenge;
        }

        public async Task<bool> ValidateChallengeAsync(ChallengeStore challengeStore)
        {
            if (challengeStore.Account == null || challengeStore.Order == null)
                throw new InvalidOperationException("ACME account/order is missing.");

            foreach (var item in challengeStore.Challenges)
            {
                var current = await _acmeClient.GetChallengeAsync(challengeStore.Account, item);
                if (current.Status != ChallengeStatus.Valid)
                    await _acmeClient.ValidateChallengeAsync(challengeStore.Account, item);

                var validated = false;
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    current = await _acmeClient.GetChallengeAsync(challengeStore.Account, item);
                    if (current.Status == ChallengeStatus.Valid)
                    {
                        validated = true;
                        break;
                    }

                    if (current.Status == ChallengeStatus.Invalid)
                        throw new InvalidOperationException($"ACME DNS challenge failed for {item.DnsKey}.");
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }

                if (!validated) throw new TimeoutException($"ACME challenge timed out for {item.DnsKey}.");
            }

            for (int attempt = 0; attempt < 40; attempt++)
            {
                var order = await _acmeClient.GetOrderAsync(challengeStore.Account, challengeStore.Order.Location);
                if (order.Status is OrderStatus.Ready or OrderStatus.Valid) return true;
                if (order.Status == OrderStatus.Invalid)
                    throw new InvalidOperationException("ACME order was rejected.");
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            throw new TimeoutException("ACME order did not reach Ready within 2 minutes.");
        }

        public async Task<Certificate> GenerateCertificateAsync(Account account, Order order,
            string certificateCommonName)
        {
            var certificate = await _acmeClient.GenerateCertificateAsync(account, order, certificateCommonName);

            return certificate;
        }
    }
}