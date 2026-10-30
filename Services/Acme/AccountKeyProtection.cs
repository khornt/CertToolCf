using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CertTool.Services.Acme;

/// <summary>Portable, passphrase-based authenticated encryption for ACME account documents.</summary>
internal static class AccountKeyProtection
{
    private const int CurrentVersion = 1;
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private sealed class Envelope
    {
        public int Version { get; set; }
        public int Iterations { get; set; }
        public string Salt { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Ciphertext { get; set; } = "";
    }

    internal static string Encrypt(string plaintext, string passphrase)
    {
        ValidatePassphrase(passphrase);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, 32);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[bytes.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, bytes, ciphertext, tag);
            return JsonSerializer.Serialize(new Envelope
            {
                Version = CurrentVersion,
                Iterations = Iterations,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    internal static string Decrypt(string json, string passphrase)
    {
        ValidatePassphrase(passphrase);
        var envelope = JsonSerializer.Deserialize<Envelope>(json)
            ?? throw new InvalidDataException("Invalid encrypted ACME account envelope.");
        if (envelope.Version != CurrentVersion || envelope.Iterations != Iterations)
            throw new InvalidDataException("Unsupported ACME account encryption format.");
        byte[] salt, nonce, tag, ciphertext;
        try
        {
            salt = Convert.FromBase64String(envelope.Salt);
            nonce = Convert.FromBase64String(envelope.Nonce);
            tag = Convert.FromBase64String(envelope.Tag);
            ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Invalid ACME account encryption data.", ex);
        }
        if (salt.Length != SaltSize || nonce.Length != NonceSize || tag.Length != TagSize)
            throw new InvalidDataException("Invalid ACME account encryption parameters.");
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, 32);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            try { aes.Decrypt(nonce, ciphertext, tag, plaintext); }
            catch (AuthenticationTagMismatchException ex)
            {
                throw new InvalidOperationException(
                    "Incorrect ACME account passphrase or corrupted account file. Existing account was not replaced.", ex);
            }
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < 12)
            throw new ArgumentException("ACME account passphrase must contain at least 12 characters.");
    }
}
