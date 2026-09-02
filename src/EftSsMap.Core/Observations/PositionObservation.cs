using System.Numerics;

namespace EftSsMap.Core.Observations;

public sealed record PositionObservation(
    DateTime CapturedAt,
    Vector3 Position,
    Quaternion Rotation,
    string GameTime,
    int? SequenceNumber,
    Vector2? HorizontalForward);
