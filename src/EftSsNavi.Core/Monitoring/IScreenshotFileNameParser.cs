using EftSsNavi.Core.Observations;

namespace EftSsNavi.Core.Monitoring;

public interface IScreenshotFileNameParser
{
    bool TryParse(string fileName, out PositionObservation? observation);
}
