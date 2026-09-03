using EftSsNavi.Core.Monitoring;

namespace EftSsNavi.App.Monitoring;

public sealed class FileSystemWatcherCreatedSource : IScreenshotCreatedSource
{
    private readonly object _gate = new();
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    public FileSystemWatcherCreatedSource(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        _watcher = new FileSystemWatcher(directoryPath)
        {
            Filter = "*.png",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
        };
        _watcher.Created += OnCreated;
        _watcher.Error += OnError;
    }

    public event EventHandler<ScreenshotCreatedEventArgs>? Created;

    public event EventHandler<ScreenshotSourceErrorEventArgs>? Error;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _watcher.EnableRaisingEvents)
            {
                return;
            }

            _watcher.EnableRaisingEvents = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed || !_watcher.EnableRaisingEvents)
            {
                return;
            }

            _watcher.EnableRaisingEvents = false;
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
            Created = null;
            Error = null;
        }

        _watcher.Created -= OnCreated;
        _watcher.Error -= OnError;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }

    private void OnCreated(object sender, FileSystemEventArgs eventArgs)
    {
        EventHandler<ScreenshotCreatedEventArgs>? handler;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            handler = Created;
        }

        handler?.Invoke(this, new ScreenshotCreatedEventArgs(eventArgs.FullPath));
    }

    private void OnError(object sender, ErrorEventArgs eventArgs)
    {
        EventHandler<ScreenshotSourceErrorEventArgs>? handler;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            handler = Error;
        }

        handler?.Invoke(this, new ScreenshotSourceErrorEventArgs(eventArgs.GetException()));
    }
}
