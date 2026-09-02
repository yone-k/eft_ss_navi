using System.Numerics;

namespace EftSsMap.Core.Observations;

public static class QuaternionDirectionCalculator
{
    public static bool TryCalculateHorizontalForward(Quaternion rotation, out Vector2 direction)
    {
        direction = default;

        if (!TryNormalize(rotation, out var normalizedRotation))
        {
            return false;
        }

        var forward = Vector3.Transform(Vector3.UnitZ, normalizedRotation);
        var horizontal = new Vector2(forward.X, forward.Z);
        var horizontalLength = Math.Sqrt(
            ((double)horizontal.X * horizontal.X) +
            ((double)horizontal.Y * horizontal.Y));

        if (!double.IsFinite(horizontalLength) || horizontalLength == 0)
        {
            return false;
        }

        direction = new Vector2(
            (float)(horizontal.X / horizontalLength),
            (float)(horizontal.Y / horizontalLength));
        return float.IsFinite(direction.X) && float.IsFinite(direction.Y);
    }

    internal static bool TryNormalize(Quaternion rotation, out Quaternion normalized)
    {
        normalized = default;

        if (!float.IsFinite(rotation.X)
            || !float.IsFinite(rotation.Y)
            || !float.IsFinite(rotation.Z)
            || !float.IsFinite(rotation.W))
        {
            return false;
        }

        var maximumComponent = MathF.Max(
            MathF.Max(MathF.Abs(rotation.X), MathF.Abs(rotation.Y)),
            MathF.Max(MathF.Abs(rotation.Z), MathF.Abs(rotation.W)));

        if (maximumComponent == 0f)
        {
            return false;
        }

        var scaled = new Quaternion(
            rotation.X / maximumComponent,
            rotation.Y / maximumComponent,
            rotation.Z / maximumComponent,
            rotation.W / maximumComponent);
        var scaledLength = scaled.Length();
        if (!float.IsFinite(scaledLength) || scaledLength == 0f)
        {
            return false;
        }

        normalized = scaled * (1f / scaledLength);
        return float.IsFinite(normalized.X)
            && float.IsFinite(normalized.Y)
            && float.IsFinite(normalized.Z)
            && float.IsFinite(normalized.W);
    }
}
