using System.Xml.Linq;

namespace EftSsNavi.App.Tests.Configuration;

public sealed class MainWindowToolbarMarkupTests
{
    [Fact]
    public void ShouldAlignMapSelectionLeftAndFrequentActionsRight()
    {
        // Given: The main toolbar markup.
        var document = LoadMarkup();
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var toolbar = document.Descendants().Single(element =>
            (string?)element.Attribute(xaml + "Name") == "MainToolbar");

        // When: The two toolbar groups are inspected.
        var mapSelectionGroup = toolbar.Descendants().Single(element =>
            (string?)element.Attribute(xaml + "Name") == "MapSelectionToolbarGroup");
        var frequentActionsGroup = toolbar.Descendants().Single(element =>
            (string?)element.Attribute(xaml + "Name") == "FrequentActionsToolbarGroup");

        // Then: Map selection stays left while the frequent actions are right-aligned.
        Assert.Equal("0", (string?)mapSelectionGroup.Attribute("Grid.Column"));
        Assert.Equal("Left", (string?)mapSelectionGroup.Attribute("HorizontalAlignment"));
        Assert.Equal("2", (string?)frequentActionsGroup.Attribute("Grid.Column"));
        Assert.Equal("Right", (string?)frequentActionsGroup.Attribute("HorizontalAlignment"));
        Assert.Contains(frequentActionsGroup.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "RotateMapLeftButton");
        Assert.Contains(frequentActionsGroup.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "RotateMapRightButton");
        Assert.Contains(frequentActionsGroup.Descendants(), element =>
            (string?)element.Attribute("Content") == "マップ全体を表示");
        Assert.Contains(frequentActionsGroup.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "PartyButton");
    }

    [Fact]
    public void ShouldKeepFrequentActionsAndHideManagementActions()
    {
        var document = LoadMarkup();
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var toolbar = document.Descendants().Single(element =>
            (string?)element.Attribute(xaml + "Name") == "MainToolbar");

        var buttonLabels = toolbar.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => (string?)element.Attribute("Content"))
            .ToArray();

        Assert.Contains("マップ全体を表示", buttonLabels);
        Assert.Contains("グループ", buttonLabels);
        Assert.DoesNotContain("マップを追加", buttonLabels);
        Assert.DoesNotContain("マップを削除", buttonLabels);
        Assert.DoesNotContain("位置を補正", buttonLabels);
        Assert.DoesNotContain(toolbar.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "WatchDirectoryText");
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
            "EftSsNavi.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft", source);
    }

    [Fact]
    public void ShouldOrderMapMenuAlphabetically()
    {
        // Given: The map-menu construction source.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "MainWindow.xaml.cs"));

        // Then: Profiles are ordered by display name every time the menu opens.
        Assert.Contains(
            "_profiles.OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)",
            source);
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
