using System.Security.Cryptography;
using System.Text.Json;

namespace EftSsNavi.Sharing.Signaling;

public sealed class WorkerRoomSignaling : IRoomSignaling
{
    private readonly Uri workerUrl;
    private readonly Func<ISignalingSocket> socketFactory;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private ISignalingSocket? hostSocket;
    private CancellationTokenSource? hostReceiveCancellation;
    private Task? hostReceiveTask;
    private Func<Guid, string, CancellationToken, Task<string?>>? offerHandler;
    private int disposed;

    public WorkerRoomSignaling(
        Uri workerUrl,
        Func<ISignalingSocket> socketFactory,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(workerUrl);
        ArgumentNullException.ThrowIfNull(socketFactory);
        if (!workerUrl.IsAbsoluteUri || workerUrl.Scheme != Uri.UriSchemeHttps ||
            workerUrl.AbsolutePath != "/" || !string.IsNullOrEmpty(workerUrl.Query) ||
            !string.IsNullOrEmpty(workerUrl.Fragment))
        {
            throw new ArgumentException("The signaling Worker URL must be an HTTPS base URL.", nameof(workerUrl));
        }

        this.workerUrl = workerUrl;
        this.socketFactory = socketFactory;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SignalingResult> ExchangeOfferAsync(
        string roomId,
        Guid participantId,
        string encryptedOffer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        Func<string, bool>? answerValidator = null)
    {
        ValidateRoomId(roomId);
        ArgumentNullException.ThrowIfNull(encryptedOffer);
        ThrowIfDisposed();
        await using var socket = socketFactory();
        using var timeoutCancellation = new CancellationTokenSource(timeout, timeProvider);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);
        try
        {
            await socket.ConnectAsync(BuildRoomUri(roomId), operationCancellation.Token).ConfigureAwait(false);
            await socket.SendAsync(
                JsonSerializer.Serialize(new { type = "join", participantId = participantId.ToString("N") }),
                operationCancellation.Token).ConfigureAwait(false);
            await socket.SendAsync(
                JsonSerializer.Serialize(new
                {
                    type = "offer",
                    participantId = participantId.ToString("N"),
                    payload = encryptedOffer,
                }),
                operationCancellation.Token).ConfigureAwait(false);

            while (true)
            {
                var message = await socket.ReceiveAsync(operationCancellation.Token).ConfigureAwait(false);
                if (message is null)
                {
                    return SignalingResult.Failure(SignalingFailureKind.ConnectionFailed);
                }

                if (!TryParseMessage(message, out var parsed))
                {
                    continue;
                }

                if (parsed.Type == "answer" && parsed.ParticipantId == participantId && parsed.Payload is not null)
                {
                    if (answerValidator is null || answerValidator(parsed.Payload))
                    {
                        await socket.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                        return SignalingResult.Success(parsed.Payload);
                    }

                    continue;
                }

                if (parsed.Type == "reject" && TryParseRejectReason(parsed.Reason, out var reason))
                {
                    return SignalingResult.Rejected(reason);
                }

                if (parsed.Type == "error")
                {
                    return SignalingResult.Failure(SignalingFailureKind.ConnectionFailed);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SignalingResult.Failure(SignalingFailureKind.Timeout);
        }
        catch (OperationCanceledException)
        {
            return SignalingResult.Failure(SignalingFailureKind.Cancelled);
        }
        catch
        {
            return SignalingResult.Failure(SignalingFailureKind.ConnectionFailed);
        }
        finally
        {
            await TryCloseAsync(socket).ConfigureAwait(false);
        }
    }

    public async Task<SignalingResult> StartHostAsync(
        string roomId,
        Func<Guid, string, CancellationToken, Task<string?>> offerHandler,
        CancellationToken cancellationToken = default)
    {
        ValidateRoomId(roomId);
        ArgumentNullException.ThrowIfNull(offerHandler);
        ThrowIfDisposed();
        await StopHostAsync(CancellationToken.None).ConfigureAwait(false);
        return await RegisterHostAsync(roomId, offerHandler, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SignalingResult> ReissueHostRoomAsync(
        string newRoomId,
        CancellationToken cancellationToken = default)
    {
        ValidateRoomId(newRoomId);
        ThrowIfDisposed();
        var handler = offerHandler;
        if (handler is null)
        {
            return SignalingResult.Failure(SignalingFailureKind.NotStarted);
        }

        await StopHostAsync(CancellationToken.None).ConfigureAwait(false);
        return await RegisterHostAsync(newRoomId, handler, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopHostAsync(CancellationToken cancellationToken = default)
    {
        var cancellation = Interlocked.Exchange(ref hostReceiveCancellation, null);
        var socket = Interlocked.Exchange(ref hostSocket, null);
        var receiveTask = Interlocked.Exchange(ref hostReceiveTask, null);
        offerHandler = null;
        if (cancellation is not null)
        {
            cancellation.Cancel();
        }

        if (socket is not null)
        {
            await TryCloseAsync(socket, cancellationToken).ConfigureAwait(false);
            await socket.DisposeAsync().ConfigureAwait(false);
        }

        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        cancellation?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await StopHostAsync(CancellationToken.None).ConfigureAwait(false);
        sendGate.Dispose();
    }

    private async Task<SignalingResult> RegisterHostAsync(
        string roomId,
        Func<Guid, string, CancellationToken, Task<string?>> handler,
        CancellationToken cancellationToken)
    {
        var socket = socketFactory();
        var token = CreateHostToken();
        try
        {
            await socket.ConnectAsync(BuildRoomUri(roomId), cancellationToken).ConfigureAwait(false);
            await socket.SendAsync(
                JsonSerializer.Serialize(new { type = "host", token }),
                cancellationToken).ConfigureAwait(false);
            var response = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (!TryParseMessage(response, out var parsed))
            {
                await socket.DisposeAsync().ConfigureAwait(false);
                return SignalingResult.Failure(SignalingFailureKind.ConnectionFailed);
            }

            if (parsed.Type == "reject" && TryParseRejectReason(parsed.Reason, out var reason))
            {
                await socket.DisposeAsync().ConfigureAwait(false);
                return SignalingResult.Rejected(reason);
            }

            if (parsed.Type != "host" || parsed.Accepted != true)
            {
                await socket.DisposeAsync().ConfigureAwait(false);
                return SignalingResult.Failure(SignalingFailureKind.ConnectionFailed);
            }

            var receiveCancellation = new CancellationTokenSource();
            offerHandler = handler;
            hostSocket = socket;
            hostReceiveCancellation = receiveCancellation;
            hostReceiveTask = RunHostReceiveLoopAsync(socket, token, handler, receiveCancellation.Token);
            return SignalingResult.Success();
        }
        catch (OperationCanceledException)
        {
            await socket.DisposeAsync().ConfigureAwait(false);
            return SignalingResult.Failure(SignalingFailureKind.Cancelled);
        }
        catch
        {
            await socket.DisposeAsync().ConfigureAwait(false);
            return SignalingResult.Failure(SignalingFailureKind.ConnectionFailed);
        }
    }

    private async Task RunHostReceiveLoopAsync(
        ISignalingSocket socket,
        string token,
        Func<Guid, string, CancellationToken, Task<string?>> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (message is null)
                {
                    return;
                }

                if (!TryParseMessage(message, out var parsed) ||
                    parsed.Type != "offer" || parsed.ParticipantId is not { } participantId ||
                    parsed.Payload is null)
                {
                    continue;
                }

                string? answer;
                try
                {
                    answer = await handler(participantId, parsed.Payload, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // One failed peer negotiation must not stop the host from accepting later offers.
                    continue;
                }

                if (answer is null)
                {
                    continue;
                }

                var answerJson = JsonSerializer.Serialize(new
                {
                    type = "answer",
                    token,
                    participantId = participantId.ToString("N"),
                    payload = answer,
                });
                await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await socket.SendAsync(answerJson, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    sendGate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // A failed host signaling socket stops accepting new peers without affecting WebRTC peers.
        }
    }

    private Uri BuildRoomUri(string roomId)
    {
        var builder = new UriBuilder(workerUrl)
        {
            Scheme = "wss",
            Port = -1,
            Path = $"rooms/{roomId}",
        };
        return builder.Uri;
    }

    private static void ValidateRoomId(string roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        if (roomId.Length != 64 || roomId.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("The room ID must be 64 lowercase hexadecimal characters.", nameof(roomId));
        }
    }

    private static string CreateHostToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static bool TryParseMessage(string? json, out ParsedMessage message)
    {
        message = default;
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            Guid? participantId = null;
            if (root.TryGetProperty("participantId", out var participantElement))
            {
                var participantText = participantElement.GetString();
                if (!Guid.TryParseExact(participantText, "N", out var parsedId) ||
                    participantText != parsedId.ToString("N"))
                {
                    return false;
                }

                participantId = parsedId;
            }

            message = new ParsedMessage(
                typeElement.GetString()!,
                participantId,
                GetOptionalString(root, "payload"),
                GetOptionalString(root, "reason"),
                root.TryGetProperty("accepted", out var accepted) && accepted.ValueKind is JsonValueKind.True
                    ? true
                    : null);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryParseRejectReason(string? value, out SignalingRejectReason reason) =>
        Enum.TryParse(value, ignoreCase: false, out reason) && Enum.IsDefined(reason);

    private static async Task TryCloseAsync(
        ISignalingSocket socket,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await socket.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(
        Volatile.Read(ref disposed) != 0,
        this);

    private readonly record struct ParsedMessage(
        string Type,
        Guid? ParticipantId,
        string? Payload,
        string? Reason,
        bool? Accepted);
}
