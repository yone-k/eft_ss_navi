using System.Security.Cryptography;
using EftSsMap.App.Imaging;
using SkiaSharp;

namespace EftSsMap.App.Tests.Imaging;

public sealed class SkiaMapImageLoaderTests
{
    [Fact]
    public async Task ShouldLoadSyntheticPngAndReturnExactFingerprint()
    {
        var png = CreateSyntheticPng();
        var path = CreateTemporaryFile("map.png", png);
        try
        {
            var result = await SkiaMapImageLoader.LoadAsync(path);

            Assert.True(result.IsSuccess, $"{result.FailureKind}: {result.ErrorMessage}");
            using var image = Assert.IsType<LoadedMapImage>(result.Image);
            Assert.Equal(Path.GetFullPath(path), image.Fingerprint.Path);
            Assert.Equal(1, image.Fingerprint.Width);
            Assert.Equal(1, image.Fingerprint.Height);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant(),
                image.Fingerprint.Sha256);
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "map.jpg")]
    [InlineData(SKEncodedImageFormat.Webp, "map.webp")]
    public async Task ShouldLoadOtherSupportedImageFormats(
        SKEncodedImageFormat format,
        string fileName)
    {
        var encodedImage = CreateSyntheticImage(format);
        var path = CreateTemporaryFile(fileName, encodedImage);
        try
        {
            var result = await SkiaMapImageLoader.LoadAsync(path);

            Assert.True(result.IsSuccess, $"{result.FailureKind}: {result.ErrorMessage}");
            using var image = Assert.IsType<LoadedMapImage>(result.Image);
            Assert.Equal(1, image.Fingerprint.Width);
            Assert.Equal(1, image.Fingerprint.Height);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(encodedImage)).ToLowerInvariant(),
                image.Fingerprint.Sha256);
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public async Task ShouldReportMissingImageWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eft-ss-map-{Guid.NewGuid():N}", "missing.png");

        var result = await SkiaMapImageLoader.LoadAsync(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(MapImageLoadFailureKind.Missing, result.FailureKind);
    }

    [Fact]
    public async Task ShouldRejectInvalidBytesWithSupportedExtension()
    {
        var path = CreateTemporaryFile("invalid.png", [1, 2, 3, 4]);
        try
        {
            var result = await SkiaMapImageLoader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Equal(MapImageLoadFailureKind.InvalidImage, result.FailureKind);
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public async Task ShouldRejectUnsupportedExtensionBeforeDecode()
    {
        var path = CreateTemporaryFile("map.bmp", CreateSyntheticPng());
        try
        {
            var result = await SkiaMapImageLoader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Equal(MapImageLoadFailureKind.UnsupportedFormat, result.FailureKind);
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    private static string CreateTemporaryFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"eft-ss-map-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static byte[] CreateSyntheticPng()
        => CreateSyntheticImage(SKEncodedImageFormat.Png);

    private static byte[] CreateSyntheticImage(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality: 100);
        return data.ToArray();
    }

    private static void DeleteTemporaryFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
