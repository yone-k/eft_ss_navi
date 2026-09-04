namespace EftSsNavi.App.Updates;

public sealed class ManualUpdateCoordinator
{
    private const string CheckErrorMessage =
        "アップデートを確認できませんでした。ネットワーク接続を確認して、もう一度お試しください。";
    private const string VersionErrorMessage =
        "アプリのバージョン情報を取得できないため、アップデートを確認できませんでした。";
    private const string DownloadErrorMessage =
        "アップデートのダウンロードを開始できませんでした。アプリはそのまま利用できます。";

    private readonly IUpdateChecker checker;
    private readonly IManualUpdatePrompt prompt;
    private readonly IExternalLinkLauncher launcher;

    public ManualUpdateCoordinator(
        IUpdateChecker checker,
        IManualUpdatePrompt prompt,
        IExternalLinkLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(launcher);
        this.checker = checker;
        this.prompt = prompt;
        this.launcher = launcher;
    }

    public async Task RunAsync(
        Version? currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (currentVersion is null || currentVersion.Build < 0)
        {
            await prompt.ShowErrorAsync(VersionErrorMessage, cancellationToken);
            return;
        }

        var currentDisplayVersion = $"v{currentVersion.ToString(3)}";
        var result = await checker.CheckAsync(
            currentVersion,
            ignoredVersion: null,
            cancellationToken);
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable when result.Candidate is { } candidate:
                var choice = await prompt.ShowUpdateAsync(
                    candidate,
                    currentDisplayVersion,
                    cancellationToken);
                if (choice == UpdatePromptChoice.Update && !launcher.TryOpen(candidate.DownloadUri))
                {
                    await prompt.ShowErrorAsync(DownloadErrorMessage, cancellationToken);
                }
                break;

            case UpdateCheckStatus.UpToDate:
                await prompt.ShowUpToDateAsync(currentDisplayVersion, cancellationToken);
                break;

            case UpdateCheckStatus.Canceled:
                break;

            case UpdateCheckStatus.Failed:
            case UpdateCheckStatus.Suppressed:
            default:
                await prompt.ShowErrorAsync(CheckErrorMessage, cancellationToken);
                break;
        }
    }
}
