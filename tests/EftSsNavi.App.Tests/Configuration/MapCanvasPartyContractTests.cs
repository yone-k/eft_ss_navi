namespace EftSsNavi.App.Tests.Configuration;

public sealed class MapCanvasPartyContractTests
{
    [Fact]
    public void ShouldExposePartyMarkerSnapshotInput()
    {
        // Given: The map canvas source used by MainWindow.
        var source = LoadMapCanvasSource();

        // When: Its public party-marker integration surface is inspected.
        var hasSnapshotInput = source.Contains("void SetPartyMarkers(", StringComparison.Ordinal);

        // Then: MainWindow can replace all remote markers atomically.
        Assert.True(hasSnapshotInput, "MapCanvas must expose SetPartyMarkers for immutable draw snapshots.");
    }

    [Fact]
    public void ShouldDrawPartyMarkersBetweenBundledMarkersAndCalibrationAnchors()
    {
        // Given: The map canvas paint path.
        var source = LoadMapCanvasSource();

        // When: The relevant draw calls are located.
        var bundledIndex = source.IndexOf("DrawMapMarkers(canvas)", StringComparison.Ordinal);
        var partyIndex = source.IndexOf("DrawPartyMarkers(canvas)", StringComparison.Ordinal);
        var calibrationIndex = source.IndexOf("DrawCalibrationAnchors(canvas)", StringComparison.Ordinal);

        // Then: Party markers paint after bundled markers and before calibration anchors.
        Assert.True(bundledIndex >= 0 && bundledIndex < partyIndex && partyIndex < calibrationIndex);
    }

    [Fact]
    public void ShouldDrawSelfMarkerAfterPartyMarkers()
    {
        // Given: The map canvas paint path.
        var source = LoadMapCanvasSource();

        // When: Party and self draw calls are located.
        var partyIndex = source.IndexOf("DrawPartyMarkers(canvas)", StringComparison.Ordinal);
        var selfIndex = source.IndexOf("DrawMarker(canvas)", StringComparison.Ordinal);

        // Then: The local red marker remains visually above remote markers.
        Assert.True(partyIndex >= 0 && partyIndex < selfIndex);
    }

    [Fact]
    public void ShouldReuseNavigationGeometryForDirectionalPartyMarkers()
    {
        // Given: The party marker draw implementation.
        var source = LoadMapCanvasSource();

        // When: Its geometry construction is inspected.
        var drawMethod = ExtractMethod(source, "private void DrawPartyMarkers");

        // Then: Remote directional arrows have the same geometry as the local cursor.
        Assert.Contains("NavigationCursorGeometry.Create", drawMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDrawDirectionlessPartyMarkersAsCircles()
    {
        // Given: The party marker draw implementation.
        var source = LoadMapCanvasSource();

        // When: Its directionless branch is inspected.
        var drawMethod = ExtractMethod(source, "private void DrawPartyMarkers");

        // Then: It has a circle rendering path as the directionless fallback.
        Assert.Contains("PartyMarkerShape.Circle", drawMethod, StringComparison.Ordinal);
        Assert.Contains("DrawCircle", drawMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDrawPartyDisplayNameWithOutlinedFillLabel()
    {
        // Given: The party marker draw implementation.
        var source = LoadMapCanvasSource();

        // When: Its display-name drawing is inspected.
        var drawMethod = ExtractMethod(source, "private void DrawPartyMarkers");

        // Then: The display name uses both outline and fill paints for readability.
        Assert.Contains("marker.DisplayName", drawMethod, StringComparison.Ordinal);
        Assert.Contains("labelOutline", drawMethod, StringComparison.Ordinal);
        Assert.Contains("labelFill", drawMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldApplyVisualOpacityToWholePartyMarkerAndLabel()
    {
        // Given: The party marker draw implementation.
        var source = LoadMapCanvasSource();

        // When: Its stale-opacity handling is inspected.
        var drawMethod = ExtractMethod(source, "private void DrawPartyMarkers");

        // Then: The visual opacity participates in the shared marker-and-label draw scope.
        Assert.Contains("marker.Opacity", drawMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldExcludePartyMarkersFromPointerHitTestingAndDragging()
    {
        // Given: The pointer interaction section of MapCanvas.
        var source = LoadMapCanvasSource();

        // When: The pointer-press implementation is isolated.
        var pointerSource = ExtractMethod(source, "private void OnPointerPressed");

        // Then: Only calibration anchors and the local marker participate in interaction.
        Assert.DoesNotContain("Party", pointerSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_markerDragInteraction.TryBegin", pointerSource, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected method '{signature}' was not found.");
        var end = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not determine the end of method '{signature}'.");
        return source[start..end];
    }

    private static string LoadMapCanvasSource() => File.ReadAllText(Path.Combine(
        FindRepositoryRoot(),
        "src",
        "EftSsNavi.App",
        "Controls",
        "MapCanvas.xaml.cs"));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsNavi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
