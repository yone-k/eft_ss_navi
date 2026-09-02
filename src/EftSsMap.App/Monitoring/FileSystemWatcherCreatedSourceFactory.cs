using EftSsMap.Core.Monitoring;

namespace EftSsMap.App.Monitoring;

public sealed class FileSystemWatcherCreatedSourceFactory : IScreenshotCreatedSourceFactory
{
    public IScreenshotCreatedSource Create(string directoryPath) =>
        new FileSystemWatcherCreatedSource(directoryPath);
}
