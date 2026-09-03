using EftSsNavi.App.Monitoring;

namespace EftSsNavi.App.Tests.Monitoring;

public sealed class DefaultScreenshotDirectoryProviderTests
{
    [Fact]
    public void ShouldReturnExpectedCandidateOnlyWhenItExists()
    {
        string? checkedPath = null;
        var provider = new DefaultScreenshotDirectoryProvider(
            name => name == "USERPROFILE" ? @"C:\Users\Player" : null,
            path =>
            {
                checkedPath = path;
                return true;
            });

        var result = provider.GetDefaultDirectory();

        var expected = Path.Combine(
            @"C:\Users\Player",
            "Documents",
            "Escape from Tarkov",
            "Screenshots");
        Assert.Equal(expected, result);
        Assert.Equal(expected, checkedPath);
    }

    [Fact]
    public void ShouldReturnNullWithoutCheckingWhenUserProfileIsMissing()
    {
        var directoryChecked = false;
        var provider = new DefaultScreenshotDirectoryProvider(
            _ => null,
            _ =>
            {
                directoryChecked = true;
                return true;
            });

        var result = provider.GetDefaultDirectory();

        Assert.Null(result);
        Assert.False(directoryChecked);
    }

    [Fact]
    public void ShouldReturnNullWhenCandidateDoesNotExist()
    {
        var provider = new DefaultScreenshotDirectoryProvider(
            _ => @"C:\Users\Player",
            _ => false);

        Assert.Null(provider.GetDefaultDirectory());
    }
}
