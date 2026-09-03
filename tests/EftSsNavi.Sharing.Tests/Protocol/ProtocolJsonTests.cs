using System.Text.Json;
using EftSsNavi.Sharing.Protocol;

namespace EftSsNavi.Sharing.Tests.Protocol;

public sealed class ProtocolJsonTests
{
    public static TheoryData<PartyMessage, Type> Messages => new()
    {
        { new HelloMessage("Alice", ProtocolJson.CurrentVersion), typeof(HelloMessage) },
        {
            new WelcomeMessage(
                Guid.Parse("03c49908-aa91-438b-80ef-b43622fe20b7"),
                "Alice",
                1,
                "Customs",
                [new PartyParticipant(Guid.Parse("4418cdb5-ad06-419a-b83a-f099af078f90"), "Host", 0)]),
            typeof(WelcomeMessage)
        },
        { new RejectMessage(RejectReason.Full), typeof(RejectMessage) },
        {
            new PositionMessage(
                Guid.Parse("03c49908-aa91-438b-80ef-b43622fe20b7"),
                "Alice",
                123.5,
                4.25,
                -98.75,
                0.6,
                -0.8,
                DateTimeOffset.Parse("2026-09-03T01:02:03Z"),
                "Customs"),
            typeof(PositionMessage)
        },
        { new MapChangedMessage(null), typeof(MapChangedMessage) },
        {
            new ParticipantJoinedMessage(
                new PartyParticipant(Guid.Parse("03c49908-aa91-438b-80ef-b43622fe20b7"), "Alice", 1)),
            typeof(ParticipantJoinedMessage)
        },
        { new ParticipantLeftMessage(Guid.Parse("03c49908-aa91-438b-80ef-b43622fe20b7")), typeof(ParticipantLeftMessage) },
        { new GoodbyeMessage(), typeof(GoodbyeMessage) },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void ShouldRoundTripSupportedMessageAsDiscriminatedJson(PartyMessage message, Type expectedType)
    {
        // Given: One of the eight supported protocol messages.

        // When: It is serialized and deserialized through the protocol boundary.
        var json = ProtocolJson.Serialize(message);
        var wasDeserialized = ProtocolJson.TryDeserialize(json, out var restored);

        // Then: Its concrete message kind and data survive the JSON round trip.
        Assert.True(wasDeserialized);
        Assert.NotNull(restored);
        Assert.IsType(expectedType, restored);
        AssertJsonEquivalent(json, ProtocolJson.Serialize(restored));
    }

    [Fact]
    public void ShouldWriteExplicitMessageTypeDiscriminator()
    {
        // Given: A supported Hello message.
        var message = new HelloMessage("Alice", ProtocolJson.CurrentVersion);

        // When: It is serialized.
        var json = ProtocolJson.Serialize(message);

        // Then: The wire JSON identifies the message as Hello.
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Hello", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void ShouldRejectUndefinedMessageType()
    {
        // Given: Syntactically valid JSON with an unsupported discriminator.
        const string json = "{\"type\":\"Chat\",\"text\":\"not-supported\"}";

        // When: The message is deserialized.
        var wasDeserialized = ProtocolJson.TryDeserialize(json, out var message);

        // Then: The undefined message kind is rejected.
        Assert.False(wasDeserialized);
        Assert.Null(message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{}")]
    public void ShouldRejectMalformedOrUndiscriminatedJsonWithoutThrowing(string json)
    {
        // Given: Input that is not a supported discriminated protocol message.

        // When: Deserialization is attempted.
        var exception = Record.Exception(() =>
        {
            var wasDeserialized = ProtocolJson.TryDeserialize(json, out var message);

            // Then: The input is rejected without producing a message.
            Assert.False(wasDeserialized);
            Assert.Null(message);
        });

        // Then: Invalid remote input does not escape as a JSON exception.
        Assert.Null(exception);
    }

    private static void AssertJsonEquivalent(string expected, string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.Equal(expectedDocument.RootElement.ToString(), actualDocument.RootElement.ToString());
    }
}
