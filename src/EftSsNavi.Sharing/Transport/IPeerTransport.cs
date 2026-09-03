namespace EftSsNavi.Sharing.Transport;

public interface IPeerTransport : IDisposable
{
    event Action<string>? MessageReceived;

    event Action? Disconnected;

    Task<string> CreateOfferAsync(CancellationToken cancellationToken);

    Task<string> CreateAnswerAsync(string remoteOffer, CancellationToken cancellationToken);

    Task ApplyAnswerAsync(string remoteAnswer, CancellationToken cancellationToken);

    Task WaitUntilConnectedAsync(CancellationToken cancellationToken);

    Task SendAsync(string message, CancellationToken cancellationToken);
}
