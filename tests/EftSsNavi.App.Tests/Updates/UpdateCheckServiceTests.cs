using System.Net;
using System.Text;
using EftSsNavi.App.Updates;

namespace EftSsNavi.App.Tests.Updates;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task ShouldReturnMatchingNewerReleaseWhenGitHubResponseIsValid()
    {
        // Given: GitHub returns a newer stable release and its matching Windows archive.
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReleaseJson(
            "v0.10.0",
            "EftSsNavi-v0.10.0-win-x64.zip",
            "https://github.com/yone-k/eft_ss_navi/releases/download/v0.10.0/EftSsNavi-v0.10.0-win-x64.zip")));
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: A four-component assembly version checks for an update.
        var result = await service.CheckAsync(new Version(0, 9, 0, 42), ignoredVersion: null);

        // Then: The revision is ignored and the matching release is returned.
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        var candidate = Assert.IsType<UpdateCandidate>(result.Candidate);
        Assert.Equal("v0.10.0", candidate.DisplayVersion);
        Assert.Equal("0.10.0", candidate.NormalizedVersion);
        Assert.Equal(
            "https://github.com/yone-k/eft_ss_navi/releases/download/v0.10.0/EftSsNavi-v0.10.0-win-x64.zip",
            candidate.DownloadUri.AbsoluteUri);
    }

    [Fact]
    public async Task ShouldSendRequiredHeadersToLatestReleaseEndpoint()
    {
        // Given: A handler that records the outgoing GitHub request.
        HttpRequestMessage? observedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            observedRequest = request;
            return JsonResponse(ReleaseJson(
                "v0.2.0",
                "EftSsNavi-v0.2.0-win-x64.zip",
                "https://github.com/yone-k/eft_ss_navi/releases/download/v0.2.0/EftSsNavi-v0.2.0-win-x64.zip"));
        });
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: The latest release is checked.
        _ = await service.CheckAsync(new Version(0, 1, 0), ignoredVersion: null);

        // Then: The request targets the fixed API and identifies the application.
        Assert.NotNull(observedRequest);
        Assert.Equal(
            "https://api.github.com/repos/yone-k/eft_ss_navi/releases/latest",
            observedRequest.RequestUri?.AbsoluteUri);
        Assert.Contains(observedRequest.Headers.UserAgent, value => value.Product?.Name == "EftSsNavi");
        Assert.Contains(
            observedRequest.Headers.Accept,
            value => value.MediaType == "application/vnd.github+json");
    }

    [Theory]
    [InlineData("v0.9.0")]
    [InlineData("v0.8.9")]
    public async Task ShouldReturnUpToDateWhenLatestVersionIsNotNewer(string latestTag)
    {
        // Given: GitHub returns the same or an older release.
        using var client = CreateClient(latestTag);
        var service = new UpdateCheckService(client);

        // When: The release is compared with the running version.
        var result = await service.CheckAsync(new Version(0, 9, 0, 99), ignoredVersion: null);

        // Then: The caller can distinguish an up-to-date result from a failure.
        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnSuppressedWhenLatestVersionWasIgnored()
    {
        // Given: The latest release is the version the user suppressed.
        using var client = CreateClient("v0.10.0");
        var service = new UpdateCheckService(client);

        // When: The release is checked with that ignored version.
        var result = await service.CheckAsync(new Version(0, 9, 0), ignoredVersion: "0.10.0");

        // Then: The caller can preserve the startup notification suppression policy.
        Assert.Equal(UpdateCheckStatus.Suppressed, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnReleaseWhenItIsNewerThanIgnoredVersion()
    {
        // Given: The user ignored an older update and GitHub now has a newer one.
        using var client = CreateClient("v0.11.0");
        var service = new UpdateCheckService(client);

        // When: The newer release is checked.
        var result = await service.CheckAsync(new Version(0, 9, 0), ignoredVersion: "0.10.0");

        // Then: The future release is offered.
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("0.11.0", result.Candidate?.NormalizedVersion);
    }

    [Theory]
    [InlineData("0.10.0")]
    [InlineData("v0.10")]
    [InlineData("v0.10.0.0")]
    [InlineData("v0.10.0-beta")]
    [InlineData("v00.10.0")]
    public async Task ShouldReturnFailureWhenTagIsNotStrictThreePartVersion(string invalidTag)
    {
        // Given: GitHub returns a release with a tag outside vX.Y.Z.
        using var client = CreateClient(invalidTag);
        var service = new UpdateCheckService(client);

        // When: The release is checked.
        var result = await service.CheckAsync(new Version(0, 1, 0), ignoredVersion: null);

        // Then: The malformed release is reported as a failed check.
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnFailureWhenArchiveNameDoesNotMatchTag()
    {
        // Given: The asset name belongs to another version.
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReleaseJson(
            "v0.10.0",
            "EftSsNavi-v0.9.0-win-x64.zip",
            "https://github.com/yone-k/eft_ss_navi/releases/download/v0.10.0/EftSsNavi-v0.9.0-win-x64.zip")));
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: The release is checked.
        var result = await service.CheckAsync(new Version(0, 9, 0), ignoredVersion: null);

        // Then: The missing matching archive is reported as a failed check.
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnFailureWhenArchiveUrlIsNotHttps()
    {
        // Given: The matching asset has a non-HTTPS URL.
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReleaseJson(
            "v0.10.0",
            "EftSsNavi-v0.10.0-win-x64.zip",
            "http://example.test/EftSsNavi-v0.10.0-win-x64.zip")));
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: The release is checked.
        var result = await service.CheckAsync(new Version(0, 9, 0), ignoredVersion: null);

        // Then: The unsafe URL is reported as a failed check.
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnFailureForDraftOrPrereleaseResponse()
    {
        // Given: The API unexpectedly returns a prerelease.
        var json = ReleaseJson(
            "v0.10.0",
            "EftSsNavi-v0.10.0-win-x64.zip",
            "https://example.test/EftSsNavi-v0.10.0-win-x64.zip",
            prerelease: true);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(json));
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: The response is checked.
        var result = await service.CheckAsync(new Version(0, 9, 0), ignoredVersion: null);

        // Then: The unusable release is reported as a failed check.
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Null(result.Candidate);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.OK, "not-json")]
    public async Task ShouldReturnFailureWhenResponseCannotBeUsed(HttpStatusCode statusCode, string body)
    {
        // Given: GitHub returns a failed status or invalid JSON.
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: The response is checked.
        var result = await service.CheckAsync(new Version(0, 9, 0), ignoredVersion: null);

        // Then: The caller can distinguish the failure from an up-to-date result.
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnCanceledWhenRequestIsCanceled()
    {
        // Given: The GitHub request observes cancellation.
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("{}");
        });
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // When: The canceled request is checked.
        var result = await service.CheckAsync(
            new Version(0, 9, 0),
            ignoredVersion: null,
            cancellation.Token);

        // Then: Cancellation is distinct from a failed check.
        Assert.Equal(UpdateCheckStatus.Canceled, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnFailureWhenRequestThrows()
    {
        // Given: The HTTP transport fails before a response is received.
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Network unavailable.")));
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: The release is checked.
        var result = await service.CheckAsync(new Version(0, 9, 0), ignoredVersion: null);

        // Then: The transport error is represented as a failed check.
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task ShouldReturnFailureWithoutRequestWhenCurrentVersionCannotBeNormalized()
    {
        // Given: A handler that records whether an HTTP request is attempted.
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return JsonResponse("{}");
        });
        using var client = new HttpClient(handler);
        var service = new UpdateCheckService(client);

        // When: A two-component current version is checked.
        var result = await service.CheckAsync(new Version(0, 9), ignoredVersion: null);

        // Then: Validation fails locally without contacting GitHub.
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal(0, requestCount);
    }

    private static HttpClient CreateClient(string tag)
    {
        var archiveName = $"EftSsNavi-{tag}-win-x64.zip";
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReleaseJson(
            tag,
            archiveName,
            $"https://example.test/{archiveName}")));
        return new HttpClient(handler);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static string ReleaseJson(
        string tag,
        string assetName,
        string downloadUrl,
        bool prerelease = false) =>
        $$"""
        {
          "tag_name": "{{tag}}",
          "draft": false,
          "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
          "assets": [
            {
              "name": "{{assetName}}",
              "browser_download_url": "{{downloadUrl}}"
            }
          ]
        }
        """;

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this((request, _) => Task.FromResult(handler(request)))
        {
        }

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            this.handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
