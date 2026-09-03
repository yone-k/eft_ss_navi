using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace EftSsNavi.Core.Observations;

public static partial class ScreenshotFileNameParser
{
    private const NumberStyles NumberStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static bool TryParse(string? fileName, out PositionObservation? observation)
    {
        observation = null;

        if (fileName is null)
        {
            return false;
        }

        var match = FileNamePattern().Match(fileName);
        if (!match.Success
            || !TryParseCapturedAt(match, out var capturedAt)
            || !TryParseFinite(match.Groups["x"].Value, out var x)
            || !TryParseFinite(match.Groups["y"].Value, out var y)
            || !TryParseFinite(match.Groups["z"].Value, out var z)
            || !TryParseFinite(match.Groups["qx"].Value, out var qx)
            || !TryParseFinite(match.Groups["qy"].Value, out var qy)
            || !TryParseFinite(match.Groups["qz"].Value, out var qz)
            || !TryParseFinite(match.Groups["qw"].Value, out var qw)
            || !TryParseSequenceNumber(match, out var sequenceNumber))
        {
            return false;
        }

        var rotation = new Quaternion(qx, qy, qz, qw);
        if (!QuaternionDirectionCalculator.TryNormalize(rotation, out _))
        {
            return false;
        }

        Vector2? horizontalForward = null;
        if (QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out var direction))
        {
            horizontalForward = direction;
        }

        observation = new PositionObservation(
            capturedAt,
            new Vector3(x, y, z),
            rotation,
            match.Groups["gameTime"].Value,
            sequenceNumber,
            horizontalForward);
        return true;
    }

    private static bool TryParseCapturedAt(Match match, out DateTime capturedAt)
    {
        var value = $"{match.Groups["date"].Value}[{match.Groups["time"].Value}]";
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd[HH-mm]",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out capturedAt);
    }

    private static bool TryParseFinite(string value, out float number)
    {
        return float.TryParse(value, NumberStyle, CultureInfo.InvariantCulture, out number)
            && float.IsFinite(number);
    }

    private static bool TryParseSequenceNumber(Match match, out int? sequenceNumber)
    {
        sequenceNumber = null;
        var group = match.Groups["sequence"];
        if (!group.Success)
        {
            return true;
        }

        if (!int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        sequenceNumber = parsed;
        return true;
    }

    [GeneratedRegex(
        @"\A(?<date>\d{4}-\d{2}-\d{2})\[(?<time>\d{2}-\d{2})\]_(?<x>[+-]?(?:\d+(?:\.\d+)?|\.\d+)), (?<y>[+-]?(?:\d+(?:\.\d+)?|\.\d+)), (?<z>[+-]?(?:\d+(?:\.\d+)?|\.\d+))_(?<qx>[+-]?(?:\d+(?:\.\d+)?|\.\d+)), (?<qy>[+-]?(?:\d+(?:\.\d+)?|\.\d+)), (?<qz>[+-]?(?:\d+(?:\.\d+)?|\.\d+)), (?<qw>[+-]?(?:\d+(?:\.\d+)?|\.\d+))_(?<gameTime>[+-]?(?:\d+(?:\.\d+)?|\.\d+))(?: \((?<sequence>\d+)\))?\.png\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}
