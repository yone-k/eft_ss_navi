using System.Security.Cryptography;
using System.Text;

namespace EftSsNavi.Sharing.Protocol;

public static class SignalingCipher
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("eftssnavi-signaling-v1");

    public static string Encrypt(string plaintext, string roomCode, Guid participantId)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(roomCode);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];
        var key = DeriveKey(roomCode, participantId);

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            var payload = new byte[NonceSize + ciphertext.Length + TagSize];
            nonce.CopyTo(payload, 0);
            ciphertext.CopyTo(payload, NonceSize);
            tag.CopyTo(payload, NonceSize + ciphertext.Length);
            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static bool TryDecrypt(
        string payload,
        string roomCode,
        Guid participantId,
        out string? plaintext)
    {
        plaintext = null;

        if (string.IsNullOrEmpty(payload) || roomCode is null)
        {
            return false;
        }

        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return false;
        }

        if (encrypted.Length < NonceSize + TagSize)
        {
            return false;
        }

        var ciphertextLength = encrypted.Length - NonceSize - TagSize;
        var plaintextBytes = new byte[ciphertextLength];
        var key = DeriveKey(roomCode, participantId);

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                encrypted.AsSpan(0, NonceSize),
                encrypted.AsSpan(NonceSize, ciphertextLength),
                encrypted.AsSpan(NonceSize + ciphertextLength, TagSize),
                plaintextBytes);
            plaintext = Encoding.UTF8.GetString(plaintextBytes);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    private static byte[] DeriveKey(string roomCode, Guid participantId)
    {
        var key = new byte[KeySize];
        var inputKeyMaterial = Encoding.UTF8.GetBytes(roomCode);
        var info = Encoding.UTF8.GetBytes(participantId.ToString("N"));

        HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKeyMaterial, key, Salt, info);
        return key;
    }
}
