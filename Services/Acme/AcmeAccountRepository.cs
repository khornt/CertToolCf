using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertTool.Backend.LetsEncrypt.Cryptography;
using CertTool.Backend.LetsEncrypt;
using CertTool.Backend.LetsEncrypt.Entities;

namespace CertTool.Services.Acme;

/// <summary>Stores ACME accounts independently from exported certificates.</summary>
public sealed class AcmeAccountRepository
{
    public string AccountsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CertTool", "Accounts");

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    // Hashing the identity avoids using an email address as a filesystem path.
    private static string FileId(string email, Uri directory)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            directory.AbsoluteUri.TrimEnd('/') + "|" + NormalizeEmail(email)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string AccountPath(string email, Uri directory) =>
        Path.Combine(AccountsDirectory, FileId(email, directory) + ".json");

    public async Task<Account?> LoadAsync(string email, Uri directory, string passphrase)
    {
        var path = AccountPath(email, directory);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        AccountPersisted data;
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.TryGetProperty("Version", out _))
            {
                data = JsonSerializer.Deserialize<AccountPersisted>(
                    AccountKeyProtection.Decrypt(json, passphrase))
                    ?? throw new InvalidDataException($"Invalid encrypted account: {path}");
            }
            else
            {
                // Legacy plaintext account: encrypt in place only after validation below.
                data = JsonSerializer.Deserialize<AccountPersisted>(json)
                    ?? throw new InvalidDataException($"Invalid account document: {path}");
            }
        }

        if (!string.Equals(data.AccountContactEmail, NormalizeEmail(email), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(data.AcmeDirectory, directory.AbsoluteUri, StringComparison.Ordinal) ||
            !Uri.TryCreate(data.AccountLocation, UriKind.Absolute, out var location) ||
            location.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(location.Host, directory.Host, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(data.PrivateKeyPem))
            throw new InvalidDataException($"Stored ACME account identity is invalid: {path}");

        var account = new Account(new RsaKeyPair(data.PrivateKeyPem), location.AbsoluteUri)
        {
            Contact = new System.Collections.Generic.List<string> { Constants.PREFIX_MAILTO + NormalizeEmail(email) }
        };
        // Verified valid plaintext account: migrate to protected representation.
        // Do not remove the old data before the encrypted replacement is installed.
        using (var document = JsonDocument.Parse(json))
        {
            if (!document.RootElement.TryGetProperty("Version", out _))
                await SaveAsync(email, directory, account, passphrase);
        }
        return account;
    }

    public async Task SaveAsync(string email, Uri directory, Account account, string passphrase)
    {
        if (account.Location is null || account.Key is null ||
            !string.Equals(account.Location.Host, directory.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot persist account without a matching ACME account URL and key.");

        System.IO.Directory.CreateDirectory(AccountsDirectory);
        var path = AccountPath(email, directory);
        var data = new AccountPersisted
        {
            AccountContactEmail = NormalizeEmail(email),
            AcmeDirectory = directory.AbsoluteUri,
            AccountLocation = account.Location.AbsoluteUri,
            PrivateKeyPem = account.Key.ToPrivateKeyPem(),
            PublicKeyPem = account.Key.ToPublicKeyPem()
        };
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var plaintext = JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(temp, AccountKeyProtection.Encrypt(plaintext, passphrase));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    // Previously implemented Account.SaveAsync stored two files in Output next to the executable.
    // Only migrate a matching production account (legacy storage had no environment field).
    public async Task<Account?> TryMigrateLegacyAsync(string email, Uri directory, string passphrase)
    {
        if (directory != ApiEnvironment.LetsEncryptV2) return null;
        var normalized = NormalizeEmail(email);
        var folder = Path.Combine(AppContext.BaseDirectory, "Output");
        var urlFile = Path.Combine(folder, $"Account.{normalized}.txt");
        var keyFile = Path.Combine(folder, $"Account.{normalized}.Private.pem");
        if (!File.Exists(urlFile) && !File.Exists(keyFile)) return null;
        if (!File.Exists(urlFile) || !File.Exists(keyFile))
            throw new InvalidDataException("Legacy ACME account files are incomplete; refusing to create another account.");

        var url = (await File.ReadAllTextAsync(urlFile)).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var location) ||
            location.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(location.Host, directory.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Legacy account URL is not for the configured ACME environment.");

        var key = new RsaKeyPair(await File.ReadAllTextAsync(keyFile));
        var account = new Account(key, location.AbsoluteUri)
        {
            Contact = new System.Collections.Generic.List<string> { Constants.PREFIX_MAILTO + normalized }
        };
        await SaveAsync(email, directory, account, passphrase);
        // Legacy private-key file is no longer needed after encrypted storage succeeds.
        // Delete it rather than leaving an unencrypted copy behind.
        File.Delete(keyFile);
        return account;
    }
}
