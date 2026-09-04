using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EftSsNavi.App.Updates;

public sealed class UpdateCheckService : IUpdateChecker
{
    private static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/yone-k/eft_ss_navi/releases/latest");
    private static readonly Regex ReleaseTagPattern = new(
        "^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex NormalizedVersionPattern = new(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$",
        RegexOptions.CultureInvariant);

    private readonly HttpClient httpClient;

    public UpdateCheckService(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        string? ignoredVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        try
        {
            var normalizedCurrent = NormalizeCurrentVersion(currentVersion);
            if (normalizedCurrent is null)
            {
                return UpdateCheckResult.Failed;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("EftSsNavi", normalizedCurrent.ToString(3)));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed;
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(
                content,
                cancellationToken: cancellationToken);
            if (release is null || release.Draft || release.Prerelease)
            {
                return UpdateCheckResult.Failed;
            }

            var latestVersion = ParseReleaseTag(release.TagName);
            if (latestVersion is null)
            {
                return UpdateCheckResult.Failed;
            }

            if (latestVersion <= normalizedCurrent)
            {
                return UpdateCheckResult.UpToDate;
            }

            var normalizedLatest = latestVersion.ToString(3);
            var ignored = ParseNormalizedVersion(ignoredVersion);
            if (ignored is not null && ignored == latestVersion)
            {
                return UpdateCheckResult.Suppressed;
            }

            var expectedAssetName = $"EftSsNavi-{release.TagName}-win-x64.zip";
            var asset = release.Assets?.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, expectedAssetName, StringComparison.Ordinal));
            if (asset is null
                || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri)
                || !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateCheckResult.Failed;
            }

            return UpdateCheckResult.Available(
                new UpdateCandidate(release.TagName!, normalizedLatest, downloadUri));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Canceled;
        }
        catch
        {
            return UpdateCheckResult.Failed;
        }
    }

    private static Version? NormalizeCurrentVersion(Version version) =>
        version.Build < 0 ? null : new Version(version.Major, version.Minor, version.Build);

    private static Version? ParseReleaseTag(string? tag)
    {
        if (tag is null || !ReleaseTagPattern.IsMatch(tag))
        {
            return null;
        }

        return Version.TryParse(tag.AsSpan(1), out var version) ? version : null;
    }

    private static Version? ParseNormalizedVersion(string? value)
    {
        if (value is null || !NormalizedVersionPattern.IsMatch(value))
        {
            return null;
        }

        return Version.TryParse(value, out var version) ? version : null;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public IReadOnlyList<GitHubAsset>? Assets { get; init; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
