using System.Net;
using DnsClient;

namespace CertTool.Services.Acme;

public sealed class DnsPropagationService
{
    private const int MaxAttempts = 36;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly LookupClient _googleDns;
    private readonly LookupClient _cloudflareDns;

    public DnsPropagationService()
    {
        _googleDns = CreateClient("8.8.8.8");
        _cloudflareDns = CreateClient("1.1.1.1");
    }

    private static LookupClient CreateClient(string address)
    {
        var options = new LookupClientOptions(IPAddress.Parse(address))
        {
            UseCache = false,
            Timeout = TimeSpan.FromSeconds(5),
            UseTcpFallback = true
        };

        return new LookupClient(options);
    }

    public async Task WaitForTxtAsync(
        string name,
        string expectedValue,
        Action<string> progress,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedValue);
        ArgumentNullException.ThrowIfNull(progress);

        name = name.Trim().TrimEnd('.');

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            progress(
                $"Checking DNS TXT propagation ({attempt}/{MaxAttempts}): {name}");

            var googleValid = await CheckResolverAsync(
                _googleDns,
                "Google DNS",
                name,
                expectedValue,
                progress,
                ct);

            var cloudflareValid = await CheckResolverAsync(
                _cloudflareDns,
                "Cloudflare DNS",
                name,
                expectedValue,
                progress,
                ct);

            if (googleValid && cloudflareValid)
            {
                progress(
                    $"DNS TXT propagation verified successfully: {name}");

                return;
            }

            if (attempt < MaxAttempts)
            {
                progress(
                    $"Waiting {RetryDelay.TotalSeconds:0} seconds before retry...");

                await Task.Delay(RetryDelay, ct);
            }
        }

        throw new TimeoutException(
            $"TXT record '{name}' did not propagate with the expected " +
            $"value to both public DNS resolvers after {MaxAttempts} attempts.");
    }

    private static async Task<bool> CheckResolverAsync(
        LookupClient client,
        string resolverName,
        string name,
        string expectedValue,
        Action<string> progress,
        CancellationToken ct)
    {
        try
        {
            var response = await client.QueryAsync(
                name,
                QueryType.TXT,
                cancellationToken: ct);

            if (response.HasError)
            {
                progress(
                    $"{resolverName}: DNS error: {response.Header.ResponseCode}");

                return false;
            }

            var txtRecords = response.Answers
                .TxtRecords()
                .ToArray();

            if (txtRecords.Length == 0)
            {
                progress($"{resolverName}: TXT record not found.");
                return false;
            }

            var match = txtRecords.Any(record =>
                string.Concat(record.Text) == expectedValue);

            if (match)
            {
                progress($"{resolverName}: TXT record found, value matches.");
                return true;
            }

            progress(
                $"{resolverName}: TXT record exists, but value does not match.");

            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            progress(
                $"{resolverName}: DNS lookup failed: " +
                $"{ex.GetType().Name}: {ex.Message}");

            return false;
        }
    }
}
