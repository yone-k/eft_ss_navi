using System.Xml.Linq;

namespace EftSsNavi.App.Tests.Configuration;

public sealed class MainWindowMenuMarkupTests
{
    [Fact]
    public void ShouldExposeFiveApplicationMenuCategoriesInOrder()
    {
        // Given: The main-window markup.
        var document = LoadMarkup();

        // When: The top-level application menu is inspected.
        var menuBar = FindNamedElement(document, "ApplicationMenuBar");
        var headers = menuBar.Elements()
            .Where(element => element.Name.LocalName == "MenuBarItem")
            .Select(element => (string?)element.Attribute("Title") ?? string.Empty)
            .ToArray();

        // Then: It exposes only the agreed desktop-application categories, in order.
        Assert.Equal(["ファイル", "マップ", "表示", "グループ", "ヘルプ"], headers);
    }

    [Theory]
    [InlineData("MenuFlyoutItem", "ChangeWatchDirectoryMenuItem", "監視先を変更")]
    [InlineData("MenuFlyoutSubItem", "SelectMapMenu", "マップを選択")]
    [InlineData("MenuFlyoutItem", "NoMapsMenuItem", "マップが登録されていません")]
    [InlineData("MenuFlyoutItem", "AddMapMenuItem", "マップを追加")]
    [InlineData("MenuFlyoutItem", "DeleteMapMenuItem", "マップを削除")]
    [InlineData("MenuFlyoutItem", "StartCorrectionMenuItem", "位置を補正")]
    [InlineData("MenuFlyoutItem", "CancelCorrectionMenuItem", "補正をキャンセル")]
    [InlineData("MenuFlyoutItem", "ConfirmCorrectionMenuItem", "補正を確定")]
    [InlineData("MenuFlyoutItem", "RotateMapLeftMenuItem", "左へ90度回転")]
    [InlineData("MenuFlyoutItem", "RotateMapRightMenuItem", "右へ90度回転")]
    [InlineData("MenuFlyoutItem", "FitMapMenuItem", "マップ全体を表示")]
    [InlineData("MenuFlyoutItem", "OpenPartyMenuItem", "グループを開く")]
    [InlineData("MenuFlyoutItem", "CheckForUpdatesMenuItem", "アップデートを確認")]
    [InlineData("MenuFlyoutItem", "AboutMenuItem", "バージョン情報")]
    public void ShouldExposeNamedApplicationMenuAction(
        string elementType,
        string elementName,
        string text)
    {
        // Given: The main-window markup.
        var document = LoadMarkup();

        // When: A planned application-menu action is located by its integration name.
        var element = FindNamedElement(document, elementName);

        // Then: Its control type and user-facing label match the menu contract.
        Assert.Equal(elementType, element.Name.LocalName);
        Assert.Equal(text, (string?)element.Attribute("Text"));
    }

    [Fact]
    public void ShouldGroupApplicationMenuActionsByPurpose()
    {
        // Given: The five top-level application-menu categories.
        var document = LoadMarkup();

        // When: Each category's direct actions are inspected.
        var actionsByCategory = new Dictionary<string, string[]>
        {
            ["FileMenu"] = ["ChangeWatchDirectoryMenuItem"],
            ["MapMenu"] =
            [
                "SelectMapMenu",
                "AddMapMenuItem",
                "DeleteMapMenuItem",
                "StartCorrectionMenuItem",
                "CancelCorrectionMenuItem",
                "ConfirmCorrectionMenuItem",
            ],
            ["ViewMenu"] =
            [
                "RotateMapLeftMenuItem",
                "RotateMapRightMenuItem",
                "FitMapMenuItem",
            ],
            ["PartyMenu"] = ["OpenPartyMenuItem"],
            ["HelpMenu"] = ["CheckForUpdatesMenuItem", "AboutMenuItem"],
        };

        // Then: Every action belongs to its agreed category, with no extra actions.
        foreach (var (categoryName, expectedActions) in actionsByCategory)
        {
            var category = FindNamedElement(document, categoryName);
            var actualActions = category.Elements()
                .Where(element => element.Name.LocalName is "MenuFlyoutItem" or "MenuFlyoutSubItem")
                .Select(GetElementName)
                .ToArray();

            Assert.Equal(expectedActions, actualActions);
        }

        var selectMapMenu = FindNamedElement(document, "SelectMapMenu");
        Assert.Equal(
            ["NoMapsMenuItem"],
            selectMapMenu.Elements().Select(element => GetElementName(element) ?? string.Empty));
    }

    [Fact]
    public void ShouldPlaceMenuAboveExistingToolbarAndContent()
    {
        // Given: The main-window root grid.
        var document = LoadMarkup();
        var rootGrid = FindNamedElement(document, "RootGrid");
        var rowDefinitions = rootGrid.Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements()
            .Select(element => (string?)element.Attribute("Height") ?? string.Empty)
            .ToArray();

        // When: The menu and existing toolbar rows are inspected.
        var menuBar = FindNamedElement(document, "ApplicationMenuBar");
        var toolbar = FindNamedElement(document, "MainToolbar");

        // Then: A dedicated auto-sized menu row precedes the auto-sized toolbar row.
        Assert.Equal(["Auto", "Auto", "*", "Auto"], rowDefinitions);
        Assert.Equal("0", (string?)menuBar.Attribute("Grid.Row"));
        Assert.Equal("1", (string?)toolbar.Attribute("Grid.Row"));
    }

    [Fact]
    public void ShouldStartConditionalAndUpdateActionsInSafeState()
    {
        // Given: The application menu actions that depend on runtime state.
        var document = LoadMarkup();

        // When: Their initial XAML state is inspected.
        var noMaps = FindNamedElement(document, "NoMapsMenuItem");
        var cancelCorrection = FindNamedElement(document, "CancelCorrectionMenuItem");
        var confirmCorrection = FindNamedElement(document, "ConfirmCorrectionMenuItem");
        var checkForUpdates = FindNamedElement(document, "CheckForUpdatesMenuItem");

        // Then: Placeholders cannot be invoked, conditional actions are hidden, and
        // update checking waits until startup coordination enables it.
        Assert.Equal("False", (string?)noMaps.Attribute("IsEnabled"));
        Assert.Equal("Collapsed", (string?)cancelCorrection.Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)confirmCorrection.Attribute("Visibility"));
        Assert.Equal("False", (string?)checkForUpdates.Attribute("IsEnabled"));
    }

    [Theory]
    [InlineData("ChangeWatchDirectoryMenuItem", "OnChangeWatchDirectoryMenuClick")]
    [InlineData("AddMapMenuItem", "OnNewProfileClick")]
    [InlineData("DeleteMapMenuItem", "OnDeleteProfileClick")]
    [InlineData("StartCorrectionMenuItem", "OnCorrectionModeClick")]
    [InlineData("CancelCorrectionMenuItem", "OnCorrectionModeClick")]
    [InlineData("ConfirmCorrectionMenuItem", "OnConfirmCorrectionClick")]
    [InlineData("RotateMapLeftMenuItem", "OnRotateMapLeftClick")]
    [InlineData("RotateMapRightMenuItem", "OnRotateMapRightClick")]
    [InlineData("FitMapMenuItem", "OnFitMapClick")]
    [InlineData("OpenPartyMenuItem", "OnPartyClick")]
    [InlineData("CheckForUpdatesMenuItem", "OnCheckForUpdatesClick")]
    [InlineData("AboutMenuItem", "OnAboutClick")]
    public void ShouldWireExistingMenuActionToSharedHandler(string elementName, string handler)
    {
        // Given: An application-menu action backed by an existing operation.
        var document = LoadMarkup();
        var action = FindNamedElement(document, elementName);

        // Then: Both menu and toolbar entry points can reach the same operation.
        Assert.Equal(handler, (string?)action.Attribute("Click"));
    }

    [Fact]
    public void ShouldKeepMapSelectionMenuSynchronizedWithProfiles()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("_profiles.CollectionChanged += OnProfilesChanged", source);
        Assert.Contains("RebuildMapSelectionMenu()", source);
        Assert.Contains(
            "_profiles.OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)",
            source);
    }

    private static XElement FindNamedElement(XDocument document, string name)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        return document.Descendants().Single(element =>
            (string?)element.Attribute(xaml + "Name") == name);
    }

    private static string? GetElementName(XElement element)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        return (string?)element.Attribute(xaml + "Name");
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
