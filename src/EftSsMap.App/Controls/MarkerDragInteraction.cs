using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Controls;

public sealed class MarkerDragInteraction
{
    public const double HitRadius = 14;

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!value)
            {
                IsDragging = false;
            }
        }
    }

    public bool IsDragging { get; private set; }

    public bool TryBegin(PixelPoint pointerView, PixelPoint markerView)
    {
        var deltaX = pointerView.X - markerView.X;
        var deltaY = pointerView.Y - markerView.Y;
        IsDragging = IsEnabled
            && double.IsFinite(pointerView.X)
            && double.IsFinite(pointerView.Y)
            && double.IsFinite(markerView.X)
            && double.IsFinite(markerView.Y)
            && ((deltaX * deltaX) + (deltaY * deltaY) <= HitRadius * HitRadius);
        return IsDragging;
    }

    public bool TryComplete(PixelPoint imagePixel, out PixelPoint correctedPixel)
    {
        correctedPixel = default;
        if (!IsDragging || !double.IsFinite(imagePixel.X) || !double.IsFinite(imagePixel.Y))
        {
            IsDragging = false;
            return false;
        }

        IsDragging = false;
        correctedPixel = imagePixel;
        return true;
    }

    public bool TryRelease(
        PixelPoint imagePixel,
        Action releasePointerCapture,
        out PixelPoint correctedPixel)
    {
        ArgumentNullException.ThrowIfNull(releasePointerCapture);
        var completed = TryComplete(imagePixel, out correctedPixel);
        releasePointerCapture();
        return completed;
    }

    public void Cancel() => IsDragging = false;
}
