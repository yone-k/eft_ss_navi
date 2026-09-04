namespace EftSsNavi.App.Tests.Configuration;

public sealed class UpdateNotificationIntegrationTests
{
    [Fact]
    public void ShouldConfigureAllThreeUpdateChoicesAndVersionText()
    {
        // Given: The WinUI update prompt adapter.
        var source = ReadAppSource("Updates", "WinUiUpdatePrompt.cs");

        // Then: It shows both versions and maps the three requested choices.
        Assert.Contains("新しいバージョンがあります", source);
        Assert.Contains("現在: {currentVersion}", source);
        Assert.Contains("最新: {candidate.DisplayVersion}", source);
        Assert.Contains("PrimaryButtonText = \"アップデートする\"", source);
        Assert.Contains("SecondaryButtonText = \"このバージョンの通知はもうしない\"", source);
        Assert.Contains("CloseButtonText = \"今はしない\"", source);
        Assert.Contains("ContentDialogResult.Primary => UpdatePromptChoice.Update", source);
        Assert.Contains("ContentDialogResult.Secondary => UpdatePromptChoice.IgnoreVersion", source);
        Assert.Contains("_ => UpdatePromptChoice.Later", source);
    }

    [Fact]
    public void ShouldStartOneCancelableFiveSecondCheckAfterMainInitialization()
    {
        // Given: The main-window startup implementation.
        var source = ReadAppSource("MainWindow.xaml.cs");

        // Then: Loaded initialization starts a non-blocking, bounded check and shutdown cancels it.
        Assert.Contains("await InitializeAsync();", source);
        Assert.Contains("_ = RunUpdateCheckAsync();", source);
        Assert.Contains("TimeSpan.FromSeconds(5)", source);
        Assert.Contains("UpdateCheckPolicy.ShouldRun(", source);
        Assert.Contains("EFTSSNAVI_DISABLE_UPDATE_CHECK", source);
        Assert.Contains("_updateCheckCancellation.Cancel();", source);
        Assert.Contains("_ignoredUpdateVersion", source);
        Assert.Contains("_stunServers,\r\n            _ignoredUpdateVersion", source.ReplaceLineEndings("\r\n"));
    }

    [Fact]
    public void ShouldUseShellOnlyForHttpsDownloadLinks()
    {
        // Given: The external-link adapter.
        var source = ReadAppSource("Updates", "ShellExternalLinkLauncher.cs");

        // Then: It validates HTTPS and lets Windows choose the default browser.
        Assert.Contains("Uri.UriSchemeHttps", source);
        Assert.Contains("UseShellExecute = true", source);
        Assert.Contains("Process.Start", source);
    }

    private static string ReadAppSource(params string[] pathParts)
    {
        var parts = new List<string> { FindRepositoryRoot(), "src", "EftSsNavi.App" };
        parts.AddRange(pathParts);
        return File.ReadAllText(Path.Combine(parts.ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsNavi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
