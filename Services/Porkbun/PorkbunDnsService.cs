using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using CertTool.Models;

namespace CertTool.Services.Porkbun;

public sealed record CreatedDnsRecord(string Zone, string Id, string Name);

public sealed class PorkbunDnsService(HttpClient http, AppConfig config)
{
    private static readonly Uri BaseUri = new("https://api.porkbun.com/api/json/v3/");

    private (string Zone, string Name) ResolveName(string fullName)
    {
        var zone = config.PorkbunDomain.Trim().TrimEnd('.').ToLowerInvariant();
        var name = fullName.Trim().TrimEnd('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(zone))
            throw new InvalidOperationException("Set PorkbunDomain in App.config (for example example.com).");
        if (name != zone && !name.EndsWith("." + zone, StringComparison.Ordinal))
            throw new InvalidOperationException($"Challenge name '{name}' is not within Porkbun zone '{zone}'.");
        return (zone, name == zone ? "" : name[..^(zone.Length + 1)]);
    }

    private async Task<JsonDocument> SendAsync(string relativePath, object? payload = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.PorkbunApiKey) || string.IsNullOrWhiteSpace(config.PorkbunSecretApiKey))
            throw new InvalidOperationException("Porkbun credentials are missing. Set PORKBUN_API_KEY and PORKBUN_SECRET_API_KEY environment variables.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, relativePath));
        request.Headers.Add("X-API-Key", config.PorkbunApiKey);
        request.Headers.Add("X-Secret-API-Key", config.PorkbunSecretApiKey);
        request.Content = JsonContent.Create(payload ?? new { });
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Porkbun API returned HTTP {(int)response.StatusCode}.");
        var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("status", out var status) || status.GetString() != "SUCCESS")
        {
            var message = document.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Unknown Porkbun error";
            document.Dispose();
            throw new InvalidOperationException($"Porkbun API rejected DNS operation: {message}");
        }
        return document;
    }

    public async Task<CreatedDnsRecord> CreateTxtAsync(string fullName, string value, CancellationToken ct = default)
    {
        var (zone, relativeName) = ResolveName(fullName);
        using var result = await SendAsync($"dns/create/{Uri.EscapeDataString(zone)}", new
        {
            name = relativeName, type = "TXT", content = value, ttl = "600"
        }, ct);
        if (!result.RootElement.TryGetProperty("id", out var id))
            throw new InvalidOperationException("Porkbun created a DNS record but did not return its ID; inspect DNS manually before retrying.");
        var recordId = id.ToString();
        if (string.IsNullOrWhiteSpace(recordId)) throw new InvalidOperationException("Porkbun returned an empty DNS record ID.");
        return new CreatedDnsRecord(zone, recordId, fullName);
    }

    public async Task DeleteAsync(CreatedDnsRecord record, CancellationToken ct = default)
    {
        // Delete by exact returned ID only. Never remove unrelated TXT records.
        using var _ = await SendAsync($"dns/delete/{Uri.EscapeDataString(record.Zone)}/{Uri.EscapeDataString(record.Id)}", cancellationToken: ct);
    }
}
