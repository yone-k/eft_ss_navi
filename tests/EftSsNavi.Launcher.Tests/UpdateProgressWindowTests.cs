using EftSsNavi.Launcher.Updates;

namespace EftSsNavi.Launcher.Tests;

public sealed class UpdateProgressWindowTests
{
    [Fact]
    public void ShouldOpenAndCloseNonCancelableNativeWindow()
    {
        using var window = new UpdateProgressWindow();
        Assert.True(window.IsOpen);
    }
}
