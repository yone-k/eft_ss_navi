using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EftSsNavi.App.About;

public sealed class WinUiAboutDialog : IAboutDialog
{
    private readonly Func<XamlRoot?> xamlRootProvider;
    private readonly Func<bool> isWindowClosed;

    public WinUiAboutDialog(
        Func<XamlRoot?> xamlRootProvider,
        Func<bool> isWindowClosed)
    {
        ArgumentNullException.ThrowIfNull(xamlRootProvider);
        ArgumentNullException.ThrowIfNull(isWindowClosed);
        this.xamlRootProvider = xamlRootProvider;
        this.isWindowClosed = isWindowClosed;
    }

    public async Task<AboutDialogChoice> ShowAboutAsync(
        AboutInformation information,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(information);
        if (!TryGetXamlRoot(cancellationToken, out var xamlRoot))
        {
            return AboutDialogChoice.Unavailable;
        }

        var content = new StackPanel
        {
            Spacing = 8,
        };
        content.Children.Add(new TextBlock
        {
            Text = information.ApplicationName,
            FontSize = 20,
        });
        content.Children.Add(new TextBlock
        {
            Text = $"バージョン {information.Version}",
        });

        var aboutDialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "バージョン情報",
            Content = content,
            PrimaryButtonText = "GitHub リポジトリ",
            SecondaryButtonText = "第三者ライセンス",
            CloseButtonText = "閉じる",
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            var result = await aboutDialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => AboutDialogChoice.OpenGitHub,
                ContentDialogResult.Secondary => AboutDialogChoice.ShowLicenses,
                _ => AboutDialogChoice.Close,
            };
        }
        catch
        {
            return AboutDialogChoice.Unavailable;
        }
    }

    public async Task ShowLicensesAsync(string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (!TryGetXamlRoot(cancellationToken, out var xamlRoot))
        {
            return;
        }

        var licenseDialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "第三者ライセンス",
            Content = new ScrollViewer
            {
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Enabled,
                Content = new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                },
            },
            CloseButtonText = "閉じる",
            DefaultButton = ContentDialogButton.Close,
        };

        await ShowWithoutPropagatingAsync(licenseDialog);
    }

    public async Task ShowErrorAsync(string message, CancellationToken cancellationToken)
    {
        if (!TryGetXamlRoot(cancellationToken, out var xamlRoot))
        {
            return;
        }

        var errorDialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "バージョン情報エラー",
            Content = message,
            CloseButtonText = "閉じる",
            DefaultButton = ContentDialogButton.Close,
        };

        await ShowWithoutPropagatingAsync(errorDialog);
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

    private static async Task ShowWithoutPropagatingAsync(ContentDialog dialog)
    {
        try
        {
            await dialog.ShowAsync();
        }
        catch
        {
            // The window may be closing or another ContentDialog may have opened.
        }
    }
}
