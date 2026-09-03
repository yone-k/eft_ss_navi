namespace EftSsNavi.Core.Monitoring;

public interface IScreenshotCreatedSource : IDisposable
{
    event EventHandler<ScreenshotCreatedEventArgs>? Created;

    event EventHandler<ScreenshotSourceErrorEventArgs>? Error;

    void Start();

    void Stop();
}
