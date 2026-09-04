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
