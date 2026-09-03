namespace EftSsNavi.Core.Monitoring;

public sealed class ScreenshotNotificationDeduplicator
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, long> _acceptedAtByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<AcceptedPath> _acceptedPathsByTime = new();

    public ScreenshotNotificationDeduplicator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public bool ShouldAccept(string path)
    {
        if (!TryNormalizePath(path, out var normalizedPath))
        {
            return false;
        }

        lock (_gate)
        {
            var timestamp = _timeProvider.GetTimestamp();
            RemoveExpiredPaths(timestamp);
            if (_acceptedAtByPath.TryGetValue(normalizedPath, out var acceptedAt)
                && _timeProvider.GetElapsedTime(acceptedAt, timestamp) < DuplicateWindow)
            {
                return false;
            }

            _acceptedAtByPath[normalizedPath] = timestamp;
            _acceptedPathsByTime.Enqueue(new AcceptedPath(normalizedPath, timestamp));
            return true;
        }
    }

    internal int TrackedPathCount
    {
        get
        {
            lock (_gate)
            {
                return _acceptedAtByPath.Count;
            }
        }
    }

    private void RemoveExpiredPaths(long timestamp)
    {
        while (_acceptedPathsByTime.TryPeek(out var candidate)
               && _timeProvider.GetElapsedTime(candidate.Timestamp, timestamp) >= DuplicateWindow)
        {
            _acceptedPathsByTime.Dequeue();
            if (_acceptedAtByPath.TryGetValue(candidate.Path, out var acceptedAt)
                && acceptedAt == candidate.Timestamp)
            {
                _acceptedAtByPath.Remove(candidate.Path);
            }
        }
    }

    private static bool TryNormalizePath(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException
            or System.Security.SecurityException)
        {
            return false;
        }
    }

    private readonly record struct AcceptedPath(string Path, long Timestamp);
}
