namespace EftSsNavi.Sharing.Transport;

internal interface IWebRtcPeerFactory
{
    IWebRtcPeer Create(IReadOnlyList<string> stunServers);
}

internal interface IWebRtcPeer : IDisposable
{
    event Action? IceGatheringCompleted;

    event Action? DataChannelOpened;

    event Action<string>? MessageReceived;

    event Action? Disconnected;

    bool IsDataChannelOpen { get; }

    bool IsDisconnected { get; }

    bool IsIceGatheringComplete { get; }

    string LocalDescriptionSdp { get; }

    Task CreateDataChannelAsync(
        string label,
        bool ordered,
        bool reliable,
        CancellationToken cancellationToken);

    WebRtcSessionDescription CreateOffer();

    WebRtcSessionDescription CreateAnswer();

    Task SetLocalDescriptionAsync(
        WebRtcSessionDescription description,
        CancellationToken cancellationToken);

    Task SetRemoteDescriptionAsync(
        WebRtcSessionDescription description,
        CancellationToken cancellationToken);

    Task SendAsync(string message, CancellationToken cancellationToken);
}

internal sealed record WebRtcSessionDescription(
    WebRtcSessionDescriptionType Type,
    string Sdp);

internal enum WebRtcSessionDescriptionType
{
    Offer,
    Answer,
}
