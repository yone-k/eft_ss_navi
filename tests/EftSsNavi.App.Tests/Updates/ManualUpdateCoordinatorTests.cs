using EftSsNavi.App.Updates;

namespace EftSsNavi.App.Tests.Updates;

public sealed class ManualUpdateCoordinatorTests
{
    [Fact]
    public async Task ShouldOfferAvailableUpdateWithoutApplyingSuppression()
    {
        // Given: A manually requested check finds a newer release.
        var candidate = new UpdateCandidate(
            "v1.2.0",
            "1.2.0",
            new Uri("https://example.test/EftSsNavi-v1.2.0-win-x64.zip"));
        var checker = new FakeChecker(UpdateCheckResult.Available(candidate));
        var prompt = new FakePrompt { UpdateChoice = UpdatePromptChoice.Update };
        var launcher = new FakeLauncher();
        var coordinator = new ManualUpdateCoordinator(checker, prompt, launcher);

        // When: The user checks from the Help menu.
        await coordinator.RunAsync(new Version(1, 1, 0));

        // Then: Ignored-version filtering is bypassed and the safe download opens.
        Assert.Null(checker.IgnoredVersion);
        Assert.Same(candidate, prompt.Candidate);
        Assert.Equal("v1.1.0", prompt.CurrentVersion);
        Assert.Equal(candidate.DownloadUri, launcher.OpenedUri);
    }

    [Fact]
    public async Task ShouldReportWhenApplicationIsUpToDate()
    {
        var prompt = new FakePrompt();
        var coordinator = new ManualUpdateCoordinator(
            new FakeChecker(UpdateCheckResult.UpToDate),
            prompt,
            new FakeLauncher());

        await coordinator.RunAsync(new Version(1, 1, 0, 42));

        Assert.Equal("v1.1.0", prompt.UpToDateVersion);
        Assert.Empty(prompt.Errors);
    }

    [Fact]
    public async Task ShouldReportCheckFailure()
    {
        var prompt = new FakePrompt();
        var coordinator = new ManualUpdateCoordinator(
            new FakeChecker(UpdateCheckResult.Failed),
            prompt,
            new FakeLauncher());

        await coordinator.RunAsync(new Version(1, 1, 0));

        Assert.Contains(prompt.Errors, message => message.Contains("確認できませんでした", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.1")]
    public async Task ShouldFailWithoutNetworkWhenCurrentVersionIsUnavailable(string? version)
    {
        var checker = new FakeChecker(UpdateCheckResult.UpToDate);
        var prompt = new FakePrompt();
        var coordinator = new ManualUpdateCoordinator(checker, prompt, new FakeLauncher());
        var currentVersion = version is null ? null : Version.Parse(version);

        await coordinator.RunAsync(currentVersion);

        Assert.Equal(0, checker.CallCount);
        Assert.Contains(prompt.Errors, message => message.Contains("バージョン", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldNotShowResultAfterCancellation()
    {
        var prompt = new FakePrompt();
        var coordinator = new ManualUpdateCoordinator(
            new FakeChecker(UpdateCheckResult.Canceled),
            prompt,
            new FakeLauncher());

        await coordinator.RunAsync(new Version(1, 1, 0));

        Assert.Null(prompt.Candidate);
        Assert.Null(prompt.UpToDateVersion);
        Assert.Empty(prompt.Errors);
    }

    private sealed class FakeChecker(UpdateCheckResult result) : IUpdateChecker
    {
        public int CallCount { get; private set; }

        public string? IgnoredVersion { get; private set; }

        public Task<UpdateCheckResult> CheckAsync(
            Version currentVersion,
            string? ignoredVersion,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            IgnoredVersion = ignoredVersion;
            return Task.FromResult(result);
        }
    }

    private sealed class FakePrompt : IManualUpdatePrompt
    {
        public UpdatePromptChoice UpdateChoice { get; init; } = UpdatePromptChoice.Later;

        public UpdateCandidate? Candidate { get; private set; }

        public string? CurrentVersion { get; private set; }

        public string? UpToDateVersion { get; private set; }

        public List<string> Errors { get; } = [];

        public Task<UpdatePromptChoice> ShowUpdateAsync(
            UpdateCandidate candidate,
            string currentVersion,
            CancellationToken cancellationToken)
        {
            Candidate = candidate;
            CurrentVersion = currentVersion;
            return Task.FromResult(UpdateChoice);
        }

        public Task ShowUpToDateAsync(string currentVersion, CancellationToken cancellationToken)
        {
            UpToDateVersion = currentVersion;
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string message, CancellationToken cancellationToken)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLauncher : IExternalLinkLauncher
    {
        public Uri? OpenedUri { get; private set; }

        public bool TryOpen(Uri uri)
        {
            OpenedUri = uri;
            return true;
        }
    }
}
