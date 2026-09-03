namespace EftSsNavi.App.Updates;

public interface IUpdatePrompt
{
    Task<UpdatePromptChoice> ShowUpdateAsync(
        UpdateCandidate candidate,
        string currentVersion,
        CancellationToken cancellationToken);

    Task ShowErrorAsync(string message, CancellationToken cancellationToken);
}
