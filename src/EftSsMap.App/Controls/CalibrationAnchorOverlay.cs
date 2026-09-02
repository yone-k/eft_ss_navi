using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Controls;

public sealed class CalibrationAnchorOverlay
{
    private const double HitRadius = 18;

    public IReadOnlyList<CalibrationAnchor> Anchors { get; private set; } = [];

    public void Show(IReadOnlyList<CalibrationPoint> points, int replacementIndex)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count != 3)
        {
            throw new ArgumentException("Exactly three calibration points are required.", nameof(points));
        }

        if (replacementIndex < 0 || replacementIndex >= points.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(replacementIndex));
        }

        Anchors = points
            .Select((point, index) => new CalibrationAnchor(
                index + 1,
                point.Pixel,
                index == replacementIndex))
            .ToArray();
    }

    public void Hide() => Anchors = [];

    public bool TryHitTest(
        PixelPoint viewPoint,
        Func<PixelPoint, PixelPoint> imageToView,
        out int anchorIndex)
    {
        ArgumentNullException.ThrowIfNull(imageToView);
        anchorIndex = -1;
        var nearestDistanceSquared = HitRadius * HitRadius;
        for (var index = 0; index < Anchors.Count; index++)
        {
            var anchorView = imageToView(Anchors[index].Position);
            var deltaX = viewPoint.X - anchorView.X;
            var deltaY = viewPoint.Y - anchorView.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (double.IsFinite(distanceSquared) && distanceSquared <= nearestDistanceSquared)
            {
                anchorIndex = index;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return anchorIndex >= 0;
    }
}
