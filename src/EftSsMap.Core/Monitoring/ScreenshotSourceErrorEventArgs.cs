namespace EftSsMap.Core.Monitoring;

public sealed class ScreenshotSourceErrorEventArgs : EventArgs
{
    public ScreenshotSourceErrorEventArgs(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }

    public Exception Exception { get; }
}
