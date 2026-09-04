using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Tests;

public sealed class LauncherStateTests
{
    [Fact]
    public void ShouldSkipAutomaticCheckWithinCacheButManualBypassesIt()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var state = new LauncherState { LastCheckedAt = now.AddHours(-23) };
        Assert.False(UpdateEligibility.ShouldCheck(UpdateCheckMode.Automatic, state, now));
        Assert.True(UpdateEligibility.ShouldCheck(UpdateCheckMode.Manual, state, now));
    }

    [Fact]
    public void ShouldSuppressFailedVersionForBothModesForTwentyFourHours()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var state = new LauncherState { FailedVersion = "1.2.3", FailedAt = now.AddHours(-23) };
        Assert.False(UpdateEligibility.CanOffer("1.2.3", UpdateCheckMode.Automatic, state, now));
        Assert.False(UpdateEligibility.CanOffer("1.2.3", UpdateCheckMode.Manual, state, now));
        Assert.True(UpdateEligibility.CanOffer("1.2.4", UpdateCheckMode.Automatic, state, now));
    }

    [Fact]
    public void ShouldBypassIgnoredVersionOnlyForManualCheck()
    {
        var state = new LauncherState { IgnoredVersion = "1.2.3" };
        var now = DateTimeOffset.UnixEpoch;
        Assert.False(UpdateEligibility.CanOffer("1.2.3", UpdateCheckMode.Automatic, state, now));
        Assert.True(UpdateEligibility.CanOffer("1.2.3", UpdateCheckMode.Manual, state, now));
    }
}
