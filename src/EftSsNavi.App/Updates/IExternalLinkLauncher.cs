namespace EftSsNavi.App.Updates;

public interface IExternalLinkLauncher
{
    bool TryOpen(Uri uri);
}
