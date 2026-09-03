using EftSsNavi.Sharing.Signaling;

namespace EftSsNavi.Sharing.Tests.Signaling;

public sealed class SignalingDefaultsTests
{
    [Fact]
    public void ShouldUseTheDeployedWorkersDevUrl()
    {
        // Given: The signaling URL compiled into release builds after deployment.
        var url = SignalingDefaults.WorkerUrl;

        // When: The default is parsed as an absolute URI.
        var parsed = new Uri(url, UriKind.Absolute);

        // Then: It targets the deployed HTTPS workers.dev service without a path.
        Assert.Equal(Uri.UriSchemeHttps, parsed.Scheme);
        Assert.EndsWith(".workers.dev", parsed.Host, StringComparison.Ordinal);
        Assert.Equal("/", parsed.AbsolutePath);
        Assert.Empty(parsed.Query);
        Assert.Empty(parsed.Fragment);
    }
}
