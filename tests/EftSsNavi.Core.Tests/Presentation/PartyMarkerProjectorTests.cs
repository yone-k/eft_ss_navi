using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Presentation;

namespace EftSsNavi.Core.Tests.Presentation;

public sealed class PartyMarkerProjectorTests
{
    private const double Precision = 1e-10;

    [Fact]
    public void ShouldTransformPositionWhenPositionIsOnSelectedMap()
    {
        // Given: A valid selected map and a position reported for that map.
        var profile = CreateProfile("Woods", new AffineTransform2D(2, 0, 0, 3, 10, 20));

        // When: The party position is projected.
        var projection = PartyMarkerProjector.Project(
            selectedProfile: profile,
            calibrationValid: true,
            positionMapName: "Woods",
            worldPosition: new WorldPoint(4, 6),
            worldDirection: null);

        // Then: The selected profile's affine transform produces image pixels.
        Assert.NotNull(projection);
        AssertPoint(new PixelPoint(18, 38), projection.Value.Position);
    }

    [Fact]
    public void ShouldTransformDirectionWithoutTranslationWhenDirectionIsAvailable()
    {
        // Given: A transform with a linear component and a large translation.
        var profile = CreateProfile("Woods", new AffineTransform2D(0, -2, 3, 0, 1000, -2000));

        // When: A party position with direction is projected.
        var projection = PartyMarkerProjector.Project(
            selectedProfile: profile,
            calibrationValid: true,
            positionMapName: "Woods",
            worldPosition: new WorldPoint(4, 6),
            worldDirection: new WorldPoint(2, 5));

        // Then: Direction uses only the affine transform's linear component.
        Assert.NotNull(projection);
        AssertPoint(new PixelPoint(-10, 6), projection.Value.Direction);
    }

    [Fact]
    public void ShouldMatchMapNameIgnoringCase()
    {
        // Given: A selected profile whose name differs only by case from the position map.
        var profile = CreateProfile("Woods");

        // When: The party position is projected.
        var projection = PartyMarkerProjector.Project(
            selectedProfile: profile,
            calibrationValid: true,
            positionMapName: "WOODS",
            worldPosition: new WorldPoint(4, 6),
            worldDirection: null);

        // Then: The map names are treated as matching.
        Assert.NotNull(projection);
    }

    [Fact]
    public void ShouldPreserveNullDirectionWhenDirectionIsUnavailable()
    {
        // Given: A party position without a horizontal direction.
        var profile = CreateProfile("Woods");

        // When: The position is projected.
        var projection = PartyMarkerProjector.Project(
            selectedProfile: profile,
            calibrationValid: true,
            positionMapName: "Woods",
            worldPosition: new WorldPoint(4, 6),
            worldDirection: null);

        // Then: The drawable projection has no direction.
        Assert.NotNull(projection);
        Assert.Null(projection.Value.Direction);
    }

    [Fact]
    public void ShouldNotProjectPositionFromDifferentMap()
    {
        // Given: A valid selected profile for a different map.
        var profile = CreateProfile("Woods");

        // When: A position from Customs is projected.
        var projection = PartyMarkerProjector.Project(
            selectedProfile: profile,
            calibrationValid: true,
            positionMapName: "Customs",
            worldPosition: new WorldPoint(4, 6),
            worldDirection: null);

        // Then: No drawable projection is returned.
        Assert.Null(projection);
    }

    [Fact]
    public void ShouldNotProjectPositionWhenNoMapIsSelected()
    {
        // Given: No selected profile.

        // When: A party position is projected.
        var projection = PartyMarkerProjector.Project(
            selectedProfile: null,
            calibrationValid: true,
            positionMapName: "Woods",
            worldPosition: new WorldPoint(4, 6),
            worldDirection: null);

        // Then: No drawable projection is returned.
        Assert.Null(projection);
    }

    [Fact]
    public void ShouldNotProjectPositionWhenCalibrationIsInvalid()
    {
        // Given: A selected profile whose calibration is invalid.
        var profile = CreateProfile("Woods");

        // When: A party position is projected.
        var projection = PartyMarkerProjector.Project(
            selectedProfile: profile,
            calibrationValid: false,
            positionMapName: "Woods",
            worldPosition: new WorldPoint(4, 6),
            worldDirection: null);

        // Then: No drawable projection is returned.
        Assert.Null(projection);
    }

    private static MapProfile CreateProfile(
        string displayName,
        AffineTransform2D? transform = null) =>
        new(
            displayName,
            @"C:\Maps\map.png",
            7000,
            6000,
            "0123456789abcdef",
            [
                new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(0, 0)),
                new CalibrationPoint(new WorldPoint(1, 0), new PixelPoint(1, 0)),
                new CalibrationPoint(new WorldPoint(0, 1), new PixelPoint(0, 1)),
            ],
            transform ?? new AffineTransform2D(1, 0, 0, 1, 0, 0));

    private static void AssertPoint(PixelPoint expected, PixelPoint? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.X, actual.Value.X, Precision);
        Assert.Equal(expected.Y, actual.Value.Y, Precision);
    }
}
