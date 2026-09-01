using EftSsMap.App.Imaging;

namespace EftSsMap.App.Tests.Imaging;

public sealed class LatestImageLoadTrackerTests
{
    [Fact]
    public void ShouldInvalidateEarlierLoadWhenNewLoadBegins()
    {
        var tracker = new LatestImageLoadTracker();
        var first = tracker.Begin();

        var second = tracker.Begin();

        Assert.False(tracker.IsCurrent(first));
        Assert.True(tracker.IsCurrent(second));
    }

    [Fact]
    public void ShouldInvalidateCurrentLoadExplicitly()
    {
        var tracker = new LatestImageLoadTracker();
        var generation = tracker.Begin();

        tracker.Invalidate();

        Assert.False(tracker.IsCurrent(generation));
    }

    [Fact]
    public void ShouldRejectEveryLoadAfterClose()
    {
        var tracker = new LatestImageLoadTracker();
        var startedBeforeClose = tracker.Begin();

        tracker.Close();
        var startedAfterClose = tracker.Begin();

        Assert.False(tracker.IsCurrent(startedBeforeClose));
        Assert.False(tracker.IsCurrent(startedAfterClose));
    }
}
