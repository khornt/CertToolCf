using CertTool.Backend.LetsEncrypt.Entities;
using CertTool.Services.Acme;

namespace CertTool.Backend.LetsEncrypt.WebClient
{
    public partial class AcmeClient
    {
        // Public Methods

        public async Task<Account> CreateNewAccountAsync(string contactEmail)
        {
            
            var account = Account.Create(new List<string> { $"{Constants.PREFIX_MAILTO}{contactEmail}" });

            var returnAccount = await NewAccountAsync(account);
            account.FillBy(returnAccount);

            return account;
        }

        public async Task<Account> GetOrCreateAccountAsync(
            string contactEmail, AcmeAccountRepository repository, Uri directoryUri, string passphrase)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);
            ArgumentNullException.ThrowIfNull(repository);
            var email = contactEmail.Trim().ToLowerInvariant();
            var account = await repository.LoadAsync(email, directoryUri, passphrase)
                ?? await repository.TryMigrateLegacyAsync(email, directoryUri, passphrase);

            if (account != null)
            {
                // POST-as-GET verifies the stored key still authenticates at the CA.
                var nonce = await GetNonceAsync();
                var signed = account.Signer.Sign(null, account.Location, account.Location, nonce);
                
                var current = await PostAsync<Account>(account.Location, signed);
                if (current.Status == AccountStatus.Deactivated || current.Status == AccountStatus.Revoked)
                    throw new InvalidOperationException("Stored ACME account is no longer active.");
                return account;
            }

            account = await CreateNewAccountAsync(email);
            await repository.SaveAsync(email, directoryUri, account, passphrase);
            return account;
        }

        // Private Methods

        //private async Task<Account> GetAccountAsync(Uri accountLocation)
        //{
        //    var nonce = await GetNonceAsync();

        //    var signedData = _jws.Sign(null, accountLocation, accountLocation, nonce);
        //    var account = await PostAsync<Account>(accountLocation, signedData);
        //    account.Location = accountLocation;
        //    return account;
        //}

        private async Task<Account> DeactivateAccountAsync(Account account)
        {
            var nonce = await GetNonceAsync();

            var data = new Account { Status = AccountStatus.Deactivated };
            var signedData = account.Signer.Sign(data, account.Location, account.Location, nonce);
            
            return await PostAsync<Account>(account.Location, signedData);
        }

        private async Task<Account> UpdateAccountAsync(Account account)
        {
            var nonce = await GetNonceAsync();

            var signedData = account.Signer.Sign(account, account.Location, account.Location, nonce);
            
            return await PostAsync<Account>(account.Location, signedData);
        }

        private async Task<Account> NewAccountAsync(Account account)
        {
            var directory = await GetDirectoryAsync();
            var nonce = await GetNonceAsync();

            var signedData = account.Signer.Sign(account, url: directory.NewAccount, nonce: nonce);
            return await PostAsync<Account>(directory.NewAccount, signedData);
        }
    }
}