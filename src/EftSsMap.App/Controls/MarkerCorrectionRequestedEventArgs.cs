using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Controls;

public sealed class MarkerCorrectionRequestedEventArgs(PixelPoint imagePixel) : EventArgs
{
    public PixelPoint ImagePixel { get; } = imagePixel;
}
