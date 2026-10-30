using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


using Certificate = CertTool.Backend.LetsEncrypt.Entities.Certificate;

namespace CertTool.Services;

/// <summary>
/// One folder per certificate export. Stages the complete output set before replacing
/// any destination files, and restores the previous files if a later move fails.
/// </summary>
public sealed class CertificateExportService
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CertTool", "export-settings.json");

    private static readonly string[] Extensions = [".pfx", ".crt", ".crt.pem", ".key.pem"];

    public sealed record ExportTarget(string Directory, string Fqdn, IReadOnlyDictionary<string, FileSnapshot> Snapshots);
    public sealed record FileSnapshot(bool Exists, long Length, DateTime LastWriteUtc);

    public async Task<ExportTarget?> ChooseDestinationAsync(string fqdn)
    {
        var directory = await PlatformDialogs.SelectFolderAsync(ReadLastDirectory());
        if (directory is null) return null;
        directory = Path.GetFullPath(directory);
        var paths = GetPaths(directory, fqdn);
        var existing = paths.Where(File.Exists).ToArray();
        if (existing.Length != 0 && !await ConfirmOverwriteAsync(existing)) return null;
        SaveLastDirectory(directory);
        return new ExportTarget(directory, fqdn, paths.ToDictionary(p => p, Snapshot));
    }

    public async Task ExportAsync(ExportTarget target, Certificate certificate, string password)
    {
        var paths = GetPaths(target.Directory, target.Fqdn);
        Directory.CreateDirectory(target.Directory);

        // Detect changes made by another process during the potentially long ACME issuance.
        if (paths.Any(p => !Snapshot(p).Equals(target.Snapshots[p])))
        {
            var existing = paths.Where(File.Exists).ToArray();
            if (existing.Length != 0 && !await ConfirmOverwriteAsync(existing))
                throw new OperationCanceledException("Certificate export cancelled because destination files changed.");
            if (existing.Length == 0 && !await PlatformDialogs.ConfirmAsync("The certificate output folder changed while the certificate was issued. Continue saving?"))
                throw new OperationCanceledException("Certificate export cancelled.");
        }

        // Materialize all formats before touching existing files.
        var data = new byte[][]
        {
            certificate.GeneratePfx(password),
            certificate.GenerateCrt(password),
            Encoding.UTF8.GetBytes(certificate.GenerateCrtPem(password)),
            Encoding.UTF8.GetBytes(certificate.GenerateKeyPem())
        };

        var staged = new Dictionary<string, string>();
        var backups = new Dictionary<string, string>();
        var installed = new List<string>();
        var transactionId = Guid.NewGuid().ToString("N");
        try
        {
            for (var i = 0; i < paths.Length; i++)
            {
                var temp = Path.Combine(target.Directory, $".certtool-{transactionId}-{i}.tmp");
                await File.WriteAllBytesAsync(temp, data[i]);
                staged.Add(paths[i], temp);
            }

            // Check the destination again immediately before the first replacement.
            // All files are already staged at this point.
            var confirmed = paths.ToDictionary(p => p, Snapshot);
            foreach (var path in paths)
            {
                if (!Snapshot(path).Equals(confirmed[path]))
                    throw new IOException($"Destination changed during export: {path}");
                if (File.Exists(path))
                {
                    var backup = Path.Combine(target.Directory,
                        $".certtool-{transactionId}-{Path.GetFileName(path)}.bak");
                    File.Move(path, backup);
                    backups.Add(path, backup);
                }
                File.Move(staged[path], path);
                installed.Add(path);
            }
        }
        catch
        {
            var restoreErrors = new List<Exception>();
            foreach (var path in installed)
            {
                try { File.Delete(path); }
                catch (Exception ex) { restoreErrors.Add(ex); }
            }
            foreach (var (original, backup) in backups)
            {
                try { File.Move(backup, original, overwrite: true); }
                catch (Exception ex) { restoreErrors.Add(ex); }
            }
            if (restoreErrors.Count != 0)
                throw new IOException("Export failed and rollback was incomplete. Check .certtool-*.bak files in the destination before retrying.", new AggregateException(restoreErrors));
            throw;
        }
        finally
        {
            foreach (var temp in staged.Values)
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { /* A leftover temp file is preferable to hiding the original failure. */ }
            }
        }

        // Successful export: remove backups. Leave a backup in place if its deletion fails.
        foreach (var backup in backups.Values)
            try { File.Delete(backup); } catch { /* Preserve recoverable backup. */ }
    }

    private static string[] GetPaths(string directory, string fqdn) =>
        Extensions.Select(ext => Path.Combine(directory, fqdn + ext)).ToArray();

    private static FileSnapshot Snapshot(string path)
    {
        var info = new FileInfo(path);
        return info.Exists
            ? new FileSnapshot(true, info.Length, info.LastWriteTimeUtc)
            : new FileSnapshot(false, 0, DateTime.MinValue);
    }

    private static Task<bool> ConfirmOverwriteAsync(string[] existing)
    {
        var names = string.Join(Environment.NewLine, existing.Select(Path.GetFileName));
        return PlatformDialogs.ConfirmAsync("Replace the following existing certificate files after issuance?\n" + names);
    }

    private static string? ReadLastDirectory()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return null;
            return JsonSerializer.Deserialize<ExportSettings>(File.ReadAllText(SettingsFile))?.LastDirectory;
        }
        catch (Exception) { return null; }
    }

    private static void SaveLastDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new ExportSettings(directory)));
        }
        catch (IOException) { /* Export can still proceed without remembered location. */ }
        catch (UnauthorizedAccessException) { /* Export can still proceed. */ }
    }

    private sealed record ExportSettings(string LastDirectory);
}
