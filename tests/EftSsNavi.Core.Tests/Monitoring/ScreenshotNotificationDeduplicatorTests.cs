using EftSsNavi.Core.Monitoring;

namespace EftSsNavi.Core.Tests.Monitoring;

public sealed class ScreenshotNotificationDeduplicatorTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_accept_notification_when_path_is_seen_for_the_first_time()
    {
        // Given
        var timeProvider = new ManualTimeProvider(InitialTime);
        var deduplicator = new ScreenshotNotificationDeduplicator(timeProvider);

        // When
        var accepted = deduplicator.ShouldAccept(Path.Combine("captures", "first.png"));

        // Then
        Assert.True(accepted);
    }

    [Fact]
    public void Should_reject_same_normalized_path_ignoring_case_when_elapsed_time_is_just_under_two_seconds()
    {
        // Given
        var timeProvider = new ManualTimeProvider(InitialTime);
        var deduplicator = new ScreenshotNotificationDeduplicator(timeProvider);
        var relativePath = Path.Combine("captures", "same.png");
        Assert.True(deduplicator.ShouldAccept(relativePath));
        timeProvider.Advance(TimeSpan.FromMilliseconds(1999));

        // When
        var accepted = deduplicator.ShouldAccept(Path.GetFullPath(relativePath).ToUpperInvariant());

        // Then
        Assert.False(accepted);
    }

    [Fact]
    public void Should_accept_same_path_when_elapsed_time_is_exactly_two_seconds()
    {
        // Given
        var timeProvider = new ManualTimeProvider(InitialTime);
        var deduplicator = new ScreenshotNotificationDeduplicator(timeProvider);
        const string path = "boundary.png";
        Assert.True(deduplicator.ShouldAccept(path));
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // When
        var accepted = deduplicator.ShouldAccept(path);

        // Then
        Assert.True(accepted);
    }

    [Fact]
    public void Should_accept_same_path_when_elapsed_time_is_over_two_seconds()
    {
        // Given
        var timeProvider = new ManualTimeProvider(InitialTime);
        var deduplicator = new ScreenshotNotificationDeduplicator(timeProvider);
        const string path = "over-boundary.png";
        Assert.True(deduplicator.ShouldAccept(path));
        timeProvider.Advance(TimeSpan.FromMilliseconds(2001));

        // When
        var accepted = deduplicator.ShouldAccept(path);

        // Then
        Assert.True(accepted);
    }

    [Fact]
    public void Should_accept_different_path_when_inside_duplicate_window()
    {
        // Given
        var timeProvider = new ManualTimeProvider(InitialTime);
        var deduplicator = new ScreenshotNotificationDeduplicator(timeProvider);
        Assert.True(deduplicator.ShouldAccept("first.png"));
        timeProvider.Advance(TimeSpan.FromMilliseconds(1));

        // When
        var accepted = deduplicator.ShouldAccept("second.png");

        // Then
        Assert.True(accepted);
    }

    [Fact]
    public void Should_remove_paths_after_duplicate_window_expires()
    {
        // Given
        var timeProvider = new ManualTimeProvider(InitialTime);
        var deduplicator = new ScreenshotNotificationDeduplicator(timeProvider);
        Assert.True(deduplicator.ShouldAccept("expired.png"));
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // When
        Assert.True(deduplicator.ShouldAccept("current.png"));

        // Then
        Assert.Equal(1, deduplicator.TrackedPathCount);
    }

    internal sealed class ManualTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => initialTime + TimeSpan.FromTicks(_timestamp);

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }
}
