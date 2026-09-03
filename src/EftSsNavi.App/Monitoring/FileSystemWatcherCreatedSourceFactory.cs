using EftSsNavi.Core.Monitoring;

namespace EftSsNavi.App.Monitoring;

public sealed class FileSystemWatcherCreatedSourceFactory : IScreenshotCreatedSourceFactory
{
    public IScreenshotCreatedSource Create(string directoryPath) =>
        new FileSystemWatcherCreatedSource(directoryPath);
}
