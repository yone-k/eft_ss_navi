using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;

namespace EftSsNavi.Sharing.Tests.Coordination;

public sealed class CoordinatorContractTests
{
    [Fact]
    public void ShouldExposeCompleteApplicationOperationsThroughCoordinatorPort()
    {
        // Given: The application-facing coordinator boundary.
        var methods = typeof(IPartyCoordinator).GetMethods().Select(method => method.Name).ToHashSet();

        // When: Its supported operations are inspected.
        string[] requiredOperations =
        [
            "StartHostAsync",
            "JoinAsync",
            "ReissueRoomCodeAsync",
            "SendPositionAsync",
            "ChangeMapAsync",
            "LeaveAsync",
            "EndAsync",
        ];

        // Then: MainWindow can perform every party lifecycle action through one port.
        Assert.All(requiredOperations, operation => Assert.Contains(operation, methods));
        Assert.NotNull(typeof(IPartyCoordinator).GetEvent("StateChanged"));
    }

    [Fact]
    public void ShouldKeepNonPositionDataOutOfPositionProtocol()
    {
        // Given: The only protocol message used to share participant location.
        var propertyNames = typeof(PositionMessage)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // When: Sensitive and unnecessary source fields are checked.
        string[] prohibitedFields =
        [
            "Screenshot",
            "Image",
            "FileName",
            "GameTime",
            "SequenceNumber",
            "Quaternion",
            "WatchDirectory",
            "MonitoringDirectory",
        ];

        // Then: None are representable in the wire position contract.
        Assert.DoesNotContain(prohibitedFields, propertyNames.Contains);
    }

    [Fact]
    public async Task ShouldSwitchConcreteCoordinatorFromHostToParticipant()
    {
        // Given: A facade with deterministic signaling, peer, IDs, and room code.
        var hostId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var clientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ids = new Queue<Guid>([hostId, clientId]);
        var hostSignaling = new FakePartySignaling();
        var clientSignaling = new FakePartySignaling();
        var signaling = new Queue<IPartySignaling>([hostSignaling, clientSignaling]);
        var clientPeer = new FakePartyPeer();
        clientPeer.OnSendAsync = message => message is HelloMessage
            ? clientPeer.ReceiveAsync(new WelcomeMessage(
                clientId,
                "Alice",
                1,
                "Woods",
                [new PartyParticipant(clientId, "Alice", 1)]))
            : Task.CompletedTask;
        var peers = new FakePartyPeerFactory { NextPeer = clientPeer };
        await using var coordinator = new PartyCoordinator(
            () => signaling.Dequeue(),
            peers,
            TimeProvider.System,
            () => ids.Dequeue(),
            () => "ABCDEFGHJKLMNPQ2");

        // When: The application starts hosting, then joins a different room.
        await coordinator.StartHostAsync("Host", "Customs");
        Assert.Equal(PartyCoordinatorRole.Host, coordinator.State.Role);
        await coordinator.JoinAsync("Alice", "ZXCVBNM23456789A");

        // Then: The prior host is stopped and only participant state remains active.
        Assert.Equal(1, hostSignaling.StopCount);
        Assert.Equal(PartyCoordinatorRole.Participant, coordinator.State.Role);
        Assert.Equal("ZXCVBNM23456789A", coordinator.State.RoomCode);
    }

    [Fact]
    public async Task ShouldDisposeSignalingWhenHostConstructionRejectsInput()
    {
        // Given: A signaling resource is created before invalid host input is validated.
        var signaling = new FakePartySignaling();
        await using var coordinator = new PartyCoordinator(
            () => signaling,
            new FakePartyPeerFactory(),
            TimeProvider.System);

        // When: Host construction rejects an empty display name.
        await Assert.ThrowsAsync<ArgumentException>(() => coordinator.StartHostAsync("", "Woods"));

        // Then: The already-created signaling resource is released.
        Assert.Equal(1, signaling.DisposeCount);
    }

    [Fact]
    public async Task ShouldDisposeFailedHostStartAndKeepCoordinatorEmpty()
    {
        // Given: Host signaling fails during startup.
        var signaling = new FakePartySignaling
        {
            StartHost = (_, _) => Task.FromException(new InvalidOperationException("start failed")),
        };
        await using var coordinator = new PartyCoordinator(
            () => signaling,
            new FakePartyPeerFactory(),
            TimeProvider.System);

        // When: Hosting is started.
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartHostAsync("Host", "Woods"));

        // Then: Failed ownership is disposed and no active host state remains.
        Assert.Equal(1, signaling.DisposeCount);
        Assert.Equal(PartyCoordinatorRole.None, coordinator.State.Role);
    }

    [Fact]
    public async Task ShouldSerializeRoomReissueWithHostEnd()
    {
        // Given: A host whose signaling reissue is paused in flight.
        var enteredReissue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReissue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signaling = new FakePartySignaling
        {
            ReissueHostRoom = async (_, _) =>
            {
                enteredReissue.SetResult();
                await releaseReissue.Task;
            },
            Stop = _ =>
            {
                enteredStop.SetResult();
                return Task.CompletedTask;
            },
        };
        await using var coordinator = new PartyCoordinator(
            () => signaling,
            new FakePartyPeerFactory(),
            TimeProvider.System,
            roomCodeFactory: () => "ZXCVBNM23456789A");
        await coordinator.StartHostAsync("Host", "Woods");
        var reissue = coordinator.ReissueRoomCodeAsync();
        await enteredReissue.Task;

        // When: End is requested while reissue owns the coordinator operation.
        var end = coordinator.EndAsync();

        // Then: Stop cannot overtake reissue, and the final state is empty.
        Assert.False(enteredStop.Task.IsCompleted);
        releaseReissue.SetResult();
        await Task.WhenAll(reissue, end);
        Assert.True(enteredStop.Task.IsCompleted);
        Assert.Equal(PartyCoordinatorRole.None, coordinator.State.Role);
    }

    [Fact]
    public async Task ShouldKeepHostWithNoAdvertisedCodeWhenReissueFails()
    {
        // Given: A coordinator hosting an established party whose Worker room switch will fail.
        var signaling = new FakePartySignaling
        {
            ReissueHostRoom = (_, _) => Task.FromException(new PartySignalingException("reissue failed")),
        };
        await using var coordinator = new PartyCoordinator(
            () => signaling,
            new FakePartyPeerFactory(),
            TimeProvider.System,
            roomCodeFactory: () => "ZXCVBNM23456789A");
        await coordinator.StartHostAsync("Host", "Woods");

        // When: Reissuing the join code fails after the old room is retired.
        await Assert.ThrowsAsync<PartySignalingException>(() => coordinator.ReissueRoomCodeAsync());

        // Then: Existing host ownership remains, but callers cannot display a stale join code.
        Assert.Equal(PartyCoordinatorRole.Host, coordinator.State.Role);
        Assert.Null(coordinator.State.RoomCode);
        Assert.Single(coordinator.State.Participants);
    }

    [Fact]
    public async Task ShouldReleaseFailedHostOwnershipWhenEndReportsStopFailure()
    {
        // Given: An active coordinator whose host signaling cannot stop cleanly.
        var signaling = new FakePartySignaling
        {
            Stop = _ => Task.FromException(new InvalidOperationException("stop failed")),
        };
        await using var coordinator = new PartyCoordinator(
            () => signaling,
            new FakePartyPeerFactory(),
            TimeProvider.System);
        await coordinator.StartHostAsync("Host", "Woods");

        // When: Host end reports its signaling failure.
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.EndAsync());

        // Then: Coordinator ownership and signaling resources are still released.
        Assert.Equal(PartyCoordinatorRole.None, coordinator.State.Role);
        Assert.Equal(1, signaling.DisposeCount);
        await coordinator.EndAsync();
        Assert.Equal(1, signaling.StopCount);
    }
}
