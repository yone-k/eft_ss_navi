using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class ExistingApplicationCoordinatorTests
{
    [Fact]
    public void ShouldForegroundExistingApplicationAndStopNormalLaunch()
    {
        var foregrounded = false;
        var coordinator = new ExistingApplicationCoordinator(() => true, () => foregrounded = true);
        Assert.True(coordinator.TryActivate()); Assert.True(foregrounded);
    }
    [Fact]
    public void ShouldContinueNormalLaunchWhenMutexDoesNotExist()
    {
        var foregrounded = false;
        var coordinator = new ExistingApplicationCoordinator(() => false, () => foregrounded = true);
        Assert.False(coordinator.TryActivate()); Assert.False(foregrounded);
    }
}
