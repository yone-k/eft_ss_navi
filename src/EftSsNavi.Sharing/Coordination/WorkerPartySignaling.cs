using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Signaling;

namespace EftSsNavi.Sharing.Coordination;

public sealed class WorkerPartySignaling : IPartySignaling, IAsyncDisposable
{
    private static readonly TimeSpan ExchangeTimeout = TimeSpan.FromSeconds(30);
    private readonly IRoomSignaling roomSignaling;
    private readonly object sync = new();
    private string? hostCode;
    private int disposed;

    public WorkerPartySignaling(IRoomSignaling roomSignaling)
    {
        ArgumentNullException.ThrowIfNull(roomSignaling);
        this.roomSignaling = roomSignaling;
    }

    public async Task StartHostAsync(
        string roomCode,
        Func<Guid, string, CancellationToken, Task<string?>> offerHandler,
        CancellationToken cancellationToken = default)
    {
        ValidateRoomCode(roomCode);
        ArgumentNullException.ThrowIfNull(offerHandler);
        ThrowIfDisposed();
        lock (sync)
        {
            hostCode = roomCode;
        }

        var result = await roomSignaling.StartHostAsync(
            RoomCode.DeriveRoomId(roomCode),
            async (participantId, encryptedOffer, messageCancellationToken) =>
            {
                string? currentCode;
                lock (sync)
                {
                    currentCode = hostCode;
                }

                if (currentCode is null ||
                    !SignalingCipher.TryDecrypt(encryptedOffer, currentCode, participantId, out var offer) ||
                    offer is null)
                {
                    return null;
                }

                var answer = await offerHandler(participantId, offer, messageCancellationToken)
                    .ConfigureAwait(false);
                return answer is null ? null : SignalingCipher.Encrypt(answer, currentCode, participantId);
            },
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            lock (sync)
            {
                hostCode = null;
            }
        }

        EnsureSuccess(result, "Could not start host signaling.", cancellationToken);
    }

    public async Task<string> ExchangeOfferAsync(
        string roomCode,
        Guid participantId,
        string offer,
        CancellationToken cancellationToken = default)
    {
        ValidateRoomCode(roomCode);
        ArgumentNullException.ThrowIfNull(offer);
        ThrowIfDisposed();
        var result = await roomSignaling.ExchangeOfferAsync(
            RoomCode.DeriveRoomId(roomCode),
            participantId,
            SignalingCipher.Encrypt(offer, roomCode, participantId),
            ExchangeTimeout,
            cancellationToken,
            payload => SignalingCipher.TryDecrypt(payload, roomCode, participantId, out _))
            .ConfigureAwait(false);
        EnsureSuccess(result, "Could not exchange the party offer.", cancellationToken);
        if (result.AnswerPayload is null ||
            !SignalingCipher.TryDecrypt(result.AnswerPayload, roomCode, participantId, out var answer) ||
            answer is null)
        {
            throw new PartySignalingException("The party answer could not be authenticated.");
        }

        return answer;
    }

    public async Task ReissueHostRoomAsync(
        string roomCode,
        CancellationToken cancellationToken = default)
    {
        ValidateRoomCode(roomCode);
        ThrowIfDisposed();
        lock (sync)
        {
            hostCode = null;
        }

        var result = await roomSignaling.ReissueHostRoomAsync(
            RoomCode.DeriveRoomId(roomCode),
            cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            lock (sync)
            {
                hostCode = roomCode;
            }
        }

        EnsureSuccess(result, "Could not reissue host signaling.", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (sync)
        {
            hostCode = null;
        }

        return roomSignaling.StopHostAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        lock (sync)
        {
            hostCode = null;
        }

        await roomSignaling.DisposeAsync().ConfigureAwait(false);
    }

    private static void EnsureSuccess(
        SignalingResult result,
        string message,
        CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            return;
        }

        if (result.FailureKind == SignalingFailureKind.Cancelled)
        {
            throw new OperationCanceledException(message, cancellationToken);
        }

        throw new PartySignalingException(
            $"{message} Failure: {result.FailureKind}.",
            result.FailureKind,
            result.RejectReason);
    }

    private static void ValidateRoomCode(string roomCode)
    {
        if (!RoomCode.IsValid(roomCode))
        {
            throw new ArgumentException("The room code is invalid.", nameof(roomCode));
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(
        Volatile.Read(ref disposed) != 0,
        this);
}
