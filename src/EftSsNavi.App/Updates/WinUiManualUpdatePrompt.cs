using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EftSsNavi.App.Updates;

public sealed class WinUiManualUpdatePrompt : IManualUpdatePrompt
{
    private readonly Func<XamlRoot?> xamlRootProvider;
    private readonly Func<bool> isWindowClosed;

    public WinUiManualUpdatePrompt(
        Func<XamlRoot?> xamlRootProvider,
        Func<bool> isWindowClosed)
    {
        ArgumentNullException.ThrowIfNull(xamlRootProvider);
        ArgumentNullException.ThrowIfNull(isWindowClosed);
        this.xamlRootProvider = xamlRootProvider;
        this.isWindowClosed = isWindowClosed;
    }

    public async Task<UpdatePromptChoice> ShowUpdateAsync(
        UpdateCandidate candidate,
        string currentVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetXamlRoot(cancellationToken, out var xamlRoot))
        {
            return UpdatePromptChoice.Unavailable;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "アップデートのお知らせ",
            Content = $"新しいバージョンがあります。\n現在: {currentVersion}\n最新: {candidate.DisplayVersion}",
            PrimaryButtonText = "アップデートする",
            CloseButtonText = "閉じる",
            DefaultButton = ContentDialogButton.Primary,
        };

        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary
                ? UpdatePromptChoice.Update
                : UpdatePromptChoice.Later;
        }
        catch
        {
            return UpdatePromptChoice.Unavailable;
        }
    }

    public Task ShowUpToDateAsync(string currentVersion, CancellationToken cancellationToken) =>
        ShowMessageAsync(
            "アップデートを確認",
            $"現在のバージョン {currentVersion} は最新です。",
            cancellationToken);

    public Task ShowErrorAsync(string message, CancellationToken cancellationToken) =>
        ShowMessageAsync("アップデートエラー", message, cancellationToken);

    private async Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        if (!TryGetXamlRoot(cancellationToken, out var xamlRoot))
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "閉じる",
            DefaultButton = ContentDialogButton.Close,
        };
        try
        {
            await dialog.ShowAsync();
        }
        catch
        {
            // The window may be closing or another ContentDialog may already be open.
        }
    }

    private bool TryGetXamlRoot(
        CancellationToken cancellationToken,
        out XamlRoot xamlRoot)
    {
        if (!cancellationToken.IsCancellationRequested
            && !isWindowClosed()
            && xamlRootProvider() is { } availableRoot)
        {
            xamlRoot = availableRoot;
            return true;
        }

        xamlRoot = null!;
        return false;
    }
}
