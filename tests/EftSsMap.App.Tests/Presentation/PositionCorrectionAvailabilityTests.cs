using EftSsMap.App.Presentation;
using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Tests.Presentation;

public sealed class PositionCorrectionAvailabilityTests
{
    [Fact]
    public void ShouldDisablePositionCorrectionForBundledProfile()
    {
        // Given: The selected profile exactly matches one supplied by the bundled catalog.
        var bundled = CreateProfile("Woods", @"C:\App\Assets\Maps\woods.png", "woods-hash");

        // When: Position-correction availability is evaluated.
        var available = PositionCorrectionAvailability.IsAvailable(bundled, [bundled]);

        // Then: Exact bundled maps do not expose manual correction.
        Assert.False(available);
    }

    [Fact]
    public void ShouldEnablePositionCorrectionForManualProfileWithBundledDisplayName()
    {
        // Given: A personal map collides with a bundled display name but uses another image.
        var bundled = CreateProfile("Customs", @"C:\App\Assets\Maps\customs.png", "bundled-hash");
        var manual = CreateProfile("CUSTOMS", @"D:\Personal\customs.png", "personal-hash");

        // When: Position-correction availability is evaluated.
        var available = PositionCorrectionAvailability.IsAvailable(manual, [bundled]);

        // Then: The manually added map retains correction access.
        Assert.True(available);
    }

    [Fact]
    public void ShouldDisablePositionCorrectionWhenNoProfileIsSelected()
    {
        // Given: No map profile is selected.
        MapProfile? selectedProfile = null;

        // When: Position-correction availability is evaluated.
        var available = PositionCorrectionAvailability.IsAvailable(selectedProfile, []);

        // Then: No correction entry point is needed.
        Assert.False(available);
    }

    private static MapProfile CreateProfile(string displayName, string imagePath, string imageHash) => new(
        displayName,
        imagePath,
        100,
        100,
        imageHash,
        [],
        default);
}
