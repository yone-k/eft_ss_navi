namespace EftSsNavi.App.Updates;

public sealed class StartupUpdateCoordinator
{
    private const string DownloadErrorMessage =
        "アップデートのダウンロードを開始できませんでした。アプリはそのまま利用できます。";
    private const string SaveErrorMessage =
        "このバージョンの通知設定を保存できませんでした。次回起動時に同じ通知が表示される可能性があります。";

    private readonly IUpdateChecker checker;
    private readonly IUpdatePrompt prompt;
    private readonly IExternalLinkLauncher launcher;
    private readonly IUpdateSuppressionStore suppressionStore;

    public StartupUpdateCoordinator(
        IUpdateChecker checker,
        IUpdatePrompt prompt,
        IExternalLinkLauncher launcher,
        IUpdateSuppressionStore suppressionStore)
    {
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(suppressionStore);

        this.checker = checker;
        this.prompt = prompt;
        this.launcher = launcher;
        this.suppressionStore = suppressionStore;
    }

    public async Task RunAsync(
        bool enabled,
        Version currentVersion,
        string? ignoredVersion,
        CancellationToken cancellationToken = default)
    {
        if (!enabled || currentVersion.Build < 0)
        {
            return;
        }

        try
        {
            var candidate = await checker.CheckAsync(
                currentVersion,
                ignoredVersion,
                cancellationToken);
            if (candidate is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var currentDisplayVersion = $"v{currentVersion.ToString(3)}";
            var choice = await prompt.ShowUpdateAsync(
                candidate,
                currentDisplayVersion,
                cancellationToken);
            switch (choice)
            {
                case UpdatePromptChoice.Update:
                    if (!launcher.TryOpen(candidate.DownloadUri))
                    {
                        await TryShowErrorAsync(DownloadErrorMessage, cancellationToken);
                    }

                    break;

                case UpdatePromptChoice.IgnoreVersion:
                    if (!suppressionStore.TrySave(candidate.NormalizedVersion))
                    {
                        await TryShowErrorAsync(SaveErrorMessage, cancellationToken);
                    }

                    break;

                case UpdatePromptChoice.Unavailable:
                case UpdatePromptChoice.Later:
                default:
                    break;
            }
        }
        catch
        {
            // Update checks must never prevent the application from starting normally.
        }
    }

    private async Task TryShowErrorAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await prompt.ShowErrorAsync(message, cancellationToken);
        }
        catch
        {
            // The window may be closing or another ContentDialog may already be open.
        }
    }
}
