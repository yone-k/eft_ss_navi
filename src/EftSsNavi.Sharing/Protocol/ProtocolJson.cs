using System.Text.Json;
using System.Text.Json.Nodes;

namespace EftSsNavi.Sharing.Protocol;

public static class ProtocolJson
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(PartyMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var type = GetMessageType(message);
        var node = JsonSerializer.SerializeToNode(message, message.GetType(), SerializerOptions)?.AsObject()
            ?? throw new JsonException("The protocol message could not be serialized.");

        var envelope = new JsonObject
        {
            ["type"] = type,
        };

        foreach (var property in node)
        {
            envelope.Add(property.Key, property.Value?.DeepClone());
        }

        return envelope.ToJsonString(SerializerOptions);
    }

    public static bool TryDeserialize(string json, out PartyMessage? message)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var type = typeElement.GetString();
            if (!HasRequiredProperties(document.RootElement, type))
            {
                return false;
            }

            message = type switch
            {
                "Hello" => document.RootElement.Deserialize<HelloMessage>(SerializerOptions),
                "Welcome" => document.RootElement.Deserialize<WelcomeMessage>(SerializerOptions),
                "Reject" => document.RootElement.Deserialize<RejectMessage>(SerializerOptions),
                "Position" => document.RootElement.Deserialize<PositionMessage>(SerializerOptions),
                "MapChanged" => document.RootElement.Deserialize<MapChangedMessage>(SerializerOptions),
                "ParticipantJoined" => document.RootElement.Deserialize<ParticipantJoinedMessage>(SerializerOptions),
                "ParticipantLeft" => document.RootElement.Deserialize<ParticipantLeftMessage>(SerializerOptions),
                "Goodbye" => document.RootElement.Deserialize<GoodbyeMessage>(SerializerOptions),
                _ => null,
            };

            if (message is null || !IsValid(message))
            {
                message = null;
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            message = null;
            return false;
        }
        catch (NotSupportedException)
        {
            message = null;
            return false;
        }
    }

    private static string GetMessageType(PartyMessage message) => message switch
    {
        HelloMessage => "Hello",
        WelcomeMessage => "Welcome",
        RejectMessage => "Reject",
        PositionMessage => "Position",
        MapChangedMessage => "MapChanged",
        ParticipantJoinedMessage => "ParticipantJoined",
        ParticipantLeftMessage => "ParticipantLeft",
        GoodbyeMessage => "Goodbye",
        _ => throw new ArgumentException("Unsupported protocol message type.", nameof(message)),
    };

    private static bool HasRequiredProperties(JsonElement element, string? type)
    {
        string[] properties = type switch
        {
            "Hello" => ["displayName", "protocolVersion"],
            "Welcome" => ["participantId", "displayName", "colorIndex", "mapName", "participants"],
            "Reject" => ["reason"],
            "Position" =>
            [
                "participantId", "displayName", "x", "y", "z", "forwardX", "forwardZ", "capturedAt", "mapName",
            ],
            "MapChanged" => ["mapName"],
            "ParticipantJoined" => ["participant"],
            "ParticipantLeft" => ["participantId"],
            "Goodbye" => [],
            _ => ["__unknown_message_type__"],
        };

        return properties.All(property => element.TryGetProperty(property, out _));
    }

    private static bool IsValid(PartyMessage message) => message switch
    {
        HelloMessage hello => IsValidRequestedName(hello.DisplayName) && hello.ProtocolVersion > 0,
        WelcomeMessage welcome =>
            welcome.ParticipantId != Guid.Empty &&
            IsValidAssignedName(welcome.DisplayName) &&
            IsValidColor(welcome.ColorIndex) &&
            welcome.Participants is not null &&
            welcome.Participants.Count is > 0 and <= 5 &&
            welcome.Participants.All(IsValidParticipant) &&
            welcome.Participants.Select(participant => participant.Id).Distinct().Count() == welcome.Participants.Count,
        RejectMessage reject => Enum.IsDefined(reject.Reason),
        PositionMessage position =>
            position.ParticipantId != Guid.Empty &&
            IsValidAssignedName(position.DisplayName) &&
            double.IsFinite(position.X) &&
            double.IsFinite(position.Y) &&
            double.IsFinite(position.Z) &&
            HasValidForward(position.ForwardX, position.ForwardZ) &&
            position.CapturedAt != default &&
            position.CapturedAt.Offset == TimeSpan.Zero,
        MapChangedMessage => true,
        ParticipantJoinedMessage joined => joined.Participant is not null && IsValidParticipant(joined.Participant),
        ParticipantLeftMessage left => left.ParticipantId != Guid.Empty,
        GoodbyeMessage => true,
        _ => false,
    };

    private static bool IsValidParticipant(PartyParticipant participant) =>
        participant.Id != Guid.Empty &&
        IsValidAssignedName(participant.DisplayName) &&
        IsValidColor(participant.ColorIndex);

    private static bool IsValidRequestedName(string? displayName) =>
        IsCanonicalName(displayName) && displayName!.Length <= 16;

    private static bool IsValidAssignedName(string? displayName) =>
        IsCanonicalName(displayName) && displayName!.Length <= 20;

    private static bool IsCanonicalName(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) &&
        string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal);

    private static bool IsValidColor(int colorIndex) => colorIndex is >= 0 and <= 4;

    private static bool HasValidForward(double? forwardX, double? forwardZ) =>
        (forwardX, forwardZ) switch
        {
            (null, null) => true,
            ({ } x, { } z) => double.IsFinite(x) && double.IsFinite(z),
            _ => false,
        };
}
