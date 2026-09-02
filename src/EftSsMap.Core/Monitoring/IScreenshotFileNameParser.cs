using EftSsMap.Core.Observations;

namespace EftSsMap.Core.Monitoring;

public interface IScreenshotFileNameParser
{
    bool TryParse(string fileName, out PositionObservation? observation);
}
