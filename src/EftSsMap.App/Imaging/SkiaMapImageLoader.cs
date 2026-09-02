using System.Security.Cryptography;
using EftSsMap.Core.Images;
using SkiaSharp;

namespace EftSsMap.App.Imaging;

public static class SkiaMapImageLoader
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    public static async Task<MapImageLoadResult> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure(MapImageLoadFailureKind.InvalidImage, "画像パスが指定されていません。");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Failure(MapImageLoadFailureKind.InvalidImage, "画像パスが無効です。");
        }

        if (!SupportedExtensions.Contains(Path.GetExtension(fullPath)))
        {
            return Failure(
                MapImageLoadFailureKind.UnsupportedFormat,
                "PNG、JPEG、WebP形式の画像を選択してください。");
        }

        try
        {
            await using var fingerprintStream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var hash = await SHA256.HashDataAsync(fingerprintStream, cancellationToken).ConfigureAwait(false);

            await using (var codecStream = OpenRead(fullPath))
            using (var codec = SKCodec.Create(codecStream, out _))
            {
                if (codec is null || !IsSupported(codec.EncodedFormat))
                {
                    return Failure(
                        codec is null
                            ? MapImageLoadFailureKind.InvalidImage
                            : MapImageLoadFailureKind.UnsupportedFormat,
                        codec is null
                            ? "画像データを認識できませんでした。"
                            : "画像データの形式がサポートされていません。");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            await using var decodeStream = OpenRead(fullPath);
            var bitmap = SKBitmap.Decode(decodeStream);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                bitmap?.Dispose();
                return Failure(MapImageLoadFailureKind.DecodeFailed, "画像をデコードできませんでした。");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                bitmap.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }

            SKImage? image = null;
            try
            {
                image = SKImage.FromBitmap(bitmap);
                if (image is null)
                {
                    bitmap.Dispose();
                    return Failure(MapImageLoadFailureKind.DecodeFailed, "画像をデコードできませんでした。");
                }

                var fingerprint = new ImageFingerprint(
                    fullPath,
                    bitmap.Width,
                    bitmap.Height,
                    Convert.ToHexString(hash).ToLowerInvariant());
                return MapImageLoadResult.Success(new LoadedMapImage(fingerprint, bitmap, image));
            }
            catch
            {
                image?.Dispose();
                bitmap.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return Failure(MapImageLoadFailureKind.Missing, "画像ファイルが見つかりません。");
        }
        catch (DirectoryNotFoundException)
        {
            return Failure(MapImageLoadFailureKind.Missing, "画像ファイルが見つかりません。");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(MapImageLoadFailureKind.AccessDenied, "画像ファイルを読み取る権限がありません。");
        }
        catch (IOException)
        {
            return Failure(MapImageLoadFailureKind.IoError, "画像ファイルを読み取れませんでした。");
        }
        catch (Exception)
        {
            return Failure(MapImageLoadFailureKind.InvalidImage, "画像をデコードできませんでした。");
        }
    }

    private static bool IsSupported(SKEncodedImageFormat format) =>
        format is SKEncodedImageFormat.Png
            or SKEncodedImageFormat.Jpeg
            or SKEncodedImageFormat.Webp;

    private static FileStream OpenRead(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 128 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static MapImageLoadResult Failure(
        MapImageLoadFailureKind failureKind,
        string message) =>
        MapImageLoadResult.Failure(failureKind, message);
}
