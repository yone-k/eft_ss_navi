using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class StartupVerifierTests
{
    [Fact]
    public async Task ShouldSucceedWhenStartedApplicationSignalsEvent()
    {
        IReadOnlyList<string>? observed = null;
        var result = await new StartupVerifier().StartAndWaitAsync("app.exe", TimeSpan.FromSeconds(2), args =>
        {
            observed = args;
            using var handle = EventWaitHandle.OpenExisting(args[1]); handle.Set();
        });
        Assert.True(result);
        Assert.Equal("--startup-success-event", observed?[0]);
    }

    [Fact]
    public async Task ShouldFailWhenApplicationDoesNotSignalBeforeTimeout()
    {
        var result = await new StartupVerifier().StartAndWaitAsync("app.exe", TimeSpan.FromMilliseconds(10), _ => { });
        Assert.False(result);
    }

    [Fact]
    public async Task ShouldVerifyUpdateByLaunchingNewRootLauncherMode()
    {
        IReadOnlyList<string>? observed = null;
        var result = await new UpdatedLauncherVerifier().StartAndWaitAsync(
            "EftSsNavi.exe",
            TimeSpan.FromSeconds(1),
            (_, arguments, _) =>
            {
                observed = arguments;
                return Task.FromResult<int?>(0);
            });

        Assert.True(result);
        Assert.Equal(["--verify-startup"], observed);
    }

    [Fact]
    public async Task ShouldFailUpdateVerificationWhenNewRootLauncherReturnsNonZero()
    {
        var result = await new UpdatedLauncherVerifier().StartAndWaitAsync(
            "EftSsNavi.exe",
            TimeSpan.FromSeconds(1),
            (_, _, _) => Task.FromResult<int?>(5));

        Assert.False(result);
    }
}
