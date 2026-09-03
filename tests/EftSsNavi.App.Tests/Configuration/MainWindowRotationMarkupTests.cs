using System.Xml.Linq;

namespace EftSsNavi.App.Tests.Configuration;

public sealed class MainWindowRotationMarkupTests
{
    [Theory]
    [InlineData("RotateMapLeftButton", "OnRotateMapLeftClick", "マップを左へ90度回転")]
    [InlineData("RotateMapRightButton", "OnRotateMapRightClick", "マップを右へ90度回転")]
    public void ShouldExposeArcArrowButtonForEachRotationDirection(
        string buttonName,
        string clickHandler,
        string tooltip)
    {
        // Given: The main toolbar markup.
        var document = LoadMarkup();
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        // When: A rotation button is located.
        var button = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .SingleOrDefault(element =>
                (string?)element.Attribute(xaml + "Name") == buttonName);

        // Then: It is reachable, described, and uses an arc-based path icon.
        Assert.NotNull(button);
        Assert.Equal(clickHandler, (string?)button.Attribute("Click"));
        Assert.Equal(tooltip, (string?)button.Attribute("ToolTipService.ToolTip"));
        var iconPath = Assert.Single(button.Descendants(), element => element.Name.LocalName == "Path");
        Assert.Contains("A", (string?)iconPath.Attribute("Data"), StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldPersistRotationAndApplyItToMapCanvas()
    {
        // Given: The main-window event-handler source.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "MainWindow.xaml.cs"));

        // Then: Profile activation, rotation actions, and persistence share the profile setting.
        Assert.Contains("MapControl.SetImageRotation(profile.ImageRotationQuarterTurns)", source);
        Assert.Contains("WithImageRotationQuarterTurns", source);
        Assert.Contains("TryUpdateSelectedProfileDisplaySettings", source);
        Assert.Contains("PersistSettings()", source);
    }

    [Fact]
    public void ShouldRotateImageMarkersAnchorsDirectionsAndPointerCoordinatesTogether()
    {
        // Given: The map-canvas drawing and interaction source.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "Controls",
            "MapCanvas.xaml.cs"));

        // Then: Every image-space consumer passes through the same display rotation.
        Assert.Contains("_imageRotation.GetDisplaySize", source);
        Assert.Contains("_imageRotation.ImageToDisplay", source);
        Assert.Contains("_imageRotation.DisplayToImage", source);
        Assert.Contains("_imageRotation.DirectionToDisplay", source);
    }

    private static XDocument LoadMarkup() => XDocument.Load(Path.Combine(
        FindRepositoryRoot(),
        "src",
        "EftSsNavi.App",
        "MainWindow.xaml"));

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
