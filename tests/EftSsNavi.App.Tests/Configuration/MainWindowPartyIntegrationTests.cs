using EftSsNavi.App.Presentation;

namespace EftSsNavi.App.Tests.Configuration;

public sealed class MainWindowPartyIntegrationTests
{
    [Theory]
    [InlineData(PartyUiRole.NotJoined, "グループ")]
    [InlineData(PartyUiRole.Participant, "グループ")]
    [InlineData(PartyUiRole.Host, "グループ (ホスト中)")]
    public void ShouldDescribeGroupSectionTitleForCurrentRole(PartyUiRole role, string expected)
    {
        // Given: A group UI state for the current role.
        var state = new PartyUiState(role, hostMapName: null, hasMatchingProfile: false);

        // When: The right-sidebar heading is resolved.
        var title = state.GroupSectionTitle;

        // Then: Only the host heading exposes its host status.
        Assert.Equal(expected, title);
    }

    [Fact]
    public void ShouldDisableMapActionsWhenJoinedAsParticipant()
    {
        // Given: The pure UI state for a joined non-host participant.
        var state = new PartyUiState(PartyUiRole.Participant, hostMapName: "Woods", hasMatchingProfile: true);

        // When: Map-action availability is evaluated.
        var enabled = state.MapActionsEnabled;

        // Then: Map selection, addition, and deletion are disabled together.
        Assert.False(enabled);
    }

    [Theory]
    [InlineData(PartyUiRole.NotJoined)]
    [InlineData(PartyUiRole.Host)]
    public void ShouldEnableMapActionsWhenMapOwnershipIsLocal(PartyUiRole role)
    {
        // Given: A UI state where this user owns local map selection.
        var state = new PartyUiState(role, hostMapName: "Woods", hasMatchingProfile: true);

        // When: Map-action availability is evaluated.
        var enabled = state.MapActionsEnabled;

        // Then: Existing map workflows remain available.
        Assert.True(enabled);
    }

    [Fact]
    public void ShouldHidePartyMarkersWhenHostHasNotSelectedMap()
    {
        // Given: A participant receives a null host map.
        var state = new PartyUiState(PartyUiRole.Participant, hostMapName: null, hasMatchingProfile: false);

        // When: Party marker visibility is evaluated.
        var visible = state.PartyMarkersVisible;

        // Then: Only remote party markers are hidden.
        Assert.False(visible);
    }

    [Fact]
    public void ShouldExplainWhenHostHasNotSelectedMap()
    {
        // Given: A participant receives a null host map.
        var state = new PartyUiState(PartyUiRole.Participant, hostMapName: null, hasMatchingProfile: false);

        // When: The UI status is resolved.
        var message = state.MapStatusMessage;

        // Then: The user is told that the host has no selected map.
        Assert.Contains("ホスト", message);
        Assert.Contains("マップ", message);
        Assert.Contains("選択", message);
    }

    [Fact]
    public void ShouldExplainWhenMatchingHostMapIsUnavailable()
    {
        // Given: A participant receives a host map absent from local profiles.
        var state = new PartyUiState(PartyUiRole.Participant, hostMapName: "Woods", hasMatchingProfile: false);

        // When: The UI status is resolved.
        var message = state.MapStatusMessage;

        // Then: The missing host map is named in the status.
        Assert.Contains("Woods", message);
        Assert.Contains("ありません", message);
    }

    [Fact]
    public void ShouldRefreshPartyAgeAndStaleStateEverySecond()
    {
        // Given: The pure party UI state contract.
        var expectedInterval = TimeSpan.FromSeconds(1);

        // When: Its requested refresh cadence is read.
        var interval = PartyUiState.RefreshInterval;

        // Then: Last-received ages and stale rendering refresh once per second.
        Assert.Equal(expectedInterval, interval);
    }

    [Fact]
    public void ShouldDescribeParticipantWithoutReceivedPosition()
    {
        // Given: A participant has joined but has not sent a position.
        const bool hasPosition = false;

        // When: The list status is formatted by the pure UI state helper.
        var status = PartyUiState.FormatParticipantPositionStatus(
            hasPosition,
            isOnSelectedMap: false,
            age: TimeSpan.Zero);

        // Then: The list communicates that no position was received.
        Assert.Equal("位置未受信", status);
    }

    [Fact]
    public void ShouldDescribeParticipantOnDifferentMap()
    {
        // Given: A participant has a position for another map.
        const bool hasPosition = true;

        // When: The list status is formatted by the pure UI state helper.
        var status = PartyUiState.FormatParticipantPositionStatus(
            hasPosition,
            isOnSelectedMap: false,
            age: TimeSpan.FromSeconds(5));

        // Then: The list communicates the map mismatch instead of an age.
        Assert.Equal("別マップ", status);
    }

    [Fact]
    public void ShouldNameDifferentParticipantMapWhenAvailable()
    {
        var status = PartyUiState.FormatParticipantPositionStatus(
            hasPosition: true,
            isOnSelectedMap: false,
            age: TimeSpan.Zero,
            positionMapName: "Customs");

        Assert.Equal("別マップ: Customs", status);
    }

    [Fact]
    public void ShouldUseExactWaitingMessageWhenHostMapIsNull()
    {
        var state = new PartyUiState(PartyUiRole.Participant, hostMapName: null, hasMatchingProfile: false);

        Assert.Equal("ホストがマップを選択するまで待機しています。", state.MapStatusMessage);
    }

    [Fact]
    public void ShouldUseExactMissingMapMessage()
    {
        var state = new PartyUiState(PartyUiRole.Participant, hostMapName: "Woods", hasMatchingProfile: false);

        Assert.Equal("ホストのマップ「Woods」がこのPCにありません。", state.MapStatusMessage);
    }

    [Fact]
    public void ShouldDescribeParticipantPositionAgeInWholeSeconds()
    {
        // Given: A participant has a position on the selected map.
        const bool hasPosition = true;

        // When: The list status is formatted by the pure UI state helper.
        var status = PartyUiState.FormatParticipantPositionStatus(
            hasPosition,
            isOnSelectedMap: true,
            age: TimeSpan.FromMilliseconds(12_900));

        // Then: The list communicates deterministic elapsed whole seconds.
        Assert.Equal("12秒前", status);
    }

    [Fact]
    public void ShouldRenderRemoteParticipantNameAndStatusOnOneRow()
    {
        var source = LoadMainWindowSource();
        var method = ExtractMethod(source, "private StackPanel CreateParticipantRow");

        Assert.True(
            method.Split("row.Children.Add(new TextBlock", StringSplitOptions.None).Length - 1 >= 2,
            "Expected both the participant name and status to be direct children of the horizontal row.");
        Assert.Contains("Text = participant.DisplayName", method, StringComparison.Ordinal);
        Assert.Contains("Text = status", method, StringComparison.Ordinal);
        Assert.DoesNotContain("var labels = new StackPanel", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldCenterRemoteParticipantMarkerInSameSlotAsLocalParticipant()
    {
        var source = LoadMainWindowSource();
        var method = ExtractMethod(source, "private StackPanel CreateParticipantRow");

        Assert.Contains("var markerSlot = new Canvas", method, StringComparison.Ordinal);
        Assert.Contains("Width = 24", method, StringComparison.Ordinal);
        Assert.Contains("Height = 20", method, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment = VerticalAlignment.Center", method, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetLeft(marker, 6)", method, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetTop(marker, 4)", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldApplyPartyMapAvailabilityToMapSelectorsAndManagementMenuItems()
    {
        // Given: MainWindow applies a pure PartyUiState snapshot.
        var source = LoadMainWindowSource();

        // When: Its view-application path is inspected.
        var method = ExtractMethod(source, "private void ApplyPartyUiState");

        // Then: Map selection and management actions are controlled by the snapshot.
        Assert.Contains("ProfileMenuButton.IsEnabled", method, StringComparison.Ordinal);
        Assert.Contains("SelectMapMenu.IsEnabled", method, StringComparison.Ordinal);
        Assert.Contains("AddMapMenuItem.IsEnabled", method, StringComparison.Ordinal);
        Assert.Contains("DeleteMapMenuItem.IsEnabled", method, StringComparison.Ordinal);
        Assert.Contains("MapActionsEnabled", method, StringComparison.Ordinal);
        Assert.Contains("PartySectionTitleText.Text", method, StringComparison.Ordinal);
        Assert.Contains("GroupSectionTitle", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldUseGroupTermInMainWindowUserFacingMessages()
    {
        var source = LoadMainWindowSource();

        Assert.DoesNotContain("パーティ", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldConfigureDispatcherTimerFromPureRefreshInterval()
    {
        // Given: MainWindow creates a timer for participant age and stale refresh.
        var source = LoadMainWindowSource();

        // When: Its timer initialization is inspected.
        var hasDispatcherTimer = source.Contains("DispatcherQueue.CreateTimer", StringComparison.Ordinal);

        // Then: The WinUI timer consumes the independently tested one-second interval.
        Assert.True(hasDispatcherTimer);
        Assert.Contains("PartyUiState.RefreshInterval", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRoutePartyCallbacksThroughUiQueue()
    {
        // Given: MainWindow receives coordinator state callbacks from networking threads.
        var source = LoadMainWindowSource();

        // When: The party callback handler is inspected.
        var handler = ExtractMethod(source, "private void OnPartyStateChanged");

        // Then: UI changes are marshalled through the existing guarded queue.
        Assert.Contains("EnqueueOnUi", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldSendAcceptedObservationOutsideCalibrationThroughCoordinator()
    {
        // Given: MainWindow's accepted-observation flow.
        var source = LoadMainWindowSource();

        // When: The flow is inspected for party forwarding.
        var handler = ExtractMethod(source, "private void OnObservationAccepted");

        // Then: Accepted data is forwarded through the party coordinator.
        Assert.Contains("_partyCoordinator", handler, StringComparison.Ordinal);
        Assert.Contains("CapturedAt.ToUniversalTime()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldNotConnectToNetworkFromWindowConstructor()
    {
        // Given: The MainWindow construction path used before a party action.
        var source = LoadMainWindowSource();

        // When: The constructor body is inspected.
        var constructor = ExtractMethod(source, "public MainWindow()");

        // Then: No broker, DNS, or WebRTC connection is started eagerly.
        Assert.DoesNotContain("ConnectAsync", constructor, StringComparison.Ordinal);
        Assert.DoesNotContain("StartHostAsync", constructor, StringComparison.Ordinal);
        Assert.DoesNotContain("JoinAsync", constructor, StringComparison.Ordinal);
        Assert.DoesNotContain("Dns", constructor, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldLoadAndPersistAllPartyConnectionSettings()
    {
        // Given: MainWindow's settings integration source.
        var source = LoadMainWindowSource();

        // When: The Worker-based party settings are traced through load and save.
        string[] settingNames =
        [
            "PartyDisplayName",
            "SignalingWorkerUrl",
            "StunServers",
        ];

        // Then: Every setting participates in MainWindow's retained configuration.
        Assert.All(settingNames, settingName => Assert.Contains(settingName, source, StringComparison.Ordinal));
        Assert.DoesNotContain("SignalingBrokerHost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SignalingBrokerPort", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SignalingUsername", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SignalingPassword", source, StringComparison.Ordinal);
        Assert.Contains("PartyCoordinatorFactory.Create(settings", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldKeepSignalingSocketCreationLazyUntilPartyAction()
    {
        // Given: The production coordinator composition root.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "Presentation",
            "PartyCoordinatorFactory.cs"));

        // When: WebSocket construction and connection ownership are inspected.

        // Then: The concrete socket is behind WorkerRoomSignaling's action-time factory and never connected here.
        Assert.Contains("new WorkerRoomSignaling", source, StringComparison.Ordinal);
        Assert.Contains("socketFactory: () => new ClientWebSocketSignalingSocket", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldUseBundledWorkerUrlWhenSettingIsMissing()
    {
        // Given: The production coordinator composition source.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "Presentation",
            "PartyCoordinatorFactory.cs"));

        // Then: A missing user setting explicitly falls back to the bundled URL.
        Assert.Contains(
            "settings.SignalingWorkerUrl ?? SignalingDefaults.WorkerUrl",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldNormalizeAndFormatWorkerRoomCodesAtEveryUiBoundary()
    {
        // Given: MainWindow accepts, displays, and copies a room code.
        var source = LoadMainWindowSource();

        // When: The room-code integration is inspected.
        var join = ExtractMethod(source, "private async void OnJoinPartyClick");
        var copy = ExtractMethod(source, "private void OnCopyPartyCodeClick");
        var refresh = ExtractMethod(source, "private void RefreshPartyView");

        // Then: Input uses protocol normalization and all output uses grouped formatting.
        Assert.Contains("RoomCode.TryNormalize", join, StringComparison.Ordinal);
        Assert.Contains("16文字の有効なルームコード", join, StringComparison.Ordinal);
        Assert.Contains("RoomCode.Format", copy, StringComparison.Ordinal);
        Assert.Contains("RoomCode.Format", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldShowWorkerRejectionReasonForHostAndJoinActions()
    {
        // Given: Host and participant actions can receive a typed Worker rejection.
        var source = LoadMainWindowSource();

        // When: Both action handlers are inspected.
        var host = ExtractMethod(source, "private async void OnHostPartyClick");
        var join = ExtractMethod(source, "private async void OnJoinPartyClick");

        // Then: Each action maps the typed reason to the dedicated UI message.
        Assert.Contains("exception.RejectReason", host, StringComparison.Ordinal);
        Assert.Contains("PartyStatusMessages.ForSignalingRejection", host, StringComparison.Ordinal);
        Assert.Contains("exception.RejectReason", join, StringComparison.Ordinal);
        Assert.Contains("PartyStatusMessages.ForSignalingRejection", join, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldUseDedicatedStatusWhenRoomCodeReissueFails()
    {
        // Given: Reissuing a room code can fail after the old Worker room is closed.
        var source = LoadMainWindowSource();

        // When: The reissue handler is inspected.
        var handler = ExtractMethod(source, "private async void OnReissuePartyCodeClick");

        // Then: Failure uses the retryable Issue-defined message rather than transport details.
        Assert.Contains("PartyStatusMessages.RoomCodeReissueFailure", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDiscardPartyStateWhenNewerCallbackArrivesDuringMapActivation()
    {
        var source = LoadMainWindowSource();
        var handler = ExtractMethod(source, "private async Task ApplyPartyCoordinatorStateAsync");

        Assert.Contains("generation", handler, StringComparison.Ordinal);
        Assert.Contains("_partyStateGeneration", handler, StringComparison.Ordinal);
        Assert.Contains("await ActivateProfileAsync", handler, StringComparison.Ordinal);
        Assert.Contains("if (generation != _partyStateGeneration)", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldCancelAndAwaitActivePartyOperationBeforeWindowCleanup()
    {
        var source = LoadMainWindowSource();
        var handler = ExtractMethod(source, "private async void OnAppWindowClosing");

        Assert.Contains("_partyOperationCancellation", handler, StringComparison.Ordinal);
        Assert.Contains("Cancel()", handler, StringComparison.Ordinal);
        Assert.Contains("_partyOperationTask", handler, StringComparison.Ordinal);
        Assert.Contains("await", handler, StringComparison.Ordinal);
        Assert.Contains("EndAsync", handler, StringComparison.Ordinal);
        Assert.Contains("LeaveAsync", handler, StringComparison.Ordinal);
        Assert.Contains("DisposeAsync", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldExcludeFingerprintMismatchFromPartyProjection()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("_partyProjectionCalibrationValid = false", source, StringComparison.Ordinal);
        Assert.Contains("_partyProjectionCalibrationValid = calibrationValid", source, StringComparison.Ordinal);
        Assert.Contains("PartyMarkerProjector.Project(", source, StringComparison.Ordinal);
        Assert.Contains("_partyProjectionCalibrationValid,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldNotUseParticipantJoinTimeoutMessageForHostStartup()
    {
        var source = LoadMainWindowSource();
        var handler = ExtractMethod(source, "private async void OnHostPartyClick");

        Assert.Contains("HostSignalingFailure", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("JoinTimeout", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldResolveMapWarningBeforeShowingJoinSuccess()
    {
        var source = LoadMainWindowSource();
        var handler = ExtractMethod(source, "private async void OnJoinPartyClick");

        Assert.Contains("ApplyJoinCompletionStatus", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(\"グループに参加しました。\")", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldStopPartyTimerAndCoordinatorWhenWindowCloses()
    {
        // Given: MainWindow's final cleanup path.
        var source = LoadMainWindowSource();

        // When: The Window.Closed handler is inspected.
        var handler = ExtractMethod(source, "private void OnWindowClosed");

        // Then: Party refresh and callback resources are released.
        Assert.Contains("_partyRefreshTimer.Stop", handler, StringComparison.Ordinal);
        Assert.Contains("_partyCoordinator", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldCancelFirstWindowCloseUntilPartyShutdownCompletes()
    {
        // Given: An active party may need to send Goodbye before process exit.
        var source = LoadMainWindowSource();

        // When: The AppWindow closing path is inspected.
        var handler = ExtractMethod(source, "private async void OnAppWindowClosing");

        // Then: The first close is canceled and resumed after asynchronous party shutdown.
        Assert.Contains("args.Cancel = true", handler, StringComparison.Ordinal);
        Assert.Contains("await", handler, StringComparison.Ordinal);
        Assert.Contains("Close()", handler, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected method '{signature}' was not found.");
        var end = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not determine the end of method '{signature}'.");
        return source[start..end];
    }

    private static string LoadMainWindowSource() => File.ReadAllText(Path.Combine(
        FindRepositoryRoot(),
        "src",
        "EftSsNavi.App",
        "MainWindow.xaml.cs"));

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
