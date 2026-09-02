using EftSsMap.Core.Observations;

namespace EftSsMap.Core.Monitoring;

public sealed class ScreenshotMonitor : IDisposable
{
    private readonly object _gate = new();
    private readonly object _publicationGate = new();
    private readonly IScreenshotCreatedSourceFactory _sourceFactory;
    private readonly IScreenshotFileNameParser _parser;
    private readonly ScreenshotNotificationDeduplicator _deduplicator;

    private IScreenshotCreatedSource? _source;
    private EventHandler<ScreenshotCreatedEventArgs>? _sourceHandler;
    private EventHandler<ScreenshotSourceErrorEventArgs>? _sourceErrorHandler;
    private long _generation;
    private bool _disposed;

    public ScreenshotMonitor(
        IScreenshotCreatedSourceFactory sourceFactory,
        IScreenshotFileNameParser parser,
        ScreenshotNotificationDeduplicator deduplicator)
    {
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(deduplicator);

        _sourceFactory = sourceFactory;
        _parser = parser;
        _deduplicator = deduplicator;
    }

    public event Action<PositionObservation>? ObservationCreated;

    public event Action<PositionObservation, string>? ObservationAccepted;

    public event Action<string>? FileNameRejected;

    public event Action<Exception>? MonitoringFailed;

    public void SetDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var generation = AdvanceGeneration();
            ReleaseCurrentSource();

            var source = _sourceFactory.Create(directoryPath);
            EventHandler<ScreenshotCreatedEventArgs> handler = (_, eventArgs) =>
                HandleCreated(generation, eventArgs.FullPath);
            EventHandler<ScreenshotSourceErrorEventArgs> errorHandler = (_, eventArgs) =>
                HandleError(generation, eventArgs.Exception);

            source.Created += handler;
            source.Error += errorHandler;
            try
            {
                source.Start();
                _source = source;
                _sourceHandler = handler;
                _sourceErrorHandler = errorHandler;
            }
            catch
            {
                source.Created -= handler;
                source.Error -= errorHandler;
                source.Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AdvanceGeneration();
            ReleaseCurrentSource();
        }
    }

    private void HandleCreated(long generation, string fullPath)
    {
        if (generation != Volatile.Read(ref _generation)
            || !HasPngExtension(fullPath)
            || !_deduplicator.ShouldAccept(fullPath))
        {
            return;
        }

        string fileName;
        try
        {
            fileName = Path.GetFileName(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            return;
        }

        if (generation != Volatile.Read(ref _generation))
        {
            return;
        }

        if (!_parser.TryParse(fileName, out var observation) || observation is null)
        {
            lock (_publicationGate)
            {
                if (generation == _generation)
                {
                    FileNameRejected?.Invoke(fileName);
                }
            }

            return;
        }

        lock (_publicationGate)
        {
            if (generation != _generation)
            {
                return;
            }

            ObservationAccepted?.Invoke(observation, fileName);
            if (generation == _generation)
            {
                ObservationCreated?.Invoke(observation);
            }
        }
    }

    private static bool HasPngExtension(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            return false;
        }
    }

    private void HandleError(long generation, Exception exception)
    {
        lock (_publicationGate)
        {
            if (generation == _generation)
            {
                MonitoringFailed?.Invoke(exception);
            }
        }
    }

    private long AdvanceGeneration()
    {
        lock (_publicationGate)
        {
            return ++_generation;
        }
    }

    private void ReleaseCurrentSource()
    {
        if (_source is null)
        {
            return;
        }

        var source = _source;
        var handler = _sourceHandler;
        var errorHandler = _sourceErrorHandler;
        _source = null;
        _sourceHandler = null;
        _sourceErrorHandler = null;

        if (handler is not null)
        {
            source.Created -= handler;
        }

        if (errorHandler is not null)
        {
            source.Error -= errorHandler;
        }

        try
        {
            source.Stop();
        }
        finally
        {
            source.Dispose();
        }
    }
}
