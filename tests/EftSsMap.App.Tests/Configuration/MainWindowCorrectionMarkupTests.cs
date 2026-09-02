using System.Xml.Linq;

namespace EftSsMap.App.Tests.Configuration;

public sealed class MainWindowCorrectionMarkupTests
{
    [Fact]
    public void ShouldExposePositionCorrectionFromMainToolbar()
    {
        // Given: The main-window markup.
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        // When: The position-correction entry point is located.
        var button = document.Descendants()
            .SingleOrDefault(element =>
                (string?)element.Attribute(xaml + "Name") == "CorrectionModeButton");

        // Then: The toolbar exposes the actual click handler used to start correction.
        Assert.NotNull(button);
        Assert.Equal("位置を補正", (string?)button.Attribute("Content"));
        Assert.Equal("OnCorrectionModeClick", (string?)button.Attribute("Click"));
    }

    [Fact]
    public void ShouldShowPositionCorrectionOnlyForManuallyAddedProfile()
    {
        // Given: The main-window profile selection implementation.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml.cs"));

        // Then: Selection updates visibility through the bundled-profile-aware policy.
        Assert.Contains("_bundledProfiles = bundledCatalog.Profiles", source);
        Assert.Contains("UpdateCorrectionModeButtonAvailability(profile)", source);
        Assert.Contains("PositionCorrectionAvailability.IsAvailable(profile, _bundledProfiles)", source);
        Assert.Contains("CorrectionModeButton.Visibility =", source);
    }

    [Fact]
    public void ShouldExposeExplicitConfirmationForDroppedCorrection()
    {
        // Given: The main-window markup.
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        // When: The correction confirmation action is located.
        var button = document.Descendants()
            .SingleOrDefault(element =>
                (string?)element.Attribute(xaml + "Name") == "ConfirmCorrectionButton");

        // Then: Saving requires an explicit confirmation click.
        Assert.NotNull(button);
        Assert.Equal("補正を確定", (string?)button.Attribute("Content"));
        Assert.Equal("OnConfirmCorrectionClick", (string?)button.Attribute("Click"));
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
