using System.Diagnostics;

namespace EftSsNavi.App.Updates;

public sealed class ShellExternalLinkLauncher : IExternalLinkLauncher
{
    public bool TryOpen(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
