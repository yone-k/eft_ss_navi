namespace EftSsNavi.App.Updates;

public sealed record UpdateCandidate(
    string DisplayVersion,
    string NormalizedVersion,
    Uri DownloadUri);
