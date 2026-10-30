using System.Windows.Input;
using CertTool.Backend.LetsEncrypt.IO;
using CertTool.Commands;
using CertTool.Models;
using CertTool.Services.Acme;
using CertTool.Services.Porkbun;
using CertTool.Services;

namespace CertTool.ViewModels;

public class CreateCertificateViewModel : ViewModelBase
{
    private readonly IAcmeService _acmeService;
    private readonly PorkbunDnsService _porkbun;
    private readonly DnsPropagationService _dns;
    private readonly CertificateExportService _export;
    private string _fqdn = "";
    private string _email = "";
    private string _challengeText = "";
    private int _state;
    private string _pfxPassword = "";
    private string _confirmPfxPassword = "";

    public CreateCertificateViewModel(IAcmeService acmeService, PorkbunDnsService porkbun,
        DnsPropagationService dns, CertificateExportService export)
    {
        _acmeService = acmeService;
        _porkbun = porkbun;
        _dns = dns;
        _export = export;
        CreateCertificateCommand = new TaskDelegateCommand(ExecuteCreateCertificate, () => State == 0 && PasswordsMatch)
            .ObservesProperty(() => State)
            .ObservesProperty(() => PasswordsMatch);
    }

    // Transient UI inputs only: never serialized or written to application settings.
    public string PfxPassword
    {
        get => _pfxPassword;
        set { _pfxPassword = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(PasswordsMatch)); }
    }

    public string ConfirmPfxPassword
    {
        get => _confirmPfxPassword;
        set { _confirmPfxPassword = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(PasswordsMatch)); }
    }

    public bool PasswordsMatch =>
        !string.IsNullOrWhiteSpace(PfxPassword) &&
        string.Equals(PfxPassword, ConfirmPfxPassword, StringComparison.Ordinal);

    public ICommand CreateCertificateCommand { get; }
    public string FQDN { get => _fqdn; set { _fqdn = value; OnPropertyChanged(nameof(FQDN)); } }
    public string Email { get => _email; set { _email = value; OnPropertyChanged(nameof(Email)); } }
    public int State { get => _state; set { _state = value; OnPropertyChanged(nameof(State)); } }
    public string ChallengeText { get => _challengeText; set { _challengeText = value; OnPropertyChanged(nameof(ChallengeText)); } }
    private void Report(string message) => ChallengeText += $"{DateTime.Now:HH:mm:ss}  {message}\n";

    private async Task ExecuteCreateCertificate()
    {
        State = 1;
        ChallengeText = "";
        var records = new List<CreatedDnsRecord>();
        try
        {
            var fqdn = FQDN.Trim().TrimEnd('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(Email)) throw new ArgumentException("Enter an ACME contact email.");
            if (!PasswordsMatch) throw new InvalidOperationException("Enter matching, non-empty PFX passwords.");
            var exportPassword = PfxPassword;

            // Ask before any ACME order or DNS record is created.
            var destination = await _export.ChooseDestinationAsync(fqdn);
            if (destination is null)
            {
                Report("Certificate creation cancelled: no export folder selected or overwrite declined.");
                return;
            }

            // Passphrase is distinct from the certificate's PFX export password.
            var accountPassphrase = await PlatformDialogs.AskAcmePassphraseAsync();
            if (accountPassphrase is null)
            {
                Report("Certificate creation cancelled: ACME account passphrase not entered.");
                return;
            }

            Report("Creating Let's Encrypt ACME order...");
            var store = await _acmeService.CreateChallengeAsync(fqdn, Email.Trim(), accountPassphrase);
            if (store.Challenges.Count == 0) throw new InvalidOperationException("No DNS-01 challenges returned.");
            foreach (var challenge in store.Challenges)
            {
                Report($"Creating Porkbun TXT record: {challenge.DnsKey}");
                var record = await _porkbun.CreateTxtAsync(challenge.DnsKey, challenge.VerificationValue);
                records.Add(record);
            }
            foreach (var challenge in store.Challenges)
            {
                Report($"Checking DNS propagation: {challenge.DnsKey}");
                await _dns.WaitForTxtAsync(challenge.DnsKey, challenge.VerificationValue, Report);
            }
            Report("Requesting ACME challenge validation...");
            await _acmeService.ValidateChallengeAsync(store);
            Report("Finalizing order and retrieving certificate...");
            var certificate = await _acmeService.GenerateCertificateAsync(store.Account, store.Order, fqdn);

            Report($"Saving certificate files to: {destination.Directory}");
            await _export.ExportAsync(destination, certificate, exportPassword);
            Report("Certificate generated and saved successfully.");
        }
        catch (Exception ex)
        {
            Report("FAILED: " + ex.Message);
        }
        finally
        {
            // Cleanup failure is reported, never hidden. Limit deletion to IDs created during this run.
            foreach (var record in records)
            {
                try { await _porkbun.DeleteAsync(record); Report($"Deleted temporary DNS record {record.Id}."); }
                catch (Exception ex) { Report($"WARNING: Remove temporary Porkbun TXT record ID {record.Id} manually: {ex.Message}"); }
            }
            State = 0;
        }
    }
}
