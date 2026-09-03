namespace EftSsNavi.Sharing.Session;

public sealed record PartyPosition
{
    public PartyPosition(
        double x,
        double y,
        double z,
        double? forwardX,
        double? forwardZ,
        DateTimeOffset capturedAt,
        string? mapName)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "World coordinates must be finite.");
        }

        if (forwardX.HasValue != forwardZ.HasValue)
        {
            throw new ArgumentException("Horizontal direction must contain both components or neither component.");
        }

        if (forwardX is { } directionX &&
            (!double.IsFinite(directionX) || !double.IsFinite(forwardZ!.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(forwardX), "Direction components must be finite.");
        }

        if (capturedAt == default || capturedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Captured time must be a non-default UTC timestamp.", nameof(capturedAt));
        }

        X = x;
        Y = y;
        Z = z;
        ForwardX = forwardX;
        ForwardZ = forwardZ;
        CapturedAt = capturedAt;
        MapName = mapName;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public double? ForwardX { get; }

    public double? ForwardZ { get; }

    public DateTimeOffset CapturedAt { get; }

    public string? MapName { get; }
}
