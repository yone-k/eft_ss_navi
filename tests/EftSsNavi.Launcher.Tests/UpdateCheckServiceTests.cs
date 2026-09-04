using System.Net;
using System.Text;
using EftSsNavi.Launcher.Updates;

namespace EftSsNavi.Launcher.Tests;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task ShouldReturnCandidateWithDigestWhenStableReleaseIsValid()
    {
        using var client = Client(Json("v1.2.3", "sha256:" + new string('a', 64)));
        var result = await new UpdateCheckService(client).CheckAsync(new Version(1, 2, 2));
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.3", result.Candidate?.NormalizedVersion);
        Assert.Equal(new string('a', 64), result.Candidate?.Sha256);
        Assert.Equal("EftSsNavi-win-x64.zip", result.Candidate?.DownloadUri.Segments[^1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256:nope")]
    [InlineData("md5:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task ShouldFailWhenDigestIsMissingOrInvalid(string? digest)
    {
        using var client = Client(Json("v1.2.3", digest));
        var result = await new UpdateCheckService(client).CheckAsync(new Version(1, 2, 2));
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Theory]
    [InlineData("v01.2.3")]
    [InlineData("v1.2")]
    [InlineData("v1.2.3-beta")]
    public async Task ShouldFailWhenTagIsNotStrictThreePartVersion(string tag)
    {
        using var client = Client(Json(tag, "sha256:" + new string('b', 64)));
        var result = await new UpdateCheckService(client).CheckAsync(new Version(1, 2, 2));
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    private static HttpClient Client(string body) => new(new Handler(body));
    private static string Json(string tag, string? digest) => $$"""
        {"tag_name":"{{tag}}","draft":false,"prerelease":false,"assets":[{"name":"EftSsNavi-win-x64.zip","browser_download_url":"https://example.test/EftSsNavi-win-x64.zip","digest":{{(digest is null ? "null" : $"\"{digest}\"")}}}]}
        """;
    private sealed class Handler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
