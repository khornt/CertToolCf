namespace CertTool.Backend.LetsEncrypt.Entities;

// Portable account data. The private key is NOT encrypted yet (checklist item 7).
public sealed class AccountPersisted
{
    public string AccountContactEmail { get; set; } = string.Empty;
    public string AccountLocation { get; set; } = string.Empty;
    public string AcmeDirectory { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
}