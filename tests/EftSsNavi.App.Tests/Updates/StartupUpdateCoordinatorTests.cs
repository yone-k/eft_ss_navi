using EftSsNavi.App.Updates;

namespace EftSsNavi.App.Tests.Updates;

public sealed class StartupUpdateCoordinatorTests
{
    private static readonly UpdateCandidate Candidate = new(
        "v0.10.0",
        "0.10.0",
        new Uri("https://example.test/EftSsNavi-v0.10.0-win-x64.zip"));

    [Fact]
    public async Task ShouldNotCheckForUpdateWhenFeatureIsDisabled()
    {
        // Given: A startup coordinator whose build policy disables update checks.
        var context = CreateContext(UpdatePromptChoice.Later);

        // When: Startup runs with update checks disabled.
        await context.Coordinator.RunAsync(
            enabled: false,
            new Version(0, 9, 0),
            ignoredVersion: null);

        // Then: GitHub and the prompt are not touched.
        Assert.Equal(0, context.Checker.CallCount);
        Assert.Equal(0, context.Prompt.ShowCount);
    }

    [Fact]
    public async Task ShouldCheckOnlyOnceAndSkipPromptWhenNoUpdateExists()
    {
        // Given: The update source reports no newer release.
        var context = CreateContext(UpdatePromptChoice.Later, hasUpdate: false);

        // When: Startup runs with update checks enabled.
        await context.Coordinator.RunAsync(
            enabled: true,
            new Version(0, 9, 0),
            ignoredVersion: "0.10.0");

        // Then: One check receives the running and ignored versions, without a prompt.
        Assert.Equal(1, context.Checker.CallCount);
        Assert.Equal(new Version(0, 9, 0), context.Checker.CurrentVersion);
        Assert.Equal("0.10.0", context.Checker.IgnoredVersion);
        Assert.Equal(0, context.Prompt.ShowCount);
    }

    [Theory]
    [InlineData(UpdatePromptChoice.Later)]
    [InlineData(UpdatePromptChoice.Unavailable)]
    public async Task ShouldLeaveStateUnchangedWhenUpdateIsDeferred(UpdatePromptChoice choice)
    {
        // Given: The prompt is closed, deferred, or unavailable.
        var context = CreateContext(choice);

        // When: Startup handles the available update.
        await context.Coordinator.RunAsync(true, new Version(0, 9, 0), ignoredVersion: null);

        // Then: Neither the browser nor settings are changed.
        Assert.Equal(0, context.Launcher.CallCount);
        Assert.Equal(0, context.Store.CallCount);
        Assert.Empty(context.Prompt.Errors);
    }

    [Fact]
    public async Task ShouldOpenArchiveWhenUpdateIsAccepted()
    {
        // Given: The user accepts the update.
        var context = CreateContext(UpdatePromptChoice.Update);

        // When: Startup handles the choice.
        await context.Coordinator.RunAsync(true, new Version(0, 9, 0), ignoredVersion: null);

        // Then: The matching archive is opened once without changing settings.
        Assert.Equal(1, context.Launcher.CallCount);
        Assert.Equal(Candidate.DownloadUri, context.Launcher.Uri);
        Assert.Equal(0, context.Store.CallCount);
        Assert.Empty(context.Prompt.Errors);
    }

    [Fact]
    public async Task ShouldPersistOnlyLatestVersionWhenSuppressionIsChosen()
    {
        // Given: The user suppresses this release.
        var context = CreateContext(UpdatePromptChoice.IgnoreVersion);

        // When: Startup handles the choice.
        await context.Coordinator.RunAsync(true, new Version(0, 9, 0), ignoredVersion: null);

        // Then: The normalized latest version is saved without opening the browser.
        Assert.Equal(1, context.Store.CallCount);
        Assert.Equal("0.10.0", context.Store.Version);
        Assert.Equal(0, context.Launcher.CallCount);
        Assert.Empty(context.Prompt.Errors);
    }

    [Fact]
    public async Task ShouldShowErrorWhenArchiveCannotBeOpened()
    {
        // Given: The shell rejects the selected download URL.
        var context = CreateContext(UpdatePromptChoice.Update, launchSucceeds: false);

        // When: Startup handles the choice.
        await context.Coordinator.RunAsync(true, new Version(0, 9, 0), ignoredVersion: null);

        // Then: A user-visible download error is requested.
        var error = Assert.Single(context.Prompt.Errors);
        Assert.Contains("ダウンロード", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldShowNextStartupWarningWhenSuppressionCannotBeSaved()
    {
        // Given: The settings repository cannot persist the suppressed version.
        var context = CreateContext(UpdatePromptChoice.IgnoreVersion, saveSucceeds: false);

        // When: Startup handles the choice.
        await context.Coordinator.RunAsync(true, new Version(0, 9, 0), ignoredVersion: null);

        // Then: The user is warned that this version can appear again next startup.
        var error = Assert.Single(context.Prompt.Errors);
        Assert.Contains("次回起動時", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotPropagatePromptFailureOrChangeState()
    {
        // Given: WinUI cannot display another ContentDialog on the same XamlRoot.
        var context = CreateContext(UpdatePromptChoice.Later);
        context.Prompt.ThrowOnShow = true;

        // When: Startup attempts to show the update prompt.
        var exception = await Record.ExceptionAsync(() => context.Coordinator.RunAsync(
            true,
            new Version(0, 9, 0),
            ignoredVersion: null));

        // Then: Normal startup continues without browser or settings changes.
        Assert.Null(exception);
        Assert.Equal(0, context.Launcher.CallCount);
        Assert.Equal(0, context.Store.CallCount);
    }

    [Fact]
    public void ShouldEnableUpdateChecksOnlyInReleaseBuilds()
    {
        // Given: The app and test project use the same build configuration.
        // When: The compile-time update policy is inspected.
        var enabled = UpdateCheckPolicy.IsEnabled;

        // Then: Debug never checks GitHub, while Release does.
#if DEBUG
        Assert.False(enabled);
#else
        Assert.True(enabled);
#endif
    }

    [Fact]
    public void ShouldDisableUpdateCheckWhenTestEnvironmentOptsOut()
    {
        // Given: An isolated startup test must not call the production GitHub API.
        // When: The internal test-only opt-out is evaluated.
        var enabled = UpdateCheckPolicy.ShouldRun("1");

        // Then: Update checks are disabled regardless of build configuration.
        Assert.False(enabled);
    }

    private static TestContext CreateContext(
        UpdatePromptChoice choice,
        bool hasUpdate = true,
        bool launchSucceeds = true,
        bool saveSucceeds = true)
    {
        var checker = new FakeUpdateChecker(hasUpdate ? Candidate : null);
        var prompt = new FakeUpdatePrompt(choice);
        var launcher = new FakeExternalLinkLauncher(launchSucceeds);
        var store = new FakeUpdateSuppressionStore(saveSucceeds);
        return new TestContext(
            new StartupUpdateCoordinator(checker, prompt, launcher, store),
            checker,
            prompt,
            launcher,
            store);
    }

    private sealed record TestContext(
        StartupUpdateCoordinator Coordinator,
        FakeUpdateChecker Checker,
        FakeUpdatePrompt Prompt,
        FakeExternalLinkLauncher Launcher,
        FakeUpdateSuppressionStore Store);

    private sealed class FakeUpdateChecker(UpdateCandidate? candidate) : IUpdateChecker
    {
        public UpdateCandidate? Candidate { get; set; } = candidate;

        public int CallCount { get; private set; }

        public Version? CurrentVersion { get; private set; }

        public string? IgnoredVersion { get; private set; }

        public Task<UpdateCandidate?> CheckAsync(
            Version currentVersion,
            string? ignoredVersion,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CurrentVersion = currentVersion;
            IgnoredVersion = ignoredVersion;
            return Task.FromResult(Candidate);
        }
    }

    private sealed class FakeUpdatePrompt(UpdatePromptChoice choice) : IUpdatePrompt
    {
        public bool ThrowOnShow { get; set; }

        public int ShowCount { get; private set; }

        public List<string> Errors { get; } = [];

        public Task<UpdatePromptChoice> ShowUpdateAsync(
            UpdateCandidate candidate,
            string currentVersion,
            CancellationToken cancellationToken)
        {
            ShowCount++;
            if (ThrowOnShow)
            {
                throw new InvalidOperationException("Another dialog is open.");
            }

            return Task.FromResult(choice);
        }

        public Task ShowErrorAsync(string message, CancellationToken cancellationToken)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExternalLinkLauncher(bool succeeds) : IExternalLinkLauncher
    {
        public int CallCount { get; private set; }

        public Uri? Uri { get; private set; }

        public bool TryOpen(Uri uri)
        {
            CallCount++;
            Uri = uri;
            return succeeds;
        }
    }

    private sealed class FakeUpdateSuppressionStore(bool succeeds) : IUpdateSuppressionStore
    {
        public int CallCount { get; private set; }

        public string? Version { get; private set; }

        public bool TrySave(string normalizedVersion)
        {
            CallCount++;
            Version = normalizedVersion;
            return succeeds;
        }
    }
}
