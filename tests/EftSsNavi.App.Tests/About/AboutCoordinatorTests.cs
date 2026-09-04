using EftSsNavi.App.About;

namespace EftSsNavi.App.Tests.About;

public sealed class AboutCoordinatorTests
{
    [Fact]
    public async Task ShouldShowLicenseOnlyAfterAboutDialogCloses()
    {
        // Given: The About dialog selects a readable license notice.
        var dialog = new FakeAboutDialog(AboutDialogChoice.ShowLicenses);
        var coordinator = CreateCoordinator(dialog, "license text");

        // When: The public display operation runs.
        await coordinator.ShowAsync();

        // Then: The separate license dialog starts after the About dialog completes.
        Assert.Equal(["about:start", "about:end", "licenses"], dialog.Events);
        Assert.Equal("license text", dialog.LicenseContent);
        Assert.Empty(dialog.Errors);
    }

    [Fact]
    public async Task ShouldShowErrorInsteadOfLicenseDialogWhenNoticeIsMissing()
    {
        // Given: The About dialog selects a missing license notice.
        var dialog = new FakeAboutDialog(AboutDialogChoice.ShowLicenses);
        var coordinator = CreateCoordinator(dialog, licenseContent: null);

        // When: The public display operation runs.
        await coordinator.ShowAsync();

        // Then: No empty license dialog is requested and an error is shown.
        Assert.Null(dialog.LicenseContent);
        Assert.Single(dialog.Errors);
    }

    [Fact]
    public async Task ShouldOpenFixedRepositoryWhenGitHubIsSelected()
    {
        // Given: The About dialog selects the GitHub action.
        var dialog = new FakeAboutDialog(AboutDialogChoice.OpenGitHub);
        var launcher = new FakeExternalLinkLauncher(succeeds: true);
        var coordinator = CreateCoordinator(dialog, "license text", launcher);

        // When: The public display operation runs.
        await coordinator.ShowAsync();

        // Then: The fixed HTTPS repository is opened.
        Assert.Equal(new Uri("https://github.com/yone-k/eft_ss_navi"), launcher.Uri);
        Assert.Empty(dialog.Errors);
    }

    [Fact]
    public async Task ShouldShowErrorWhenGitHubCannotBeOpened()
    {
        // Given: The shell cannot open the GitHub repository.
        var dialog = new FakeAboutDialog(AboutDialogChoice.OpenGitHub);
        var launcher = new FakeExternalLinkLauncher(succeeds: false);
        var coordinator = CreateCoordinator(dialog, "license text", launcher);

        // When: The public display operation runs.
        await coordinator.ShowAsync();

        // Then: The failure is made visible to the user.
        Assert.Single(dialog.Errors);
    }

    private static AboutCoordinator CreateCoordinator(
        FakeAboutDialog dialog,
        string? licenseContent,
        FakeExternalLinkLauncher? launcher = null)
    {
        var fileSystem = new FakeLicenseNoticeFileSystem(licenseContent);
        return new AboutCoordinator(
            dialog,
            new LicenseNoticeReader(@"C:\app", fileSystem),
            launcher ?? new FakeExternalLinkLauncher(succeeds: true),
            () => new Version(1, 2, 3, 4));
    }

    private sealed class FakeAboutDialog(AboutDialogChoice choice) : IAboutDialog
    {
        public List<string> Events { get; } = [];

        public string? LicenseContent { get; private set; }

        public List<string> Errors { get; } = [];

        public Task<AboutDialogChoice> ShowAboutAsync(
            AboutInformation information,
            CancellationToken cancellationToken)
        {
            Events.Add("about:start");
            Assert.Equal("EFT Screenshot Navi", information.ApplicationName);
            Assert.Equal("1.2.3", information.Version);
            Events.Add("about:end");
            return Task.FromResult(choice);
        }

        public Task ShowLicensesAsync(string content, CancellationToken cancellationToken)
        {
            Events.Add("licenses");
            LicenseContent = content;
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string message, CancellationToken cancellationToken)
        {
            Events.Add("error");
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLicenseNoticeFileSystem(string? content) : ILicenseNoticeFileSystem
    {
        public bool Exists(string path) => content is not null;

        public string ReadAllText(string path) => content!;
    }

    private sealed class FakeExternalLinkLauncher(bool succeeds) : IExternalLinkLauncher
    {
        public Uri? Uri { get; private set; }

        public bool TryOpen(Uri uri)
        {
            Uri = uri;
            return succeeds;
        }
    }
}
