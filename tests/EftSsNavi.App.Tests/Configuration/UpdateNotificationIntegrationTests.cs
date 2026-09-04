namespace EftSsNavi.App.Tests.Configuration;

public sealed class UpdateNotificationIntegrationTests
{
    [Fact]
    public void ShouldDelegateManualUpdateCheckToRootLauncher()
    {
        // Given: The main-window update menu handler.
        var source = ReadAppSource("MainWindow.xaml.cs");

        // Then: The app invokes the distribution-root launcher in its validated internal mode.
        Assert.Contains("Path.Combine(AppContext.BaseDirectory, \"..\", \"EftSsNavi.exe\")", source);
        Assert.Contains("\"--manual-update\"", source);
        Assert.Contains("\"--caller-pid\"", source);
        Assert.Contains("\"--caller-session-id\"", source);
        Assert.Contains("\"--caller-path\"", source);
        Assert.Contains("\"--shutdown-event\"", source);
        Assert.Contains("UseShellExecute = false", source);
    }

    [Fact]
    public void ShouldNotCheckForUpdatesDuringDirectApplicationStartup()
    {
        // Given: The main-window startup implementation.
        var source = ReadAppSource("MainWindow.xaml.cs");

        // Then: Initialization does not call GitHub or start the removed startup update flow.
        Assert.Contains("await InitializeAsync();", source);
        Assert.DoesNotContain("RunStartupUpdateCheckAsync", source);
        Assert.DoesNotContain("UpdateCheckService", source);
        Assert.DoesNotContain("GitHub", source);
    }

    [Fact]
    public void ShouldPreventConcurrentManualChecksAndRestoreMenuState()
    {
        var source = ReadAppSource("MainWindow.xaml.cs");
        Assert.Contains("OnCheckForUpdatesClick", source);
        Assert.Contains("_manualUpdateGate.WaitAsync(0)", source);
        Assert.Contains("CheckForUpdatesMenuItem.IsEnabled = false", source);
        Assert.Contains("CheckForUpdatesMenuItem.IsEnabled = true", source);
        Assert.Contains("launcherProcess.WaitForExitAsync", source);
        Assert.Contains("_manualUpdateCancellation.Token", source);
        Assert.Contains("manualUpdateWaitCancellation.Cancel();", source);
        Assert.Contains("_manualUpdateCancellation.Cancel();", source);
    }

    [Fact]
    public void ShouldKeepApplicationRunningAndShowErrorWhenLauncherIsMissing()
    {
        var source = ReadAppSource("MainWindow.xaml.cs");

        Assert.Contains("File.Exists(launcherPath)", source);
        Assert.Contains("アップデート用ランチャーが見つかりません。", source);
        Assert.Contains("ShowManualUpdateErrorAsync", source);
    }

    [Fact]
    public void ShouldConnectAboutMenuToInApplicationInformationDialogs()
    {
        var source = ReadAppSource("MainWindow.xaml.cs");

        Assert.Contains("OnAboutClick", source);
        Assert.Contains("AboutCoordinator.CreateDefault", source);
        Assert.Contains("await coordinator.ShowAsync", source);
    }

    [Fact]
    public void ShouldUseShellOnlyForHttpsDownloadLinks()
    {
        // Given: The external-link adapter.
        var source = ReadAppSource("About", "ShellExternalLinkLauncher.cs");

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
