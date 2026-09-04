namespace EftSsNavi.App.About;

public interface ILicenseNoticeFileSystem
{
    bool Exists(string path);

    string ReadAllText(string path);
}

public sealed class SystemLicenseNoticeFileSystem : ILicenseNoticeFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);
}

public sealed record LicenseNoticeReadResult(
    bool IsSuccess,
    string? Content,
    string? ErrorMessage)
{
    public static LicenseNoticeReadResult Success(string content) => new(true, content, null);

    public static LicenseNoticeReadResult Failure(string message) => new(false, null, message);
}

public sealed class LicenseNoticeReader
{
    public const string FileName = "THIRD-PARTY-NOTICES.md";

    private readonly string noticePath;
    private readonly ILicenseNoticeFileSystem fileSystem;

    public LicenseNoticeReader(
        string baseDirectory,
        ILicenseNoticeFileSystem? fileSystem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        noticePath = Path.Combine(baseDirectory, FileName);
        this.fileSystem = fileSystem ?? new SystemLicenseNoticeFileSystem();
    }

    public LicenseNoticeReadResult Read()
    {
        try
        {
            if (!fileSystem.Exists(noticePath))
            {
                return LicenseNoticeReadResult.Failure(
                    $"第三者ライセンス文書（{FileName}）が見つかりません。");
            }

            var content = fileSystem.ReadAllText(noticePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return LicenseNoticeReadResult.Failure(
                    "第三者ライセンス文書に表示できる内容がありません。");
            }

            return LicenseNoticeReadResult.Success(content);
        }
        catch
        {
            return LicenseNoticeReadResult.Failure(
                $"第三者ライセンス文書（{FileName}）を読み込めませんでした。");
        }
    }
}
