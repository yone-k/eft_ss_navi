using EftSsNavi.Sharing.Protocol;

namespace EftSsNavi.Sharing.Tests.Protocol;

public sealed class ProtocolValidationTests
{
    [Theory]
    [InlineData("{\"type\":\"Hello\",\"protocolVersion\":1}")]
    [InlineData("{\"type\":\"Hello\",\"displayName\":\"   \",\"protocolVersion\":1}")]
    [InlineData("{\"type\":\"Hello\",\"displayName\":\"12345678901234567\",\"protocolVersion\":1}")]
    [InlineData("{\"type\":\"Hello\",\"displayName\":\"Alice\",\"protocolVersion\":0}")]
    public void ShouldRejectHelloWithInvalidRequiredValue(string json)
    {
        // Given: A Hello payload with a missing or invalid required value.

        // When: The payload is parsed.
        var accepted = ProtocolJson.TryDeserialize(json, out var message);

        // Then: It is rejected before reaching coordination code.
        Assert.False(accepted);
        Assert.Null(message);
    }

    [Theory]
    [InlineData("{\"type\":\"ParticipantLeft\",\"participantId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"type\":\"ParticipantJoined\",\"participant\":{\"id\":\"00000000-0000-0000-0000-000000000000\",\"displayName\":\"Alice\",\"colorIndex\":1}}")]
    [InlineData("{\"type\":\"ParticipantJoined\",\"participant\":{\"id\":\"4418cdb5-ad06-419a-b83a-f099af078f90\",\"displayName\":\"Alice\",\"colorIndex\":5}}")]
    public void ShouldRejectParticipantWithInvalidIdentityOrColor(string json)
    {
        // Given: A participant payload with an empty ID or out-of-range color.

        // When: The payload is parsed.
        var accepted = ProtocolJson.TryDeserialize(json, out var message);

        // Then: It is rejected as an invalid protocol message.
        Assert.False(accepted);
        Assert.Null(message);
    }

    [Theory]
    [InlineData("NaN", "0", "0")]
    [InlineData("0", "1", "null")]
    [InlineData("0", "null", "1")]
    public void ShouldRejectPositionWithInvalidFiniteOrForwardPair(
        string x,
        string forwardX,
        string forwardZ)
    {
        // Given: A Position payload with a non-finite coordinate or incomplete forward pair.
        var json = $$"""
            {"type":"Position","participantId":"4418cdb5-ad06-419a-b83a-f099af078f90","displayName":"Alice","x":"{{x}}","y":2,"z":3,"forwardX":{{forwardX}},"forwardZ":{{forwardZ}},"capturedAt":"2026-09-03T00:00:00Z","mapName":"Woods"}
            """.Replace($"\"{x}\"", x, StringComparison.Ordinal);

        // When: The payload is parsed.
        var accepted = ProtocolJson.TryDeserialize(json, out var message);

        // Then: It is rejected before projection or drawing.
        Assert.False(accepted);
        Assert.Null(message);
    }

    [Fact]
    public void ShouldRejectPositionTimestampWhenNotUtc()
    {
        // Given: A Position timestamp carrying a non-UTC offset.
        const string json = "{\"type\":\"Position\",\"participantId\":\"4418cdb5-ad06-419a-b83a-f099af078f90\",\"displayName\":\"Alice\",\"x\":1,\"y\":2,\"z\":3,\"forwardX\":null,\"forwardZ\":null,\"capturedAt\":\"2026-09-03T09:00:00+09:00\",\"mapName\":\"Woods\"}";

        // When: The payload is parsed.
        var accepted = ProtocolJson.TryDeserialize(json, out var message);

        // Then: The wire contract rejects a non-UTC observation timestamp.
        Assert.False(accepted);
        Assert.Null(message);
    }
}
