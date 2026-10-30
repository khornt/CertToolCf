
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class CertificateGenerator
{
    public enum KeyAlgorithm { Rsa, Ecdsa }

    public static IReadOnlyList<int> AllowedKeySizes(KeyAlgorithm algorithm) =>
        algorithm == KeyAlgorithm.Rsa ? [2048, 3072, 4096] : [256, 384, 521];

    private static AsymmetricAlgorithm CreateKey(KeyAlgorithm algorithm, int keySize) =>
        algorithm switch
        {
            KeyAlgorithm.Rsa => RSA.Create(keySize),
            KeyAlgorithm.Ecdsa => ECDsa.Create(keySize switch
            {
                256 => ECCurve.NamedCurves.nistP256,
                384 => ECCurve.NamedCurves.nistP384,
                521 => ECCurve.NamedCurves.nistP521,
                _ => throw new ArgumentOutOfRangeException(nameof(keySize))
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };

    private static CertificateRequest CreateRequest(
        string subject, AsymmetricAlgorithm key, HashAlgorithmName hash) => key switch
        {
            RSA rsa => new CertificateRequest(subject, rsa, hash, RSASignaturePadding.Pkcs1),
            ECDsa ecdsa => new CertificateRequest(subject, ecdsa, hash),
            _ => throw new NotSupportedException("Unsupported key algorithm")
        };

    private static string ExportPublicKey(AsymmetricAlgorithm key) => key switch
    {
        RSA rsa => rsa.ExportSubjectPublicKeyInfoPem(),
        ECDsa ecdsa => ecdsa.ExportSubjectPublicKeyInfoPem(),
        _ => throw new NotSupportedException("Unsupported key algorithm")
    };

    private static string ExportPrivateKey(
        AsymmetricAlgorithm key, string password, PbeParameters encryption) => key switch
    {
        RSA rsa => rsa.ExportEncryptedPkcs8PrivateKeyPem(password, encryption),
        ECDsa ecdsa => ecdsa.ExportEncryptedPkcs8PrivateKeyPem(password, encryption),
        _ => throw new NotSupportedException("Unsupported key algorithm")
    };

    private const int Pbkdf2Iterations = 100_000;

    private const string RootCaName = "LocusOHVTestRootCa";

    private const string RootCaSubject =
        "CN=Locus OHV Test CA, OU=OHV, O=Locus Solutions AS, " +
        "L=Sandefjord, S=Vestfold, C=NO";

    public static void Generate(
        string outputDirectory,
        string hostname,
        string subject,
        string rootCaPassword,
        string pfxPassword,
        int validDays = 2825,
        KeyAlgorithm algorithm = KeyAlgorithm.Rsa,
        int keySize = 2048,
        string signatureHash = "SHA256")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(validDays);
        if (!Enum.IsDefined(algorithm))
            throw new ArgumentOutOfRangeException(nameof(algorithm));
        if (!AllowedKeySizes(algorithm).Contains(keySize))
            throw new ArgumentOutOfRangeException(nameof(keySize),
                $"Unsupported key size for {algorithm}: {keySize}");
        var hash = signatureHash switch
        {
            "SHA256" => HashAlgorithmName.SHA256,
            "SHA384" => HashAlgorithmName.SHA384,
            "SHA512" => HashAlgorithmName.SHA512,
            _ => throw new ArgumentException("Choose SHA256, SHA384 or SHA512", nameof(signatureHash))
        };

        Directory.CreateDirectory(outputDirectory);

        // Check all expected filenames before doing any cryptographic work.
        var filenames = GetOutputFileNames(hostname);
        var existing = filenames
            .Where(name => File.Exists(Path.Combine(outputDirectory, name)))
            .ToArray();

        if (existing.Length > 0)
        {
            throw new IOException(
                "Certificate generation cancelled. These files already exist: " +
                string.Join(", ", existing));
        }

        // First generate the complete set in a private staging directory.
        // Never write directly over previously issued certificates or keys.
        var stagingDirectory = Path.Combine(
            outputDirectory, $".certtool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        var movedFiles = new List<string>();
        try
        {
            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            var rootNotAfter = notBefore.AddDays(validDays);
            var serverNotAfter = rootNotAfter.AddMinutes(-1);

            using var rootKey = CreateKey(algorithm, keySize);
            using var rootCert = CreateRootCa(rootKey, hash, notBefore, rootNotAfter);
            ExportRootCa(stagingDirectory, rootCert, rootKey, rootCaPassword);

            using var serverKey = CreateKey(algorithm, keySize);
            using var serverCert = CreateServerCertificate(
                hostname, subject, serverKey, rootCert, hash, notBefore, serverNotAfter);
            ExportServerCertificate(
                stagingDirectory, hostname, serverCert, serverKey, pfxPassword);

            ExportPfx(stagingDirectory, hostname, serverCert, rootCert, pfxPassword);

            // File.Move without overwrite:true fails if another process
            // created a destination file since our initial check.
            foreach (var filename in filenames)
            {
                var destination = Path.Combine(outputDirectory, filename);
                File.Move(Path.Combine(stagingDirectory, filename), destination);
                movedFiles.Add(destination);
            }
        }
        catch
        {
            // A failure must not leave a partially installed certificate set.
            // Delete only files that this invocation successfully moved.
            foreach (var file in movedFiles)
                File.Delete(file);
            throw;
        }
        finally
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    private static string[] GetOutputFileNames(string hostname) =>
    [
        $"{RootCaName}Sert.pem",
        $"{RootCaName}.key",
        $"{hostname}.pem",
        $"{hostname}.public.pem",
        $"{hostname}.key",
        $"{hostname}.pfx"
    ];

    private static X509Certificate2 CreateRootCa(
        AsymmetricAlgorithm rootKey,
        HashAlgorithmName hash,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        var request = CreateRequest(RootCaSubject, rootKey, hash);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                true, false, 0, true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign |
                X509KeyUsageFlags.CrlSign,
                true));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                request.PublicKey, false));

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 CreateServerCertificate(
        string hostname,
        string subject,
        AsymmetricAlgorithm serverKey,
        X509Certificate2 rootCert,
        HashAlgorithmName hash,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
     
        var request = CreateRequest(subject, serverKey, hash);

        AddServerExtensions(request, hostname);

        var serialNumber = CreateSerialNumber();

        using var signedCert = request.Create(
            rootCert,
            notBefore,
            notAfter,
            serialNumber);

        return serverKey switch
        {
            RSA rsa => signedCert.CopyWithPrivateKey(rsa),
            ECDsa ecdsa => signedCert.CopyWithPrivateKey(ecdsa),
            _ => throw new NotSupportedException("Unsupported key algorithm")
        };
    }

    private static void AddServerExtensions(
        CertificateRequest request,
        string hostname)
    {
        // This is not a CA certificate
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                false, false, 0, true));

        // Permitted key usages
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | // Applies to RSA and ECDSA
                (request.PublicKey.Oid?.Value == "1.2.840.113549.1.1.1"
                    ? X509KeyUsageFlags.KeyEncipherment
                    : X509KeyUsageFlags.None),
                true));

        // Extended Key Usage: TLS Server Authentication
        var eku = new OidCollection
        {
            new("1.3.6.1.5.5.7.3.1")
        };

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(eku, false));

        // Subject Alternative Name
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(hostname);

        request.CertificateExtensions.Add(san.Build());

        // Subject Key Identifier
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                request.PublicKey, false));
    }

    private static byte[] CreateSerialNumber()
    {
        var serialNumber = RandomNumberGenerator.GetBytes(16);

        // Ensure a positive, nonzero serial number
        serialNumber[0] &= 0x7F;

        if (serialNumber.All(b => b == 0))
            serialNumber[^1] = 1;

        return serialNumber;
    }

    private static void ExportRootCa(
        string outputDirectory,
        X509Certificate2 rootCert,
        AsymmetricAlgorithm rootKey,
        string password)
    {
        File.WriteAllText(
            Path.Combine(outputDirectory, $"{RootCaName}Sert.pem"),
            rootCert.ExportCertificatePem());

        ExportEncryptedPrivateKey(
            Path.Combine(outputDirectory, $"{RootCaName}.key"),
            rootKey,
            password);
    }

    private static void ExportServerCertificate(
        string outputDirectory,
        string hostname,
        X509Certificate2 serverCert,
        AsymmetricAlgorithm serverKey,
        string password)
    {
        // Signed server certificate
        File.WriteAllText(
            Path.Combine(outputDirectory, $"{hostname}.pem"),
            serverCert.ExportCertificatePem());

        // Public key
        File.WriteAllText(
            Path.Combine(outputDirectory, $"{hostname}.public.pem"),
            ExportPublicKey(serverKey));

        // Encrypted private key
        ExportEncryptedPrivateKey(
            Path.Combine(outputDirectory, $"{hostname}.key"),
            serverKey,
            password);
    }

    private static void ExportEncryptedPrivateKey(
        string filePath,
        AsymmetricAlgorithm key,
        string password)
    {
        var encryption = new PbeParameters(
            PbeEncryptionAlgorithm.Aes256Cbc,
            HashAlgorithmName.SHA256,
            Pbkdf2Iterations);

        File.WriteAllText(
            filePath,
            ExportPrivateKey(key, password, encryption));
    }

    private static void ExportPfx(
        string outputDirectory,
        string hostname,
        X509Certificate2 serverCert,
        X509Certificate2 rootCert,
        string password)
    {
        using var publicRootCert = X509Certificate2.CreateFromPem(
            rootCert.ExportCertificatePem());

        
        
        if (OperatingSystem.IsWindows())
        {
            rootCert.FriendlyName = "Locus OHV Test CA";
            serverCert.FriendlyName = hostname;
        }
        
        var certificates = new X509Certificate2Collection
        {
            serverCert,
            publicRootCert
        };

        var pfx = certificates.Export(
            X509ContentType.Pkcs12,
            password);

        if (pfx is null)
            throw new CryptographicException("Failed to export PFX.");

        try
        {
            File.WriteAllBytes(
                Path.Combine(outputDirectory, $"{hostname}.pfx"),
                pfx);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }
}
