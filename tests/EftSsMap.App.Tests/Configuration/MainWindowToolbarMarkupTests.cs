using System.Xml.Linq;

namespace EftSsMap.App.Tests.Configuration;

public sealed class MainWindowToolbarMarkupTests
{
    [Fact]
    public void ShouldExposeClearlyNamedMapActionsWithoutRecalibration()
    {
        var document = LoadMarkup();

        var buttonLabels = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => (string?)element.Attribute("Content"))
            .ToArray();

        Assert.Contains("マップを追加", buttonLabels);
        Assert.Contains("マップを削除", buttonLabels);
        Assert.Contains("マップ全体を表示", buttonLabels);
        Assert.DoesNotContain("新規", buttonLabels);
        Assert.DoesNotContain("削除", buttonLabels);
        Assert.DoesNotContain("再校正", buttonLabels);
    }

    [Fact]
    public void ShouldOpenMapSelectionFromButtonInsteadOfComboBox()
    {
        var document = LoadMarkup();
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var menuButton = document.Descendants()
            .SingleOrDefault(element =>
                (string?)element.Attribute(xaml + "Name") == "ProfileMenuButton");

        Assert.NotNull(menuButton);
        Assert.Equal("OnProfileMenuClick", (string?)menuButton.Attribute("Click"));
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Name.LocalName == "ComboBox"
            && (string?)element.Attribute(xaml + "Name") == "ProfileComboBox");
    }

    [Fact]
    public void ShouldPlaceMapMenuBelowSelectionButton()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft", source);
    }

    [Fact]
    public void ShouldDescribeMapNavigationInPlainLanguage()
    {
        var document = LoadMarkup();

        var footer = document.Descendants()
            .Single(element => (string?)element.Attribute("Text") ==
                "ホイール: 拡大・縮小 / ドラッグ: マップを移動");

        Assert.NotNull(footer);
    }

    private static XDocument LoadMarkup() => XDocument.Load(Path.Combine(
        FindRepositoryRoot(),
        "src",
        "EftSsMap.App",
        "MainWindow.xaml"));

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
