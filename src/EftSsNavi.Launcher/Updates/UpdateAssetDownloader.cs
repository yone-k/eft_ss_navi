namespace EftSsNavi.Launcher.Updates;

public sealed class UpdateAssetDownloader(HttpClient httpClient)
{
    public async Task<string> DownloadAsync(Uri source, string transactionDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!string.Equals(source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only HTTPS downloads are allowed.", nameof(source));
        Directory.CreateDirectory(transactionDirectory);
        var destination = Path.Combine(transactionDirectory, UpdateCheckService.AssetName);
        var partial = destination + ".partial";
        try
        {
            using var response = await httpClient.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
            target.Close();
            File.Move(partial, destination, true);
            return destination;
        }
        catch
        {
            if (File.Exists(partial)) File.Delete(partial);
            throw;
        }
    }
}
