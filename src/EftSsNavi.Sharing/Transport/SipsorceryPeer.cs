using System.Text;
using SIPSorcery.Net;

namespace EftSsNavi.Sharing.Transport;

internal sealed class SipsorceryPeerFactory : IWebRtcPeerFactory
{
    public IWebRtcPeer Create(IReadOnlyList<string> stunServers)
    {
        return new SipsorceryPeer(new RTCPeerConnection(CreateConfiguration(stunServers)));
    }

    internal static RTCConfiguration CreateConfiguration(IReadOnlyList<string> stunServers)
    {
        return new RTCConfiguration
        {
            iceServers = stunServers
                .Select(server => new RTCIceServer { urls = server })
                .ToList(),
        };
    }
}

internal sealed class SipsorceryPeer : IWebRtcPeer
{
    private readonly RTCPeerConnection _peerConnection;
    private readonly SingleResourceOwner<RTCDataChannel> _dataChannel = new(channel => channel.close());
    private int _disconnected;
    private int _disposed;

    public SipsorceryPeer(RTCPeerConnection peerConnection)
    {
        _peerConnection = peerConnection;
        _peerConnection.onicegatheringstatechange += OnIceGatheringStateChanged;
        _peerConnection.onconnectionstatechange += OnConnectionStateChanged;
        _peerConnection.ondatachannel += AttachDataChannel;
    }

    public event Action? IceGatheringCompleted;

    public event Action? DataChannelOpened;

    public event Action<string>? MessageReceived;

    public event Action? Disconnected;

    public bool IsDataChannelOpen => _dataChannel.Current?.IsOpened == true;

    public bool IsDisconnected => Volatile.Read(ref _disconnected) != 0;

    public bool IsIceGatheringComplete =>
        _peerConnection.iceGatheringState == RTCIceGatheringState.complete;

    public string LocalDescriptionSdp =>
        _peerConnection.localDescription?.sdp?.ToString()
        ?? throw new InvalidOperationException("A local WebRTC description has not been set.");

    public async Task CreateDataChannelAsync(
        string label,
        bool ordered,
        bool reliable,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_dataChannel.Current is not null)
        {
            throw new InvalidOperationException("This peer already has a data channel.");
        }

        var channel = await _peerConnection.createDataChannel(
            label,
            CreateDataChannelOptions(ordered, reliable)).WaitAsync(cancellationToken).ConfigureAwait(false);
        AttachDataChannel(channel);
    }

    internal static RTCDataChannelInit CreateDataChannelOptions(bool ordered, bool reliable) =>
        new()
        {
            ordered = ordered,
            maxPacketLifeTime = reliable ? null : (ushort)0,
            maxRetransmits = reliable ? null : (ushort)0,
        };

    public WebRtcSessionDescription CreateOffer()
    {
        var offer = _peerConnection.createOffer(null);
        return new WebRtcSessionDescription(WebRtcSessionDescriptionType.Offer, offer.sdp);
    }

    public WebRtcSessionDescription CreateAnswer()
    {
        var answer = _peerConnection.createAnswer(null);
        return new WebRtcSessionDescription(WebRtcSessionDescriptionType.Answer, answer.sdp);
    }

    public async Task SetLocalDescriptionAsync(
        WebRtcSessionDescription description,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _peerConnection.setLocalDescription(ToSipsDescription(description))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task SetRemoteDescriptionAsync(
        WebRtcSessionDescription description,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _peerConnection.setRemoteDescription(ToSipsDescription(description));
        if (result != SetDescriptionResultEnum.OK)
        {
            throw new InvalidOperationException($"SIPSorcery rejected the remote description: {result}.");
        }

        return Task.CompletedTask;
    }

    public Task SendAsync(string message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        var channel = _dataChannel.Current
            ?? throw new InvalidOperationException("The WebRTC data channel is not available.");
        if (!channel.IsOpened)
        {
            throw new InvalidOperationException("The WebRTC data channel is not open.");
        }

        channel.send(message);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _peerConnection.onicegatheringstatechange -= OnIceGatheringStateChanged;
        _peerConnection.onconnectionstatechange -= OnConnectionStateChanged;
        _peerConnection.ondatachannel -= AttachDataChannel;
        _dataChannel.ReleaseAndSeal(channel =>
        {
            channel.onopen -= OnDataChannelOpened;
            channel.onmessage -= OnDataChannelMessage;
            channel.onclose -= OnDataChannelClosed;
        });
        _peerConnection.Close("Peer transport disposed.");
        _peerConnection.Dispose();
    }

    private static RTCSessionDescriptionInit ToSipsDescription(WebRtcSessionDescription description) =>
        new()
        {
            type = description.Type switch
            {
                WebRtcSessionDescriptionType.Offer => RTCSdpType.offer,
                WebRtcSessionDescriptionType.Answer => RTCSdpType.answer,
                _ => throw new ArgumentOutOfRangeException(nameof(description)),
            },
            sdp = description.Sdp,
        };

    private void AttachDataChannel(RTCDataChannel channel)
    {
        var wasAcquired = _dataChannel.TryAcquire(channel, acquired =>
        {
            acquired.onopen += OnDataChannelOpened;
            acquired.onmessage += OnDataChannelMessage;
            acquired.onclose += OnDataChannelClosed;
        });
        if (wasAcquired && channel.IsOpened)
        {
            OnDataChannelOpened();
        }
    }

    private void OnIceGatheringStateChanged(RTCIceGatheringState state)
    {
        if (state == RTCIceGatheringState.complete)
        {
            IceGatheringCompleted?.Invoke();
        }
    }

    private void OnConnectionStateChanged(RTCPeerConnectionState state)
    {
        if (state is RTCPeerConnectionState.disconnected
            or RTCPeerConnectionState.failed
            or RTCPeerConnectionState.closed)
        {
            Interlocked.Exchange(ref _disconnected, 1);
            Disconnected?.Invoke();
        }
    }

    private void OnDataChannelMessage(
        RTCDataChannel channel,
        DataChannelPayloadProtocols protocol,
        byte[] payload)
    {
        MessageReceived?.Invoke(Encoding.UTF8.GetString(payload));
    }

    private void OnDataChannelOpened() => DataChannelOpened?.Invoke();

    private void OnDataChannelClosed()
    {
        Interlocked.Exchange(ref _disconnected, 1);
        Disconnected?.Invoke();
    }
}
