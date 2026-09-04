namespace EftSsNavi.App.Tests.Configuration;

public sealed class MainWindowMapMarkerTests
{
    [Fact]
    public void ShouldConnectBundledMarkersFromCatalogToMapCanvas()
    {
        // Given: Application startup, profile activation, and map-canvas sources.
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "EftSsNavi.App", "MainWindow.xaml.cs"));
        var mapCanvas = File.ReadAllText(Path.Combine(root, "src", "EftSsNavi.App", "Controls", "MapCanvas.xaml.cs"));
        var markup = File.ReadAllText(Path.Combine(root, "src", "EftSsNavi.App", "MainWindow.xaml"));

        // Then: Catalog markers reach the canvas and are included in its paint path.
        Assert.Contains("bundledCatalog.MarkersByProfileName", mainWindow);
        Assert.Contains("SetBundledMapMarkers(profile)", mainWindow);
        Assert.Contains("SetMapMarkers(", mapCanvas);
        Assert.Contains("DrawMapMarkers(canvas)", mapCanvas);
        Assert.Contains("Text=\"PMC脱出\"", markup);
        Assert.Contains("Text=\"共同脱出\"", markup);
        Assert.DoesNotContain("Text=\"PMC/共用脱出\"", markup);
        Assert.Contains("Text=\"SCAV脱出\"", markup);
        Assert.Contains("Text=\"TRANSIT\"", markup);
        Assert.Contains("Text=\"PMCスポーン\"", markup);
        Assert.Contains("new SKColor(238, 50, 66, 235)", mapCanvas);
        Assert.Contains("new SKColor(255, 145, 35, 235)", mapCanvas);
    }

    [Fact]
    public void ShouldFitMapInformationAfterSelectionRotationAndFitButton()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "EftSsNavi.App", "MainWindow.xaml.cs"));
        var mapCanvas = File.ReadAllText(Path.Combine(root, "src", "EftSsNavi.App", "Controls", "MapCanvas.xaml.cs"));

        Assert.Contains(
            "SetBundledMapMarkers(profile);\n            MapControl.FitToView();",
            mainWindow.Replace("\r\n", "\n"));
        Assert.Contains(
            "private void OnFitMapClick(object sender, RoutedEventArgs e) => MapControl.FitToView();",
            mainWindow);
        Assert.Contains(
            "_imageRotation = rotation;\n        RefreshMapMarkerVisualBounds();\n        FitToView();",
            mapCanvas.Replace("\r\n", "\n"));
        Assert.Contains("MapContentViewportFitter.Fit(", mapCanvas);
    }

    [Fact]
    public void ShouldClearBundledMarkersWhenManuallyAddingMap()
    {
        // Given: Bundled markers may still be displayed for the previously selected map.
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "EftSsNavi.App", "MainWindow.xaml.cs"));

        // When: A manually selected image becomes a new map profile.
        var normalizedSource = mainWindow.Replace("\r\n", "\n");

        // Then: Markers that only belong to the bundled map are cleared before the new profile is shown.
        Assert.Contains(
            "MapControl.SetMarker(null, null);\n        SetBundledMapMarkers(null);\n        _profiles.Add(profile);",
            normalizedSource);
    }

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
