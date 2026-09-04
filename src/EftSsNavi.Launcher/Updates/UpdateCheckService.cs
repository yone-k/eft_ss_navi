using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EftSsNavi.Launcher.Updates;

public sealed partial class UpdateCheckService(HttpClient httpClient) : IUpdateChecker
{
    public const string AssetName = "EftSsNavi-win-x64.zip";
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/yone-k/eft_ss_navi/releases/latest");

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        try
        {
            if (currentVersion.Build < 0) return UpdateCheckResult.Failed;
            var current = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build);
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("EftSsNavi", current.ToString(3)));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return UpdateCheckResult.Failed;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<Release>(stream, cancellationToken: cancellationToken);
            if (release is null || release.Draft || release.Prerelease || release.TagName is null || !TagRegex().IsMatch(release.TagName)) return UpdateCheckResult.Failed;
            if (!Version.TryParse(release.TagName.AsSpan(1), out var latest)) return UpdateCheckResult.Failed;
            if (latest <= current) return UpdateCheckResult.UpToDate;
            var matches = release.Assets?.Where(x => string.Equals(x.Name, AssetName, StringComparison.Ordinal)).ToArray();
            if (matches is not { Length: 1 }) return UpdateCheckResult.Failed;
            var asset = matches[0];
            if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return UpdateCheckResult.Failed;
            var match = asset.Digest is null ? null : DigestRegex().Match(asset.Digest);
            if (match is null || !match.Success) return UpdateCheckResult.Failed;
            return UpdateCheckResult.Available(new(release.TagName, latest.ToString(3), uri, match.Groups[1].Value.ToLowerInvariant()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return UpdateCheckResult.Canceled; }
        catch { return UpdateCheckResult.Failed; }
    }

    [GeneratedRegex("^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();
    [GeneratedRegex("^sha256:([0-9a-fA-F]{64})$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestRegex();

    private sealed class Release
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("assets")] public IReadOnlyList<Asset>? Assets { get; init; }
    }
    private sealed class Asset
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("browser_download_url")] public string? Url { get; init; }
        [JsonPropertyName("digest")] public string? Digest { get; init; }
    }
}
