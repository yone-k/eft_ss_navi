using EftSsNavi.App.About;

namespace EftSsNavi.App.Tests.About;

public sealed class LicenseNoticeReaderTests
{
    private const string BaseDirectory = @"C:\app";
    private static readonly string NoticePath = Path.Combine(BaseDirectory, LicenseNoticeReader.FileName);

    [Fact]
    public void ShouldReadNoticeFromSpecifiedApplicationDirectory()
    {
        // Given: A notice file exists beside the application.
        const string content = "# Third-party notices";
        var fileSystem = new FakeLicenseNoticeFileSystem(content);
        var reader = new LicenseNoticeReader(BaseDirectory, fileSystem);

        // When: The notice is read.
        var result = reader.Read();

        // Then: Its full content is returned from the expected path.
        Assert.True(result.IsSuccess);
        Assert.Equal(content, result.Content);
        Assert.Equal(NoticePath, fileSystem.RequestedPath);
    }

    [Fact]
    public void ShouldReturnFailureWhenNoticeIsMissing()
    {
        // Given: The notice file is absent from the application directory.
        var fileSystem = new FakeLicenseNoticeFileSystem(content: null);
        var reader = new LicenseNoticeReader(BaseDirectory, fileSystem);

        // When: The notice is read.
        var result = reader.Read();

        // Then: A user-presentable failure is returned without content.
        Assert.False(result.IsSuccess);
        Assert.Null(result.Content);
        Assert.Contains("見つかりません", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldReturnFailureWhenNoticeCannotBeRead()
    {
        // Given: The notice exists but the file system rejects the read.
        var fileSystem = new FakeLicenseNoticeFileSystem(new UnauthorizedAccessException("denied"));
        var reader = new LicenseNoticeReader(BaseDirectory, fileSystem);

        // When: The notice is read.
        var result = reader.Read();

        // Then: The exception is converted to a user-presentable failure.
        Assert.False(result.IsSuccess);
        Assert.Null(result.Content);
        Assert.Contains("読み込めません", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldReturnFailureInsteadOfEmptyContentWhenNoticeIsBlank()
    {
        // Given: The notice exists but contains no displayable text.
        var fileSystem = new FakeLicenseNoticeFileSystem("   ");
        var reader = new LicenseNoticeReader(BaseDirectory, fileSystem);

        // When: The notice is read.
        var result = reader.Read();

        // Then: The caller cannot open an empty license dialog.
        Assert.False(result.IsSuccess);
        Assert.Null(result.Content);
    }

    private sealed class FakeLicenseNoticeFileSystem : ILicenseNoticeFileSystem
    {
        private readonly string? content;
        private readonly Exception? exception;

        public FakeLicenseNoticeFileSystem(string? content)
        {
            this.content = content;
        }

        public FakeLicenseNoticeFileSystem(Exception exception)
        {
            this.exception = exception;
            content = string.Empty;
        }

        public string? RequestedPath { get; private set; }

        public bool Exists(string path)
        {
            RequestedPath = path;
            return content is not null || exception is not null;
        }

        public string ReadAllText(string path)
        {
            RequestedPath = path;
            if (exception is not null)
            {
                throw exception;
            }

            return content!;
        }
    }
}
