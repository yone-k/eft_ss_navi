using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Controls;

public sealed class MarkerCorrectionRequestedEventArgs(PixelPoint imagePixel) : EventArgs
{
    public PixelPoint ImagePixel { get; } = imagePixel;
}
