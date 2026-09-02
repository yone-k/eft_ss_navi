using System.Xml.Linq;

namespace EftSsMap.App.Tests.Configuration;

public sealed class MainWindowProgressiveCalibrationTests
{
    [Fact]
    public void ShouldGuideDetectedScreenshotPlacementWithoutManualScreenshotPicker()
    {
        // Given: The main-window markup.
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        // When: Calibration-related controls are inspected.
        var progressivePanel = document.Descendants()
            .SingleOrDefault(element =>
                (string?)element.Attribute(xaml + "Name") == "ProgressiveCalibrationPanel");
        var manualPicker = document.Descendants()
            .SingleOrDefault(element =>
                (string?)element.Attribute(xaml + "Name") == "ChooseCalibrationScreenshotButton");

        // Then: Detection-driven guidance replaces the dedicated screenshot picker.
        Assert.NotNull(progressivePanel);
        Assert.Null(manualPicker);
        Assert.Contains(
            document.Descendants(),
            element => (string?)element.Attribute("Text") ==
                "スクリーンショットを検知したら、その位置をマップ上でクリックしてください。");
    }

    [Fact]
    public void ShouldConnectMapAdditionAndScreenshotDetectionToProgressiveCalibration()
    {
        // Given: The main-window event-handler source.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml.cs"));

        // Then: New profiles start empty, detected positions are staged, and map clicks place them.
        Assert.Contains("MapProfile.CreateUncalibrated", source);
        Assert.Contains("session.TryStage(new WorldPoint", source);
        Assert.Contains("session.Place(e.ImagePixel)", source);
        Assert.DoesNotContain("PickCalibrationScreenshotAsync", source);
        Assert.DoesNotContain("OnChooseCalibrationScreenshotClick", source);
        Assert.DoesNotContain("CalibrationDraft", source);
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
