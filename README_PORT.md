# CertTool — Avalonia desktop port

## Scope

Cross-platform Avalonia 11 desktop shell targeting .NET 10. Runs from a single source project on Windows and Linux. The following features have been moved to Avalonia controls:

- PEM-to-JWK conversion, file open/save, and certificate store lookup
- Self-signed certificate generation, key algorithm/size/hash choices, and file output directory selection
- Automated Let's Encrypt ACME DNS-01 issuance via Porkbun, including two PFX passwords, visibility toggles, and progress
- Dedicated ACME account passphrase dialog, account encryption/reuse, certificate export folder selection and overwrite confirmation

The existing ACME, Porkbun, DNS propagation, encryption and certificate generation source has been retained.

## Run

Install the .NET 10 SDK on Windows or Linux, then:

    dotnet restore CertTool.Avalonia.csproj
    dotnet build CertTool.Avalonia.csproj
    dotnet run --project CertTool.Avalonia.csproj

For a standalone platform-specific deployment:

    dotnet publish -c Release -r win-x64 --self-contained true
    dotnet publish -c Release -r linux-x64 --self-contained true

Run the Windows publish on Windows, and Linux publish on Linux, to test native platform behavior.

## Configuration

The WPF `App.config` is not used by this port. Configure environment variables:

- `PORKBUN_DOMAIN` (required, e.g. `example.com`)
- `PORKBUN_API_KEY` and `PORKBUN_SECRET_API_KEY` (required for ACME)
- `ACME_CONTACT_EMAIL` (optional)

RSA options are RS256, RS384 and RS512. The UI currently asks for email rather than pre-populating it.

ACME account encryption remains user-passphrase-based (AES-256-GCM). The repository uses `Environment.SpecialFolder.ApplicationData` and keeps certificate outputs in a selected folder. Back up your existing account directory before testing migration on Linux.

For portable file-based certificate lookups, set `CERTTOOL_CERTIFICATE_FILE` to a certificate filename; alternatively the existing OS store lookup remains available where supported.

For OAuth token test without a local OS certificate store, set `CERTTOOL_SIGNING_PFX` and `CERTTOOL_SIGNING_PFX_PASSWORD`. Optional `CERTTOOL_TOKEN_ENDPOINT`, `CERTTOOL_CLIENT_ID` and `CERTTOOL_CERT_THUMBPRINT` override legacy defaults. Do not commit private keys or credentials.

## Important caveats

- No .NET SDK is installed in the authoring environment; **the port has not been compiled or runtime-tested**. Treat it as a migration candidate, not a certified final build.
- The older OAuth demo's fallback behavior depends on machine-specific certificate-store contents; use the file-based PFX path across platforms.
- Linux desktop environments differ in file picker providers, certificate store exposure and clipboard support; test specifically on KDE/Wayland, Windows and any target distributions.
- The UI was reconstructed using Avalonia controls in C# rather than mechanically converting the WPF XAML templates. The new layout has the three functional pages but is not a pixel-identical WPF clone.
- Account passphrases and PFX password strings are temporarily held in managed memory for the operation; plaintext secrets are not saved to application settings.
- Progress and error UX can be improved after first successful compilation.
