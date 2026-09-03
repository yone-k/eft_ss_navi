using EftSsNavi.Core.Settings;
using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Signaling;
using EftSsNavi.Sharing.Transport;

namespace EftSsNavi.App.Presentation;

internal static class PartyCoordinatorFactory
{
    public static IPartyCoordinator Create(AppSettings settings, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var workerUrl = settings.SignalingWorkerUrl ?? SignalingDefaults.WorkerUrl;
        var stunServers = settings.StunServers.ToArray();

        return new PartyCoordinator(
            signalingFactory: () => CreateSignaling(workerUrl, timeProvider),
            peerFactory: new AppPeerTransportFactory(stunServers),
            timeProvider);
    }

    private static IPartySignaling CreateSignaling(string workerUrl, TimeProvider timeProvider)
    {
        if (!Uri.TryCreate(workerUrl, UriKind.Absolute, out var parsedWorkerUrl))
        {
            throw new PartySignalingException(
                "The signaling Worker URL has not been configured.",
                SignalingFailureKind.ConnectionFailed);
        }

        return new WorkerPartySignaling(new WorkerRoomSignaling(
            parsedWorkerUrl,
            socketFactory: () => new ClientWebSocketSignalingSocket(),
            timeProvider));
    }

    private sealed class AppPeerTransportFactory(IReadOnlyList<string> stunServers) : IPeerTransportFactory
    {
        private readonly IReadOnlyList<string> stunServers = stunServers.ToArray();

        public IPeerTransport Create(Guid participantId) => new WebRtcPeerTransport(stunServers);
    }
}
