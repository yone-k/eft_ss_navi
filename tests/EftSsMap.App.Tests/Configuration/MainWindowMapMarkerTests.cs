namespace EftSsMap.App.Tests.Configuration;

public sealed class MainWindowMapMarkerTests
{
    [Fact]
    public void ShouldConnectBundledMarkersFromCatalogToMapCanvas()
    {
        // Given: Application startup, profile activation, and map-canvas sources.
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "EftSsMap.App", "MainWindow.xaml.cs"));
        var mapCanvas = File.ReadAllText(Path.Combine(root, "src", "EftSsMap.App", "Controls", "MapCanvas.xaml.cs"));
        var markup = File.ReadAllText(Path.Combine(root, "src", "EftSsMap.App", "MainWindow.xaml"));

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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsMap.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
