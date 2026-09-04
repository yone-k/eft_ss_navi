namespace EftSsNavi.App.About;

public enum AboutDialogChoice
{
    Close,
    OpenGitHub,
    ShowLicenses,
    Unavailable,
}

public interface IAboutDialog
{
    Task<AboutDialogChoice> ShowAboutAsync(
        AboutInformation information,
        CancellationToken cancellationToken);

    Task ShowLicensesAsync(string content, CancellationToken cancellationToken);

    Task ShowErrorAsync(string message, CancellationToken cancellationToken);
}
