using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Transport;

namespace EftSsNavi.Sharing.Tests.Coordination;

internal sealed class FakePartySignaling : IPartySignaling, IAsyncDisposable
{
    private Func<Guid, string, CancellationToken, Task<string?>>? offerHandler;

    public Func<string, Guid, string, CancellationToken, Task<string>>? ExchangeOffer { get; set; }

    public Func<string, CancellationToken, Task>? StartHost { get; set; }

    public Func<string, CancellationToken, Task>? ReissueHostRoom { get; set; }

    public Func<CancellationToken, Task>? Stop { get; set; }

    public List<string> ReissuedRoomCodes { get; } = [];

    public int StopCount { get; private set; }

    public int DisposeCount { get; private set; }

    public Task StartHostAsync(
        string roomCode,
        Func<Guid, string, CancellationToken, Task<string?>> onOffer,
        CancellationToken cancellationToken = default)
    {
        offerHandler = onOffer;
        return StartHost?.Invoke(roomCode, cancellationToken) ?? Task.CompletedTask;
    }

    public Task<string> ExchangeOfferAsync(
        string roomCode,
        Guid participantId,
        string offer,
        CancellationToken cancellationToken = default) =>
        ExchangeOffer?.Invoke(roomCode, participantId, offer, cancellationToken)
        ?? Task.FromResult("remote-answer");

    public Task ReissueHostRoomAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        ReissuedRoomCodes.Add(roomCode);
        return ReissueHostRoom?.Invoke(roomCode, cancellationToken) ?? Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCount++;
        return Stop?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    public Task<string?> DeliverOfferAsync(Guid participantId, string offer) =>
        (offerHandler ?? throw new InvalidOperationException("Host signaling is not started."))(
            participantId,
            offer,
            CancellationToken.None);
}

internal sealed class FakePartyPeerFactory : IPeerTransportFactory
{
    public Dictionary<Guid, FakePartyPeer> ByParticipantId { get; } = [];

    public FakePartyPeer? NextPeer { get; set; }

    public List<FakePartyPeer> CreatedPeers { get; } = [];

    public IPeerTransport Create(Guid participantId)
    {
        var peer = NextPeer ?? new FakePartyPeer();
        NextPeer = null;
        ByParticipantId[participantId] = peer;
        CreatedPeers.Add(peer);
        return peer;
    }
}

internal sealed class FakePartyPeer : IPeerTransport
{
    public event Action<string>? MessageReceived;

    public event Action? Disconnected;

    public Func<PartyMessage, Task>? OnSendAsync { get; set; }

    public Func<string, CancellationToken, Task<string>>? OnCreateAnswerAsync { get; set; }

    public List<PartyMessage> SentMessages { get; } = [];

    public int DisposeCount { get; private set; }

    public Task<string> CreateOfferAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult("local-offer");

    public Task<string> CreateAnswerAsync(string offer, CancellationToken cancellationToken = default) =>
        OnCreateAnswerAsync?.Invoke(offer, cancellationToken) ?? Task.FromResult("local-answer");

    public Task ApplyAnswerAsync(string answer, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task WaitUntilConnectedAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task SendAsync(string json, CancellationToken cancellationToken = default)
    {
        Assert.True(ProtocolJson.TryDeserialize(json, out var message));
        Assert.NotNull(message);
        SentMessages.Add(message);
        if (OnSendAsync is not null)
        {
            await OnSendAsync(message);
        }
    }

    public Task ReceiveAsync(PartyMessage message) => ReceiveRawAsync(ProtocolJson.Serialize(message));

    public Task ReceiveRawAsync(string json)
    {
        MessageReceived?.Invoke(json);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        Disconnected?.Invoke();
        return Task.CompletedTask;
    }

    public void ClearSentMessages() => SentMessages.Clear();

    public void Dispose() => DisposeCount++;
}
