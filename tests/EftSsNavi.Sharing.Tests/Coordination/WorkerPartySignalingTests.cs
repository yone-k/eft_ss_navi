using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Signaling;

namespace EftSsNavi.Sharing.Tests.Coordination;

public sealed class WorkerPartySignalingTests
{
    private const string Code = "ABCDEFGHJKLMNPQ2";
    private static readonly Guid ParticipantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ShouldEncryptOfferAndDecryptAuthenticatedAnswer()
    {
        // Given: A room adapter that returns an encrypted answer.
        var room = new FakeRoomSignaling
        {
            Exchange = (roomId, participantId, encryptedOffer, _, _, validator) =>
            {
                Assert.Equal(RoomCode.DeriveRoomId(Code), roomId);
                Assert.True(SignalingCipher.TryDecrypt(encryptedOffer, Code, participantId, out var offer));
                Assert.Equal("plain-offer", offer);
                var answer = SignalingCipher.Encrypt("plain-answer", Code, participantId);
                Assert.True(validator?.Invoke(answer));
                return Task.FromResult(SignalingResult.Success(answer));
            },
        };
        await using var signaling = new WorkerPartySignaling(room);

        // When: A plain SDP offer is exchanged.
        var answer = await signaling.ExchangeOfferAsync(Code, ParticipantId, "plain-offer");

        // Then: Only the authenticated plaintext answer escapes the wrapper.
        Assert.Equal("plain-answer", answer);
    }

    [Fact]
    public async Task ShouldExposeStructuredWorkerRejection()
    {
        // Given: Worker room signaling rejects because no host exists.
        var room = new FakeRoomSignaling
        {
            Exchange = (_, _, _, _, _, _) =>
                Task.FromResult(SignalingResult.Rejected(SignalingRejectReason.HostNotFound)),
        };
        await using var signaling = new WorkerPartySignaling(room);

        // When: The participant attempts to exchange an offer.
        var error = await Assert.ThrowsAsync<PartySignalingException>(() =>
            signaling.ExchangeOfferAsync(Code, ParticipantId, "offer"));

        // Then: Both failure kind and reject reason remain available to the application.
        Assert.Equal(SignalingFailureKind.Rejected, error.FailureKind);
        Assert.Equal(SignalingRejectReason.HostNotFound, error.RejectReason);
    }

    [Fact]
    public async Task ShouldRejectOldCodeImmediatelyWhenReissueStarts()
    {
        // Given: A host whose Worker room switch is paused.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var room = new FakeRoomSignaling
        {
            Reissue = async (_, _) =>
            {
                entered.SetResult();
                await release.Task;
                return SignalingResult.Success();
            },
        };
        await using var signaling = new WorkerPartySignaling(room);
        await signaling.StartHostAsync(Code, (_, _, _) => Task.FromResult<string?>("answer"));
        var reissue = signaling.ReissueHostRoomAsync("ZXCVBNM23456789A");
        await entered.Task;

        // When: An offer encrypted with the old code arrives during the switch.
        var encrypted = SignalingCipher.Encrypt("old-offer", Code, ParticipantId);
        var answer = await room.DeliverOfferAsync(ParticipantId, encrypted);
        release.SetResult();
        await reissue;

        // Then: No WebRTC answer is created for the retired code.
        Assert.Null(answer);
    }

    private sealed class FakeRoomSignaling : IRoomSignaling
    {
        private Func<Guid, string, CancellationToken, Task<string?>>? handler;

        public Func<string, Guid, string, TimeSpan, CancellationToken, Func<string, bool>?, Task<SignalingResult>>? Exchange { get; init; }
        public Func<string, CancellationToken, Task<SignalingResult>>? Reissue { get; init; }

        public Task<SignalingResult> ExchangeOfferAsync(string roomId, Guid participantId, string encryptedOffer, TimeSpan timeout, CancellationToken cancellationToken = default, Func<string, bool>? answerValidator = null) =>
            Exchange?.Invoke(roomId, participantId, encryptedOffer, timeout, cancellationToken, answerValidator)
            ?? Task.FromResult(SignalingResult.Failure(SignalingFailureKind.Timeout));

        public Task<SignalingResult> StartHostAsync(string roomId, Func<Guid, string, CancellationToken, Task<string?>> offerHandler, CancellationToken cancellationToken = default)
        {
            handler = offerHandler;
            return Task.FromResult(SignalingResult.Success());
        }

        public Task<SignalingResult> ReissueHostRoomAsync(string newRoomId, CancellationToken cancellationToken = default) =>
            Reissue?.Invoke(newRoomId, cancellationToken) ?? Task.FromResult(SignalingResult.Success());

        public Task StopHostAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<string?> DeliverOfferAsync(Guid participantId, string payload) =>
            handler!(participantId, payload, CancellationToken.None);
    }
}
