using System.Numerics;
using EftSsNavi.Core.Monitoring;
using EftSsNavi.Core.Observations;

namespace EftSsNavi.Core.Tests.Monitoring;

public sealed class ScreenshotMonitorTests
{
    private const string ValidFileName = "2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41.png";

    [Fact]
    public void Should_start_source_automatically_when_directory_is_set()
    {
        // Given
        var fixture = new MonitorFixture();

        // When
        fixture.Monitor.SetDirectory("captures");

        // Then
        var source = Assert.Single(fixture.Factory.CreatedSources);
        Assert.Equal(1, source.StartCount);
    }

    [Fact]
    public void Should_unsubscribe_stop_and_dispose_old_source_before_starting_new_source_when_directory_changes()
    {
        // Given
        var fixture = new MonitorFixture();
        fixture.Monitor.SetDirectory("old-captures");
        var oldSource = fixture.Factory.CreatedSources[0];

        // When
        fixture.Monitor.SetDirectory("new-captures");

        // Then
        var newSource = fixture.Factory.CreatedSources[1];
        Assert.Equal(0, oldSource.SubscriberCount);
        Assert.Equal(0, oldSource.ErrorSubscriberCount);
        Assert.Equal(1, oldSource.StopCount);
        Assert.Equal(1, oldSource.DisposeCount);
        Assert.Equal(1, newSource.StartCount);
        Assert.Equal(1, newSource.ErrorSubscriberCount);
        Assert.Equal(new[] { "start:old-captures", "stop:old-captures", "dispose:old-captures", "start:new-captures" }, fixture.Factory.OperationLog);
    }

    [Fact]
    public void Should_ignore_queued_event_from_old_source_when_directory_generation_has_changed()
    {
        // Given
        var fixture = new MonitorFixture();
        var observations = new List<PositionObservation>();
        fixture.Monitor.ObservationCreated += observations.Add;
        fixture.Monitor.SetDirectory("old-captures");
        var dispatchQueuedBeforeSwitch = fixture.Factory.CreatedSources[0].CaptureCreatedDispatch(ValidFileName);
        fixture.Monitor.SetDirectory("new-captures");

        // When
        dispatchQueuedBeforeSwitch();

        // Then
        Assert.Empty(observations);
        Assert.Empty(fixture.Parser.ReceivedFileNames);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".PNG")]
    public void Should_process_created_notification_when_extension_is_png_ignoring_case(string extension)
    {
        // Given
        var fixture = new MonitorFixture();
        var observations = new List<PositionObservation>();
        fixture.Monitor.ObservationCreated += observations.Add;
        fixture.Monitor.SetDirectory("captures");
        var fullPath = Path.Combine("captures", Path.ChangeExtension(ValidFileName, extension));

        // When
        fixture.Factory.CreatedSources[0].RaiseCreated(fullPath);

        // Then
        Assert.Single(observations);
    }

    [Fact]
    public void Should_reject_created_notification_when_file_has_png_tmp_suffix()
    {
        // Given
        var fixture = new MonitorFixture();
        var observations = new List<PositionObservation>();
        fixture.Monitor.ObservationCreated += observations.Add;
        fixture.Monitor.SetDirectory("captures");

        // When
        fixture.Factory.CreatedSources[0].RaiseCreated(Path.Combine("captures", $"{ValidFileName}.tmp"));

        // Then
        Assert.Empty(observations);
        Assert.Empty(fixture.Parser.ReceivedFileNames);
    }

    [Fact]
    public void Should_pass_only_file_name_to_parser_when_created_notification_is_processed()
    {
        // Given
        var fixture = new MonitorFixture();
        fixture.Monitor.SetDirectory("captures");
        var fullPath = Path.GetFullPath(Path.Combine("captures", ValidFileName));

        // When
        fixture.Factory.CreatedSources[0].RaiseCreated(fullPath);

        // Then
        Assert.Equal(new[] { ValidFileName }, fixture.Parser.ReceivedFileNames);
    }

    [Fact]
    public void Should_publish_processed_file_name_with_valid_observation()
    {
        // Given
        var fixture = new MonitorFixture();
        string? processedFileName = null;
        fixture.Monitor.ObservationAccepted += (_, fileName) => processedFileName = fileName;
        fixture.Monitor.SetDirectory("captures");

        // When
        fixture.Factory.CreatedSources[0].RaiseCreated(Path.Combine("captures", ValidFileName));

        // Then
        Assert.Equal(ValidFileName, processedFileName);
    }

    [Fact]
    public void Should_publish_rejected_file_name_when_png_name_cannot_be_parsed()
    {
        // Given
        var fixture = new MonitorFixture();
        fixture.Parser.ShouldSucceed = false;
        string? rejectedFileName = null;
        fixture.Monitor.FileNameRejected += fileName => rejectedFileName = fileName;
        fixture.Monitor.SetDirectory("captures");

        // When
        fixture.Factory.CreatedSources[0].RaiseCreated(Path.Combine("captures", "invalid.png"));

        // Then
        Assert.Equal("invalid.png", rejectedFileName);
    }

    [Fact]
    public async Task Should_serialize_directory_switch_with_observation_publication()
    {
        // Given
        var fixture = new MonitorFixture();
        using var publicationEntered = new ManualResetEventSlim();
        using var releasePublication = new ManualResetEventSlim();
        fixture.Monitor.ObservationAccepted += (_, _) =>
        {
            publicationEntered.Set();
            releasePublication.Wait();
        };
        fixture.Monitor.SetDirectory("old-captures");

        // When
        var publication = Task.Run(
            () => fixture.Factory.CreatedSources[0].RaiseCreated(Path.Combine("old-captures", ValidFileName)));
        await Task.Run(() => publicationEntered.Wait(TimeSpan.FromSeconds(5)));
        var directorySwitch = Task.Run(() => fixture.Monitor.SetDirectory("new-captures"));

        // Then
        try
        {
            var firstCompleted = await Task.WhenAny(directorySwitch, Task.Delay(TimeSpan.FromMilliseconds(200)));
            Assert.NotSame(directorySwitch, firstCompleted);
        }
        finally
        {
            releasePublication.Set();
        }

        await Task.WhenAll(publication, directorySwitch).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Should_not_publish_secondary_observation_event_after_handler_switches_directory()
    {
        // Given
        var fixture = new MonitorFixture();
        var observations = new List<PositionObservation>();
        fixture.Monitor.ObservationAccepted += (_, _) => fixture.Monitor.SetDirectory("new-captures");
        fixture.Monitor.ObservationCreated += observations.Add;
        fixture.Monitor.SetDirectory("old-captures");

        // When
        fixture.Factory.CreatedSources[0].RaiseCreated(Path.Combine("old-captures", ValidFileName));

        // Then
        Assert.Empty(observations);
    }

    [Fact]
    public void Should_publish_error_from_current_source()
    {
        // Given
        var fixture = new MonitorFixture();
        Exception? received = null;
        fixture.Monitor.MonitoringFailed += exception => received = exception;
        fixture.Monitor.SetDirectory("captures");
        var expected = new IOException("Watcher buffer overflowed.");

        // When
        fixture.Factory.CreatedSources[0].RaiseError(expected);

        // Then
        Assert.Same(expected, received);
    }

    [Fact]
    public void Should_ignore_queued_error_from_old_source_after_directory_switch()
    {
        // Given
        var fixture = new MonitorFixture();
        var errors = new List<Exception>();
        fixture.Monitor.MonitoringFailed += errors.Add;
        fixture.Monitor.SetDirectory("old-captures");
        var dispatchQueuedBeforeSwitch = fixture.Factory.CreatedSources[0]
            .CaptureErrorDispatch(new IOException("old watcher"));
        fixture.Monitor.SetDirectory("new-captures");

        // When
        dispatchQueuedBeforeSwitch();

        // Then
        Assert.Empty(errors);
    }

    [Fact]
    public void Should_expose_created_and_error_notifications_without_preexisting_file_enumeration_contract()
    {
        // Given
        var contract = typeof(IScreenshotCreatedSource);

        // When
        var declaredEvents = contract.GetEvents().Select(item => item.Name);
        var declaredMethods = contract.GetMethods().Where(item => !item.IsSpecialName).Select(item => item.Name);

        // Then
        Assert.Equal(new[] { "Created", "Error" }, declaredEvents.OrderBy(item => item));
        Assert.Equal(new[] { "Start", "Stop" }, declaredMethods.OrderBy(item => item));
    }

    private sealed class MonitorFixture
    {
        public MonitorFixture()
        {
            var timeProvider = new ScreenshotNotificationDeduplicatorTests.ManualTimeProvider(
                new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
            Monitor = new ScreenshotMonitor(
                Factory,
                Parser,
                new ScreenshotNotificationDeduplicator(timeProvider));
        }

        public FakeScreenshotCreatedSourceFactory Factory { get; } = new();

        public RecordingParser Parser { get; } = new();

        public ScreenshotMonitor Monitor { get; }
    }

    private sealed class FakeScreenshotCreatedSourceFactory : IScreenshotCreatedSourceFactory
    {
        public List<FakeScreenshotCreatedSource> CreatedSources { get; } = [];

        public List<string> OperationLog { get; } = [];

        public IScreenshotCreatedSource Create(string directoryPath)
        {
            var source = new FakeScreenshotCreatedSource(directoryPath, OperationLog);
            CreatedSources.Add(source);
            return source;
        }
    }

    private sealed class FakeScreenshotCreatedSource(string directoryPath, List<string> operationLog) : IScreenshotCreatedSource
    {
        private EventHandler<ScreenshotCreatedEventArgs>? _created;
        private EventHandler<ScreenshotSourceErrorEventArgs>? _error;

        public event EventHandler<ScreenshotCreatedEventArgs>? Created
        {
            add => _created += value;
            remove => _created -= value;
        }

        public event EventHandler<ScreenshotSourceErrorEventArgs>? Error
        {
            add => _error += value;
            remove => _error -= value;
        }

        public int SubscriberCount => _created?.GetInvocationList().Length ?? 0;

        public int ErrorSubscriberCount => _error?.GetInvocationList().Length ?? 0;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public void Start()
        {
            StartCount++;
            operationLog.Add($"start:{directoryPath}");
        }

        public void Stop()
        {
            StopCount++;
            operationLog.Add($"stop:{directoryPath}");
        }

        public void Dispose()
        {
            DisposeCount++;
            operationLog.Add($"dispose:{directoryPath}");
        }

        public void RaiseCreated(string fullPath) => _created?.Invoke(this, new ScreenshotCreatedEventArgs(fullPath));

        public Action CaptureCreatedDispatch(string fullPath)
        {
            var queuedHandlers = _created;
            return () => queuedHandlers?.Invoke(this, new ScreenshotCreatedEventArgs(fullPath));
        }

        public void RaiseError(Exception exception) =>
            _error?.Invoke(this, new ScreenshotSourceErrorEventArgs(exception));

        public Action CaptureErrorDispatch(Exception exception)
        {
            var queuedHandlers = _error;
            return () => queuedHandlers?.Invoke(this, new ScreenshotSourceErrorEventArgs(exception));
        }
    }

    private sealed class RecordingParser : IScreenshotFileNameParser
    {
        public List<string> ReceivedFileNames { get; } = [];

        public bool ShouldSucceed { get; set; } = true;

        public bool TryParse(string fileName, out PositionObservation? observation)
        {
            ReceivedFileNames.Add(fileName);
            if (!ShouldSucceed)
            {
                observation = null;
                return false;
            }

            observation = new PositionObservation(
                new DateTime(2026, 9, 1, 20, 20, 0),
                new Vector3(1, 2, 3),
                Quaternion.Identity,
                "10.41",
                null,
                Vector2.UnitY);
            return true;
        }
    }
}
