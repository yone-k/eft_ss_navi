using EftSsNavi.Sharing.Protocol;

namespace EftSsNavi.Sharing.Tests.Protocol;

public sealed class SignalingCipherTests
{
    private const string RoomCodeValue = "ABCDEFGHJKLMNPQR";
    private static readonly Guid ParticipantId = Guid.Parse("28acaed1-ab41-45bb-a0f0-bd38221a96e6");

    [Fact]
    public void ShouldRecoverPlaintextWhenCiphertextIsAuthentic()
    {
        // Given: A signaling JSON payload encrypted for a room participant.
        const string plaintext = "{\"sdp\":\"offer-value\"}";
        var encrypted = SignalingCipher.Encrypt(plaintext, RoomCodeValue, ParticipantId);

        // When: The intended participant decrypts it with the room code.
        var wasDecrypted = SignalingCipher.TryDecrypt(encrypted, RoomCodeValue, ParticipantId, out var decrypted);

        // Then: Authentication succeeds and the original UTF-8 text is recovered.
        Assert.True(wasDecrypted);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void ShouldUseDifferentNonceForEachEncryption()
    {
        // Given: The same signaling plaintext and key inputs.
        const string plaintext = "{\"sdp\":\"same-value\"}";

        // When: The payload is encrypted twice.
        var first = SignalingCipher.Encrypt(plaintext, RoomCodeValue, ParticipantId);
        var second = SignalingCipher.Encrypt(plaintext, RoomCodeValue, ParticipantId);

        // Then: Random nonces produce different Base64 payloads.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ShouldRejectCiphertextWhenPayloadIsTampered()
    {
        // Given: A valid encrypted payload with one ciphertext byte modified.
        var encrypted = SignalingCipher.Encrypt("offer", RoomCodeValue, ParticipantId);
        var bytes = Convert.FromBase64String(encrypted);
        bytes[12] ^= 0x01;
        var tampered = Convert.ToBase64String(bytes);

        // When: Decryption is attempted.
        var exception = Record.Exception(() =>
        {
            var wasDecrypted = SignalingCipher.TryDecrypt(tampered, RoomCodeValue, ParticipantId, out var plaintext);

            // Then: The invalid message is rejected without exposing plaintext.
            Assert.False(wasDecrypted);
            Assert.Null(plaintext);
        });

        // Then: Authentication failures do not escape as cryptographic exceptions.
        Assert.Null(exception);
    }

    [Fact]
    public void ShouldRejectCiphertextWhenParticipantIdDiffers()
    {
        // Given: A payload encrypted for one participant.
        var encrypted = SignalingCipher.Encrypt("answer", RoomCodeValue, ParticipantId);

        // When: Another participant attempts to decrypt it.
        var wasDecrypted = SignalingCipher.TryDecrypt(encrypted, RoomCodeValue, Guid.NewGuid(), out var plaintext);

        // Then: The participant-bound authentication fails safely.
        Assert.False(wasDecrypted);
        Assert.Null(plaintext);
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("")]
    [InlineData("AA==")]
    public void ShouldRejectMalformedPayloadWithoutThrowing(string payload)
    {
        // Given: A payload that cannot contain nonce, ciphertext, and tag.

        // When: Decryption is attempted.
        var exception = Record.Exception(() =>
        {
            var wasDecrypted = SignalingCipher.TryDecrypt(payload, RoomCodeValue, ParticipantId, out var plaintext);

            // Then: The malformed message is treated as invalid.
            Assert.False(wasDecrypted);
            Assert.Null(plaintext);
        });

        // Then: Parsing failures do not escape to signaling callbacks.
        Assert.Null(exception);
    }

    [Fact]
    public void ShouldDeriveKeyWithEftSsNaviV1Salt()
    {
        // Given: A payload encrypted with the production cipher.
        const string plaintext = "known signaling payload";
        var encrypted = Convert.FromBase64String(SignalingCipher.Encrypt(plaintext, RoomCodeValue, ParticipantId));
        var expectedKey = new byte[32];
        System.Security.Cryptography.HKDF.DeriveKey(
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Text.Encoding.UTF8.GetBytes(RoomCodeValue),
            expectedKey,
            System.Text.Encoding.UTF8.GetBytes("eftssnavi-signaling-v1"),
            System.Text.Encoding.UTF8.GetBytes(ParticipantId.ToString("N")));
        var decrypted = new byte[encrypted.Length - 28];

        // When: An independent AES-GCM implementation decrypts with the specified salt.
        using var aes = new System.Security.Cryptography.AesGcm(expectedKey, 16);
        aes.Decrypt(encrypted.AsSpan(0, 12), encrypted.AsSpan(12, decrypted.Length), encrypted.AsSpan(12 + decrypted.Length, 16), decrypted);

        // Then: The documented key derivation recovers the plaintext.
        Assert.Equal(plaintext, System.Text.Encoding.UTF8.GetString(decrypted));
    }
}
