namespace EftSsNavi.Sharing.Signaling;

public enum SignalingFailureKind
{
    ConnectionFailed,
    Timeout,
    Cancelled,
    NotStarted,
    Rejected,
}

public enum SignalingRejectReason
{
    HostNotFound,
    HostExists,
    Capacity,
    RateLimited,
}

public sealed record SignalingResult(
    bool IsSuccess,
    string? AnswerPayload,
    SignalingFailureKind? FailureKind,
    SignalingRejectReason? RejectReason = null)
{
    public static SignalingResult Success(string? answerPayload = null) =>
        new(true, answerPayload, null, null);

    public static SignalingResult Failure(SignalingFailureKind failureKind) =>
        new(false, null, failureKind, null);

    public static SignalingResult Rejected(SignalingRejectReason reason) =>
        new(false, null, SignalingFailureKind.Rejected, reason);
}

public interface IRoomSignaling : IAsyncDisposable
{
    Task<SignalingResult> ExchangeOfferAsync(
        string roomId,
        Guid participantId,
        string encryptedOffer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        Func<string, bool>? answerValidator = null);

    Task<SignalingResult> StartHostAsync(
        string roomId,
        Func<Guid, string, CancellationToken, Task<string?>> offerHandler,
        CancellationToken cancellationToken = default);

    Task<SignalingResult> ReissueHostRoomAsync(
        string newRoomId,
        CancellationToken cancellationToken = default);

    Task StopHostAsync(CancellationToken cancellationToken = default);
}
