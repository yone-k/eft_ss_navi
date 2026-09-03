namespace EftSsNavi.Core.Monitoring;

public sealed class ScreenshotCreatedEventArgs : EventArgs
{
    public ScreenshotCreatedEventArgs(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        FullPath = fullPath;
    }

    public string FullPath { get; }
}
