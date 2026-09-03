using System.Collections.Concurrent;
using EftSsNavi.Sharing.Signaling;

namespace EftSsNavi.Sharing.Tests.Signaling;

public sealed class WorkerRoomSignalingTests
{
    private const string RoomId = "d57a15a6d102fb25e5d39696df650b877d1764fa8c1d76bba5a536a4750b7ba8";
    private static readonly Guid ParticipantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Uri WorkerUrl = new("https://eftssnavi-signaling.example.workers.dev");

    [Fact]
    public async Task ShouldRegisterHostAndRelayOffersWithOwnerToken()
    {
        // Given: A socket that accepts host registration and later supplies one offer.
        var socket = new FakeSignalingSocket();
        socket.EnqueueIncoming("{\"type\":\"host\",\"accepted\":true}");
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => socket);
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // When: Hosting starts and an offer arrives from the Worker.
        var result = await signaling.StartHostAsync(RoomId, (id, payload, _) =>
        {
            Assert.Equal(ParticipantId, id);
            Assert.Equal("encrypted-offer", payload);
            handled.TrySetResult();
            return Task.FromResult<string?>("encrypted-answer");
        });
        socket.EnqueueIncoming($"{{\"type\":\"offer\",\"participantId\":\"{ParticipantId:N}\",\"payload\":\"encrypted-offer\"}}");
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await socket.WaitForSentCountAsync(2);

        // Then: It connects to the room, registers, and authenticates the answer with the same hidden token.
        Assert.True(result.IsSuccess);
        Assert.Equal($"wss://eftssnavi-signaling.example.workers.dev/rooms/{RoomId}", socket.ConnectedUri?.AbsoluteUri);
        using var host = System.Text.Json.JsonDocument.Parse(socket.Sent[0]);
        using var answer = System.Text.Json.JsonDocument.Parse(socket.Sent[1]);
        Assert.Equal("host", host.RootElement.GetProperty("type").GetString());
        Assert.Equal(43, host.RootElement.GetProperty("token").GetString()?.Length);
        Assert.Equal(host.RootElement.GetProperty("token").GetString(), answer.RootElement.GetProperty("token").GetString());
        Assert.Equal("answer", answer.RootElement.GetProperty("type").GetString());
        Assert.Equal("encrypted-answer", answer.RootElement.GetProperty("payload").GetString());
    }

    [Fact]
    public async Task ShouldSendJoinThenOfferAndReturnValidatedAnswer()
    {
        // Given: A socket with an answer for the joining participant.
        var socket = new FakeSignalingSocket();
        socket.EnqueueIncoming($"{{\"type\":\"answer\",\"participantId\":\"{ParticipantId:N}\",\"payload\":\"encrypted-answer\"}}");
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => socket);

        // When: The participant exchanges an encrypted offer.
        var result = await signaling.ExchangeOfferAsync(
            RoomId,
            ParticipantId,
            "encrypted-offer",
            TimeSpan.FromSeconds(1),
            answerValidator: answer => answer == "encrypted-answer");

        // Then: Join precedes offer and only the validated answer succeeds.
        Assert.True(result.IsSuccess);
        Assert.Equal("encrypted-answer", result.AnswerPayload);
        Assert.Equal(2, socket.Sent.Count);
        Assert.Contains("\"type\":\"join\"", socket.Sent[0], StringComparison.Ordinal);
        Assert.Contains("\"type\":\"offer\"", socket.Sent[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnTimeoutAndCloseSocketWhenAnswerDeadlineExpires()
    {
        // Given: A connected participant socket that never receives an answer.
        var timeProvider = new ManualTimeProvider();
        var socket = new FakeSignalingSocket();
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => socket, timeProvider);
        var exchange = signaling.ExchangeOfferAsync(
            RoomId,
            ParticipantId,
            "encrypted-offer",
            TimeSpan.FromSeconds(30));
        await socket.WaitForSentCountAsync(2);

        // When: The application-enforced answer deadline expires.
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        var result = await exchange;

        // Then: Timeout is distinguished from rejection and the socket is closed.
        Assert.Equal(SignalingFailureKind.Timeout, result.FailureKind);
        Assert.True(socket.CloseCount >= 1);
    }

    [Fact]
    public async Task ShouldIgnoreUnvalidatedAnswerUntilDeadline()
    {
        // Given: The Worker sends an answer that fails participant-bound authentication.
        var timeProvider = new ManualTimeProvider();
        var socket = new FakeSignalingSocket();
        socket.EnqueueIncoming($"{{\"type\":\"answer\",\"participantId\":\"{ParticipantId:N}\",\"payload\":\"tampered\"}}");
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => socket, timeProvider);
        var exchange = signaling.ExchangeOfferAsync(
            RoomId,
            ParticipantId,
            "encrypted-offer",
            TimeSpan.FromSeconds(30),
            answerValidator: _ => false);
        await socket.WaitForSentCountAsync(2);

        // When: The full deadline expires without an authentic answer.
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        var result = await exchange;

        // Then: The tampered answer is not surfaced as success.
        Assert.Equal(SignalingFailureKind.Timeout, result.FailureKind);
        Assert.Null(result.AnswerPayload);
    }

    [Theory]
    [InlineData("HostNotFound", SignalingRejectReason.HostNotFound)]
    [InlineData("HostExists", SignalingRejectReason.HostExists)]
    [InlineData("Capacity", SignalingRejectReason.Capacity)]
    [InlineData("RateLimited", SignalingRejectReason.RateLimited)]
    public async Task ShouldMapWorkerRejectReason(string wireReason, SignalingRejectReason expected)
    {
        // Given: A Worker that immediately rejects the participant.
        var socket = new FakeSignalingSocket();
        socket.EnqueueIncoming($"{{\"type\":\"reject\",\"reason\":\"{wireReason}\"}}");
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => socket);

        // When: Offer exchange observes the rejection.
        var result = await signaling.ExchangeOfferAsync(RoomId, ParticipantId, "offer", TimeSpan.FromSeconds(1));

        // Then: The structured rejection is preserved for the UI layer.
        Assert.False(result.IsSuccess);
        Assert.Equal(SignalingFailureKind.Rejected, result.FailureKind);
        Assert.Equal(expected, result.RejectReason);
    }

    [Fact]
    public async Task ShouldCloseOldHostBeforeRegisteringReissuedRoomWithNewToken()
    {
        // Given: Two host sockets and an event log.
        var events = new ConcurrentQueue<string>();
        var first = new FakeSignalingSocket(events, "first");
        var second = new FakeSignalingSocket(events, "second");
        first.EnqueueIncoming("{\"type\":\"host\",\"accepted\":true}");
        second.EnqueueIncoming("{\"type\":\"host\",\"accepted\":true}");
        var sockets = new Queue<ISignalingSocket>([first, second]);
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => sockets.Dequeue());
        await signaling.StartHostAsync(RoomId, (_, _, _) => Task.FromResult<string?>(null));

        // When: The room is reissued.
        var result = await signaling.ReissueHostRoomAsync(new string('a', 64));

        // Then: The old room closes first and the new registration uses a fresh token.
        Assert.True(result.IsSuccess);
        Assert.True(Array.IndexOf(events.ToArray(), "first:close") < Array.IndexOf(events.ToArray(), "second:connect"));
        using var firstHost = System.Text.Json.JsonDocument.Parse(first.Sent.Single());
        using var secondHost = System.Text.Json.JsonDocument.Parse(second.Sent.Single());
        Assert.NotEqual(firstHost.RootElement.GetProperty("token").GetString(), secondHost.RootElement.GetProperty("token").GetString());
    }

    [Fact]
    public async Task ShouldPreserveHostExistsRejectionDuringHostRegistration()
    {
        // Given: The Worker reports that another host already owns the room.
        var socket = new FakeSignalingSocket();
        socket.EnqueueIncoming("{\"type\":\"reject\",\"reason\":\"HostExists\"}");
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => socket);

        // When: Host registration is attempted.
        var result = await signaling.StartHostAsync(RoomId, (_, _, _) => Task.FromResult<string?>(null));

        // Then: The application can distinguish a room collision from connectivity failure.
        Assert.Equal(SignalingFailureKind.Rejected, result.FailureKind);
        Assert.Equal(SignalingRejectReason.HostExists, result.RejectReason);
    }

    [Fact]
    public async Task ShouldContinueHostReceiveLoopAfterOneOfferHandlerFails()
    {
        // Given: A host handler that fails the first peer but can answer the next one.
        var socket = new FakeSignalingSocket();
        socket.EnqueueIncoming("{\"type\":\"host\",\"accepted\":true}");
        var attempts = 0;
        await using var signaling = new WorkerRoomSignaling(WorkerUrl, () => socket);
        await signaling.StartHostAsync(RoomId, (_, _, _) =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<string?>(new InvalidOperationException("peer failed"))
                : Task.FromResult<string?>("second-answer");
        });

        // When: A failed offer is followed by another valid offer.
        socket.EnqueueIncoming($"{{\"type\":\"offer\",\"participantId\":\"{ParticipantId:N}\",\"payload\":\"first\"}}");
        socket.EnqueueIncoming($"{{\"type\":\"offer\",\"participantId\":\"{ParticipantId:N}\",\"payload\":\"second\"}}");
        await socket.WaitForSentCountAsync(2);

        // Then: The second peer still receives its answer.
        Assert.Equal(2, attempts);
        Assert.Contains("second-answer", socket.Sent[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldNotCreateSocketBeforePartyOperation()
    {
        // Given: A Worker signaling instance whose factory records creation.
        var creations = 0;

        // When: The instance is constructed but no party action runs.
        _ = new WorkerRoomSignaling(WorkerUrl, () =>
        {
            creations++;
            return new FakeSignalingSocket();
        });

        // Then: Construction performs no network-resource creation.
        Assert.Equal(0, creations);
    }

    private sealed class FakeSignalingSocket(ConcurrentQueue<string>? events = null, string name = "socket") : ISignalingSocket
    {
        private readonly System.Threading.Channels.Channel<string?> incoming =
            System.Threading.Channels.Channel.CreateUnbounded<string?>();
        private readonly System.Threading.Channels.Channel<int> sentCounts =
            System.Threading.Channels.Channel.CreateUnbounded<int>();

        public Uri? ConnectedUri { get; private set; }
        public List<string> Sent { get; } = [];
        public int CloseCount { get; private set; }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            ConnectedUri = uri;
            events?.Enqueue($"{name}:connect");
            return Task.CompletedTask;
        }

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            sentCounts.Writer.TryWrite(Sent.Count);
            events?.Enqueue($"{name}:send");
            return Task.CompletedTask;
        }

        public async Task<string?> ReceiveAsync(CancellationToken cancellationToken = default) =>
            await incoming.Reader.ReadAsync(cancellationToken);

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCount++;
            events?.Enqueue($"{name}:close");
            incoming.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            incoming.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public void EnqueueIncoming(string message) => incoming.Writer.TryWrite(message);

        public async Task WaitForSentCountAsync(int count)
        {
            while (Sent.Count < count)
            {
                await sentCounts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset utcNow = DateTimeOffset.Parse("2026-09-03T00:00:00Z");

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, utcNow + dueTime);
            timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            utcNow += elapsed;
            foreach (var timer in timers.ToArray())
            {
                timer.FireIfDue(utcNow);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state,
            DateTimeOffset dueAt) : ITimer
        {
            private bool disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

            public void Dispose()
            {
                disposed = true;
                owner.timers.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                if (disposed || now < dueAt)
                {
                    return;
                }

                disposed = true;
                owner.timers.Remove(this);
                callback(state);
            }
        }
    }
}
