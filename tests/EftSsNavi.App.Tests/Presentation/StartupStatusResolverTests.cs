using EftSsNavi.App.Presentation;

namespace EftSsNavi.App.Tests.Presentation;

public sealed class StartupStatusResolverTests
{
    [Fact]
    public void ShouldPrioritizeSettingsFailureOverOtherStartupState()
    {
        var message = StartupStatusResolver.Resolve(
            "settings failed",
            "watch failed",
            watchDirectoryConfigured: true);

        Assert.Equal("settings failed", message);
    }

    [Fact]
    public void ShouldPrioritizeWatchFailureWhenSettingsLoaded()
    {
        var message = StartupStatusResolver.Resolve(
            settingsFailureMessage: null,
            watchFailureMessage: "watch failed",
            watchDirectoryConfigured: false);

        Assert.Equal("watch failed", message);
    }

    [Fact]
    public void ShouldPromptForWatchDirectoryWhenNoneWasConfigured()
    {
        var message = StartupStatusResolver.Resolve(
            settingsFailureMessage: null,
            watchFailureMessage: null,
            watchDirectoryConfigured: false);

        Assert.Equal(StartupStatusResolver.ChooseWatchDirectoryMessage, message);
    }

    [Fact]
    public void ShouldPromptForProfileWhenWatchDirectoryWasConfigured()
    {
        var message = StartupStatusResolver.Resolve(
            settingsFailureMessage: null,
            watchFailureMessage: null,
            watchDirectoryConfigured: true);

        Assert.Equal(StartupStatusResolver.ChooseProfileMessage, message);
    }
}
