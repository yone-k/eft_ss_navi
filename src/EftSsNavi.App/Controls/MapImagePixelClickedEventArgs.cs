using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Controls;

public sealed class MapImagePixelClickedEventArgs : EventArgs
{
    public MapImagePixelClickedEventArgs(PixelPoint imagePixel)
    {
        ImagePixel = imagePixel;
    }

    public PixelPoint ImagePixel { get; }
}
