using EftSsMap.Core.Images;
using SkiaSharp;

namespace EftSsMap.App.Imaging;

/// <summary>
/// Owns the decoded native image resources. Dispose it after the canvas no longer uses it.
/// </summary>
public sealed class LoadedMapImage : IDisposable
{
    private SKBitmap? _bitmap;
    private SKImage? _image;

    internal LoadedMapImage(ImageFingerprint fingerprint, SKBitmap bitmap, SKImage image)
    {
        Fingerprint = fingerprint;
        _bitmap = bitmap;
        _image = image;
    }

    public ImageFingerprint Fingerprint { get; }

    public SKImage Image => _image
        ?? throw new ObjectDisposedException(nameof(LoadedMapImage));

    public void Dispose()
    {
        Interlocked.Exchange(ref _image, null)?.Dispose();
        Interlocked.Exchange(ref _bitmap, null)?.Dispose();
    }
}
