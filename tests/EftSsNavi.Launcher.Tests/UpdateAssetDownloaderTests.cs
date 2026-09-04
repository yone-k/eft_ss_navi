using System.Net;
using System.Text;
using EftSsNavi.Launcher.Updates;

namespace EftSsNavi.Launcher.Tests;

public sealed class UpdateAssetDownloaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviDownload", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ShouldDownloadToTransactionDirectoryWithoutLeavingPartialFile()
    {
        using var client = new HttpClient(new Handler());
        var path = await new UpdateAssetDownloader(client).DownloadAsync(new Uri("https://example.test/file.zip"), root);
        Assert.Equal("payload", await File.ReadAllTextAsync(path));
        Assert.False(File.Exists(path + ".partial"));
    }
    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("payload", Encoding.UTF8) });
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
