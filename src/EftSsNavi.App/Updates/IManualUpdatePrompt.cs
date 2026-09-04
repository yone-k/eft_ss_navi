namespace EftSsNavi.App.Updates;

public interface IManualUpdatePrompt
{
    Task<UpdatePromptChoice> ShowUpdateAsync(
        UpdateCandidate candidate,
        string currentVersion,
        CancellationToken cancellationToken);

    Task ShowUpToDateAsync(string currentVersion, CancellationToken cancellationToken);

    Task ShowErrorAsync(string message, CancellationToken cancellationToken);
}
