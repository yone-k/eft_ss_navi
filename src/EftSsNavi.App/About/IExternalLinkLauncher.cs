namespace EftSsNavi.App.About;

public interface IExternalLinkLauncher
{
    bool TryOpen(Uri uri);
}
