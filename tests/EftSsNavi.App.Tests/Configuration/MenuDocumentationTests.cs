namespace EftSsNavi.App.Tests.Configuration;

public sealed class MenuDocumentationTests
{
    [Fact]
    public void ShouldDocumentApplicationMenuPaths()
    {
        var readme = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "README.md"));

        Assert.Contains("［ファイル］→［監視先を変更］", readme, StringComparison.Ordinal);
        Assert.Contains("［マップ］→［マップを追加］", readme, StringComparison.Ordinal);
        Assert.Contains("［マップ］→［位置を補正］", readme, StringComparison.Ordinal);
        Assert.Contains("［マップ］→［マップを削除］", readme, StringComparison.Ordinal);
        Assert.Contains("［表示］", readme, StringComparison.Ordinal);
        Assert.Contains("［グループ］→［グループを開く］", readme, StringComparison.Ordinal);
        Assert.Contains("［ヘルプ］→［アップデートを確認］", readme, StringComparison.Ordinal);
        Assert.Contains("［ヘルプ］→［バージョン情報］", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDocumentLauncherAsTheNormalEntryPoint()
    {
        // Given: The user-facing README.
        var readme = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "README.md"));

        // When: The installation and launch instructions are read.
        // Then: Users are directed to the root launcher, not the executable under app.
        Assert.Contains("`EftSsNavi.exe`を起動", readme, StringComparison.Ordinal);
        Assert.Contains("`app/EftSsNavi.App.exe`を直接起動", readme, StringComparison.Ordinal);
        Assert.Contains("自動アップデートは行われません", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDocumentMigrationFromVersionZeroPointTwoPointTwoAndEarlier()
    {
        // Given: The user-facing README.
        var readme = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "README.md"));

        // When: The migration guidance is read.
        // Then: Legacy users are told that a fresh extraction is required.
        Assert.Contains("v0.2.2以前", readme, StringComparison.Ordinal);
        Assert.Contains("新しいZIPを手動でダウンロード", readme, StringComparison.Ordinal);
        Assert.Contains("別のフォルダーへ展開", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDocumentAutomaticAndManualUpdateBehavior()
    {
        // Given: The user-facing README.
        var readme = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "README.md"));

        // When: The update guidance is read.
        // Then: Both launcher startup checks and the menu route are explained.
        Assert.Contains("起動時に自動でアップデートを確認", readme, StringComparison.Ordinal);
        Assert.Contains("［ヘルプ］→［アップデートを確認］", readme, StringComparison.Ordinal);
        Assert.Contains("配布フォルダーに書き込めない", readme, StringComparison.Ordinal);
        Assert.Contains("更新の復旧に失敗", readme, StringComparison.Ordinal);
        Assert.Contains("もう一度展開", readme, StringComparison.Ordinal);
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
