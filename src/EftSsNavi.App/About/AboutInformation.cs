namespace EftSsNavi.App.About;

public sealed record AboutInformation(
    string ApplicationName,
    string Version,
    Uri GitHubUri)
{
    public const string UnknownVersion = "不明";

    public static readonly Uri RepositoryUri = new("https://github.com/yone-k/eft_ss_navi");

    public static AboutInformation Create(Version? version)
    {
        var displayVersion = version is { Build: >= 0 }
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : UnknownVersion;

        return new AboutInformation(
            "EFT Screenshot Navi",
            displayVersion,
            RepositoryUri);
    }
}
