namespace EftSsNavi.App.Tests.Configuration;

public sealed class PartyDocumentationTests
{
    [Fact]
    public void ShouldDocumentPartyUsagePrivacyAndNetworkLimitations()
    {
        var readme = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "README.md"));

        Assert.Contains("グループで位置を共有する", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("パーティ", readme, StringComparison.Ordinal);
        Assert.Contains("Cloudflare", readme, StringComparison.Ordinal);
        Assert.Contains("STUN", readme, StringComparison.Ordinal);
        Assert.Contains("位置データはシグナリングサーバーを通", readme, StringComparison.Ordinal);
        Assert.Contains("ルームコードを知っている", readme, StringComparison.Ordinal);
        Assert.Contains("IP候補", readme, StringComparison.Ordinal);
        Assert.Contains("接続元IPアドレス", readme, StringComparison.Ordinal);
        Assert.Contains("SignalingWorkerUrl", readme, StringComparison.Ordinal);
        Assert.Contains("シンメトリックNAT", readme, StringComparison.Ordinal);
        Assert.Contains("スクリーンショット画像", readme, StringComparison.Ordinal);
        Assert.Contains("クォータニオン", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("HiveMQ", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("MQTT", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldAttributeOnlyDistributedPartyDependency()
    {
        var notices = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "THIRD-PARTY-NOTICES.md"));

        Assert.Contains("SIPSorcery", notices, StringComparison.Ordinal);
        Assert.Contains("10.0.16", notices, StringComparison.Ordinal);
        Assert.Contains("BSD-3-Clause", notices, StringComparison.Ordinal);
        Assert.DoesNotContain("MQTTnet", notices, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRunDotNetAndWorkerTestsInCi()
    {
        var workflow = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "ci.yml"));

        Assert.Contains("EftSsNavi.Core.Tests.csproj", workflow, StringComparison.Ordinal);
        Assert.Contains("EftSsNavi.Sharing.Tests.csproj", workflow, StringComparison.Ordinal);
        Assert.Contains("EftSsNavi.App.Tests.csproj", workflow, StringComparison.Ordinal);
        Assert.Contains("worker:", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/setup-node@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("node-version: 22", workflow, StringComparison.Ordinal);
        Assert.Contains("cache-dependency-path: workers/signaling/package-lock.json", workflow, StringComparison.Ordinal);
        Assert.Contains("npm ci", workflow, StringComparison.Ordinal);
        Assert.Contains("npm test", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDescribeNetworkAccessAsLimitedToActivePartyUse()
    {
        var readme = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "README.md"));

        Assert.DoesNotContain("ネットワークにはアクセスしません。", readme, StringComparison.Ordinal);
        Assert.Contains("グループに未参加の間はネットワーク通信を行いません", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRequireReadmeInReleaseArtifactValidation()
    {
        var workflow = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), ".github", "workflows", "release.yml"));

        Assert.Contains("publish/README.md", workflow, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsNavi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
