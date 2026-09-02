using EftSsMap.Core.Observations;

namespace EftSsMap.Core.Monitoring;

public sealed class ScreenshotFileNameParserAdapter : IScreenshotFileNameParser
{
    public bool TryParse(string fileName, out PositionObservation? observation) =>
        ScreenshotFileNameParser.TryParse(fileName, out observation);
}
