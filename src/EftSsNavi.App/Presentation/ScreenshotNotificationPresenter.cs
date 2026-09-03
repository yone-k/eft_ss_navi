using EftSsNavi.Core.Observations;
using EftSsNavi.Core.Presentation;

namespace EftSsNavi.App.Presentation;

public sealed class ScreenshotNotificationPresenter
{
    private readonly MainStateCoordinator stateCoordinator;

    public ScreenshotNotificationPresenter(MainStateCoordinator stateCoordinator)
    {
        ArgumentNullException.ThrowIfNull(stateCoordinator);
        this.stateCoordinator = stateCoordinator;
    }

    public void Accept(PositionObservation observation, string fileName, long observationEpoch) =>
        stateCoordinator.ProcessObservation(observation, fileName, observationEpoch);

    public string RejectFileName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return $"スクリーンショットのファイル名を解析できません: {fileName}";
    }

    public string MonitoringFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return $"スクリーンショット監視中にエラーが発生しました。{exception.Message}";
    }
}
