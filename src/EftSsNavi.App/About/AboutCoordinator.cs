using System.Reflection;
using EftSsNavi.App.Updates;
using Microsoft.UI.Xaml;

namespace EftSsNavi.App.About;

public sealed class AboutCoordinator
{
    private readonly IAboutDialog dialog;
    private readonly LicenseNoticeReader licenseReader;
    private readonly IExternalLinkLauncher externalLinkLauncher;
    private readonly Func<Version?> versionProvider;

    public AboutCoordinator(
        IAboutDialog dialog,
        LicenseNoticeReader licenseReader,
        IExternalLinkLauncher externalLinkLauncher,
        Func<Version?> versionProvider)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(licenseReader);
        ArgumentNullException.ThrowIfNull(externalLinkLauncher);
        ArgumentNullException.ThrowIfNull(versionProvider);
        this.dialog = dialog;
        this.licenseReader = licenseReader;
        this.externalLinkLauncher = externalLinkLauncher;
        this.versionProvider = versionProvider;
    }

    public static AboutCoordinator CreateDefault(
        Func<XamlRoot?> xamlRootProvider,
        Func<bool> isWindowClosed)
    {
        return new AboutCoordinator(
            new WinUiAboutDialog(xamlRootProvider, isWindowClosed),
            new LicenseNoticeReader(AppContext.BaseDirectory),
            new ShellExternalLinkLauncher(),
            () => Assembly.GetEntryAssembly()?.GetName().Version);
    }

    public async Task ShowAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var information = AboutInformation.Create(versionProvider());
        var choice = await dialog.ShowAboutAsync(information, cancellationToken);

        if (choice == AboutDialogChoice.OpenGitHub)
        {
            if (!externalLinkLauncher.TryOpen(information.GitHubUri))
            {
                await dialog.ShowErrorAsync(
                    "GitHub リポジトリを開けませんでした。",
                    cancellationToken);
            }

            return;
        }

        if (choice != AboutDialogChoice.ShowLicenses)
        {
            return;
        }

        var license = licenseReader.Read();
        if (!license.IsSuccess || license.Content is null)
        {
            await dialog.ShowErrorAsync(
                license.ErrorMessage ?? "第三者ライセンス文書を読み込めませんでした。",
                cancellationToken);
            return;
        }

        await dialog.ShowLicensesAsync(license.Content, cancellationToken);
    }
}
