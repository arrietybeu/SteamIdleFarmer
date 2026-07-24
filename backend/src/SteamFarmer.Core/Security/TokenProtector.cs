using System.Security.Cryptography;
using System.Text;

namespace SteamFarmer.Core.Security;

/// <summary>Encrypts Steam refresh tokens at rest. These tokens ARE credentials for other people's accounts.</summary>
public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedText);
}

/// <summary>AES-256-GCM with a key derived from a shared secret (env FARMER_SECRET).</summary>
public sealed class AesTokenProtector : ITokenProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesTokenProtector(string secret, byte[] salt)
    {
        if (string.IsNullOrEmpty(secret))
            throw new ArgumentException("secret required", nameof(secret));
        ArgumentNullException.ThrowIfNull(salt);
        // Salted, stretched key derivation (PBKDF2) so a weak FARMER_SECRET is not trivially
        // brute-forceable if the encrypted token DB ever leaks.
        _key = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations: 210_000, HashAlgorithmName.SHA256, outputLength: 32);
    }

    public string Protect(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var packed = new byte[NonceSize + cipher.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, packed, 0, NonceSize);
        Buffer.BlockCopy(cipher, 0, packed, NonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, packed, NonceSize + cipher.Length, TagSize);
        return Convert.ToBase64String(packed);
    }

    public string Unprotect(string protectedText)
    {
        var packed = Convert.FromBase64String(protectedText);
        var nonce = packed[..NonceSize];
        var tag = packed[^TagSize..];
        var cipher = packed[NonceSize..^TagSize];
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}

/// <summary>
/// Dev-only fallback when no FARMER_SECRET is configured: base64 (NOT encryption).
/// The service logs a loud warning when this is used.
/// </summary>
public sealed class NullTokenProtector : ITokenProtector
{
    public string Protect(string plaintext) => Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
    public string Unprotect(string protectedText) => Encoding.UTF8.GetString(Convert.FromBase64String(protectedText));
}
