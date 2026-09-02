using EftSsMap.App.Controls;
using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Tests.Controls;

public sealed class MarkerDragInteractionTests
{
    [Fact]
    public void ShouldCompleteMarkerDragWhenEnabledAndPointerStartsOnMarker()
    {
        // Given: Marker correction is enabled and the pointer is within its hit target.
        var interaction = new MarkerDragInteraction { IsEnabled = true };
        var began = interaction.TryBegin(
            new PixelPoint(100, 100),
            new PixelPoint(106, 108));

        // When: The marker is dropped at an image coordinate.
        var completed = interaction.TryComplete(new PixelPoint(500, 600), out var correctedPixel);

        // Then: The drop becomes a correction request and the drag ends.
        Assert.True(began);
        Assert.True(completed);
        Assert.Equal(new PixelPoint(500, 600), correctedPixel);
        Assert.False(interaction.IsDragging);
    }

    [Fact]
    public void ShouldLeaveMapAvailableForPanningWhenPointerStartsAwayFromMarker()
    {
        // Given: Marker correction is enabled but the pointer is outside its hit target.
        var interaction = new MarkerDragInteraction { IsEnabled = true };

        // When: A marker drag is attempted.
        var began = interaction.TryBegin(
            new PixelPoint(100, 100),
            new PixelPoint(150, 150));

        // Then: No marker drag is captured, so the canvas can pan normally.
        Assert.False(began);
        Assert.False(interaction.IsDragging);
    }

    [Fact]
    public void ShouldRetainCompletedDropWhenPointerCaptureLossCancelsInteraction()
    {
        // Given: A marker drag whose pointer-capture release synchronously cancels interaction.
        var interaction = new MarkerDragInteraction { IsEnabled = true };
        Assert.True(interaction.TryBegin(
            new PixelPoint(100, 100),
            new PixelPoint(100, 100)));
        var captureReleased = false;

        // When: The drop is completed and pointer capture is released.
        var completed = interaction.TryRelease(
            new PixelPoint(500, 600),
            () =>
            {
                captureReleased = true;
                interaction.Cancel();
            },
            out var correctedPixel);

        // Then: Capture loss cannot discard the already-completed correction.
        Assert.True(captureReleased);
        Assert.True(completed);
        Assert.Equal(new PixelPoint(500, 600), correctedPixel);
    }
}
