using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EftSsNavi.App.Updates;

public sealed class WinUiUpdatePrompt : IUpdatePrompt
{
    private readonly Func<XamlRoot?> xamlRootProvider;
    private readonly Func<bool> isWindowClosed;

    public WinUiUpdatePrompt(Func<XamlRoot?> xamlRootProvider, Func<bool> isWindowClosed)
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
        if (cancellationToken.IsCancellationRequested
            || isWindowClosed()
            || xamlRootProvider() is not { } xamlRoot)
        {
            return UpdatePromptChoice.Unavailable;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "アップデートのお知らせ",
            Content = $"新しいバージョンがあります。\n現在: {currentVersion}\n最新: {candidate.DisplayVersion}",
            PrimaryButtonText = "アップデートする",
            SecondaryButtonText = "このバージョンの通知はもうしない",
            CloseButtonText = "今はしない",
            DefaultButton = ContentDialogButton.Primary,
        };

        try
        {
            var result = await dialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => UpdatePromptChoice.Update,
                ContentDialogResult.Secondary => UpdatePromptChoice.IgnoreVersion,
                _ => UpdatePromptChoice.Later,
            };
        }
        catch
        {
            return UpdatePromptChoice.Unavailable;
        }
    }

    public async Task ShowErrorAsync(string message, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested
            || isWindowClosed()
            || xamlRootProvider() is not { } xamlRoot)
        {
            return;
        }

        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "アップデートエラー",
                Content = message,
                CloseButtonText = "閉じる",
                DefaultButton = ContentDialogButton.Close,
            };
            await dialog.ShowAsync();
        }
        catch
        {
            // The application may be closing or another dialog may be active.
        }
    }
}
