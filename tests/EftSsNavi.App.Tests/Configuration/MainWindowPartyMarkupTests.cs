using System.Xml.Linq;

namespace EftSsNavi.App.Tests.Configuration;

public sealed class MainWindowPartyMarkupTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ShouldReachPartyFlyoutFromToolbarButton()
    {
        // Given: The main-window markup.
        var document = LoadMarkup();

        // When: The toolbar party entry is located.
        var button = FindNamedElement(document, "PartyButton");

        // Then: A clearly named button opens the party UI.
        Assert.Equal("Button", button.Name.LocalName);
        Assert.Equal("グループ", (string?)button.Attribute("Content"));
        Assert.Equal("OnPartyClick", (string?)button.Attribute("Click"));
    }

    [Fact]
    public void ShouldOfferDisplayNameAndRoomCodeInputsBeforeJoining()
    {
        // Given: The main-window party flyout markup.
        var document = LoadMarkup();

        // When: The pre-session form controls are located.
        var displayName = FindNamedElement(document, "PartyDisplayNameTextBox");
        var roomCode = FindNamedElement(document, "PartyRoomCodeTextBox");

        // Then: Users can provide both values required to host or join.
        Assert.Equal("TextBox", displayName.Name.LocalName);
        Assert.Equal("TextBox", roomCode.Name.LocalName);
        Assert.Equal("19", (string?)roomCode.Attribute("MaxLength"));
        Assert.Equal("XXXX-XXXX-XXXX-XXXX", (string?)roomCode.Attribute("PlaceholderText"));
        Assert.Equal("-------------------", (string?)FindNamedElement(document, "PartyFlyoutRoomCodeText").Attribute("Text"));
    }

    [Fact]
    public void ShouldOfferHostAndJoinActionsBeforeJoining()
    {
        // Given: The main-window party flyout markup.
        var document = LoadMarkup();

        // When: The pre-session actions are located.
        var host = FindNamedElement(document, "HostPartyButton");
        var join = FindNamedElement(document, "JoinPartyButton");

        // Then: Both supported entry paths are actionable.
        Assert.Equal("OnHostPartyClick", (string?)host.Attribute("Click"));
        Assert.Equal("OnJoinPartyClick", (string?)join.Attribute("Click"));
    }

    [Fact]
    public void ShouldOfferHostSessionOperations()
    {
        // Given: The main-window party flyout markup.
        var document = LoadMarkup();

        // When: Host-only session actions are located.
        var copy = FindNamedElement(document, "CopyPartyCodeButton");
        var reissue = FindNamedElement(document, "ReissuePartyCodeButton");
        var end = FindNamedElement(document, "EndPartyButton");

        // Then: The host can share, rotate, and end the room.
        Assert.Equal("OnCopyPartyCodeClick", (string?)copy.Attribute("Click"));
        Assert.Equal("OnReissuePartyCodeClick", (string?)reissue.Attribute("Click"));
        Assert.Equal("OnEndPartyClick", (string?)end.Attribute("Click"));
    }

    [Fact]
    public void ShouldOfferParticipantLeaveOperation()
    {
        // Given: The main-window party flyout markup.
        var document = LoadMarkup();

        // When: The participant session action is located.
        var leave = FindNamedElement(document, "LeavePartyButton");

        // Then: A participant can leave without ending the host room.
        Assert.Equal("OnLeavePartyClick", (string?)leave.Attribute("Click"));
    }

    [Fact]
    public void ShouldExposePartySectionAndParticipantListInRightSidebar()
    {
        // Given: The main-window markup.
        var document = LoadMarkup();

        // When: The persistent party summary is located.
        var section = FindNamedElement(document, "PartySection");
        var title = FindNamedElement(document, "PartySectionTitleText");
        var list = FindNamedElement(document, "PartyParticipantList");

        // Then: Current party membership is visible outside the flyout.
        Assert.Equal("StackPanel", section.Name.LocalName);
        Assert.Equal("グループ", (string?)title.Attribute("Text"));
        Assert.Equal("ItemsControl", list.Name.LocalName);
    }

    [Fact]
    public void ShouldKeepRoomCodeOnlyInsideFlyout()
    {
        // Given: The main-window party UI.
        var document = LoadMarkup();

        // When: Room-code displays are located.
        var flyoutRoomCode = FindNamedElement(document, "PartyFlyoutRoomCodeText");

        // Then: The code remains available in the flyout but not in the right sidebar.
        Assert.Equal("TextBlock", flyoutRoomCode.Name.LocalName);
        Assert.DoesNotContain(document.Descendants(), element =>
            (string?)element.Attribute(Xaml + "Name") == "PartyRoomCodeText");
    }

    [Fact]
    public void ShouldShowNotJoinedStatusInPersistentPartySection()
    {
        var document = LoadMarkup();

        var section = FindNamedElement(document, "PartySection");
        var notJoined = FindNamedElement(document, "PartyNotJoinedText");

        Assert.NotEqual("Collapsed", (string?)section.Attribute("Visibility"));
        Assert.Equal("未参加", (string?)notJoined.Attribute("Text"));
    }

    [Fact]
    public void ShouldPlaceLocalRedParticipantBeforeRemoteParticipantList()
    {
        // Given: The persistent party summary markup.
        var document = LoadMarkup();

        // When: The local row and remote list are located in document order.
        var partySection = FindNamedElement(document, "PartySection");
        var descendants = partySection.Descendants().ToArray();
        var selfIndex = Array.FindIndex(descendants, element =>
            (string?)element.Attribute(Xaml + "Name") == "PartySelfParticipantRow");
        var remoteIndex = Array.FindIndex(descendants, element =>
            (string?)element.Attribute(Xaml + "Name") == "PartyParticipantList");

        // Then: The local participant is shown first using the existing self-marker red.
        Assert.True(selfIndex >= 0 && selfIndex < remoteIndex);
        var selfRow = descendants[selfIndex];
        Assert.Contains(selfRow.Descendants(), element =>
            string.Equals((string?)element.Attribute("Fill"), "#EE3242", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShouldRenderLocalParticipantNameAndStatusOnOneRow()
    {
        var document = LoadMarkup();

        var selfRow = FindNamedElement(document, "PartySelfParticipantRow");
        var directTextBlocks = selfRow.Elements()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => (string?)element.Attribute(Xaml + "Name"))
            .ToArray();

        Assert.Contains("PartySelfDisplayNameText", directTextBlocks);
        Assert.Contains("PartySelfStatusText", directTextBlocks);
    }

    [Fact]
    public void ShouldCenterLocalParticipantMarkerInConsistentSlot()
    {
        var document = LoadMarkup();

        var selfRow = FindNamedElement(document, "PartySelfParticipantRow");
        var markerSlot = selfRow.Elements().Single(element => element.Name.LocalName == "Canvas");
        var marker = markerSlot.Elements().Single(element => element.Name.LocalName == "Ellipse");

        Assert.Equal("24", (string?)markerSlot.Attribute("Width"));
        Assert.Equal("20", (string?)markerSlot.Attribute("Height"));
        Assert.Equal("Center", (string?)markerSlot.Attribute("VerticalAlignment"));
        Assert.Equal("6", (string?)marker.Attribute("Canvas.Left"));
        Assert.Equal("4", (string?)marker.Attribute("Canvas.Top"));
    }

    [Fact]
    public void ShouldNameMapActionsThatParticipantStateDisables()
    {
        // Given: The main-window toolbar markup.
        var document = LoadMarkup();

        // When: The three participant-restricted map actions are located.
        var selector = FindNamedElement(document, "ProfileMenuButton");
        var add = FindNamedElement(document, "AddMapMenuItem");
        var delete = FindNamedElement(document, "DeleteMapMenuItem");

        // Then: MainWindow can disable the actual controls, not only ignore their handlers.
        Assert.Equal("DropDownButton", selector.Name.LocalName);
        Assert.Equal("MenuFlyoutItem", add.Name.LocalName);
        Assert.Equal("MenuFlyoutItem", delete.Name.LocalName);
    }

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == name);

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
