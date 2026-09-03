namespace EftSsNavi.Core.Monitoring;

public interface IScreenshotCreatedSourceFactory
{
    IScreenshotCreatedSource Create(string directoryPath);
}
