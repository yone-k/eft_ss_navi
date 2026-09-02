using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Controls;

public sealed class MapImagePixelClickedEventArgs : EventArgs
{
    public MapImagePixelClickedEventArgs(PixelPoint imagePixel)
    {
        ImagePixel = imagePixel;
    }

    public PixelPoint ImagePixel { get; }
}
