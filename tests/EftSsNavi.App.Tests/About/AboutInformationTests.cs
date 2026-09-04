using EftSsNavi.App.About;

namespace EftSsNavi.App.Tests.About;

public sealed class AboutInformationTests
{
    [Fact]
    public void ShouldFormatOnlyThreeVersionElementsWhenRevisionExists()
    {
        // Given: A four-element assembly version.
        var version = new Version(1, 2, 3, 4);

        // When: Display information is created.
        var information = AboutInformation.Create(version);

        // Then: The revision is omitted from the displayed version.
        Assert.Equal("1.2.3", information.Version);
    }

    [Fact]
    public void ShouldUseUnknownVersionWhenVersionCannotBeObtained()
    {
        // Given: No entry assembly version.
        Version? version = null;

        // When: Display information is created.
        var information = AboutInformation.Create(version);

        // Then: A stable Japanese fallback is displayed.
        Assert.Equal("不明", information.Version);
    }

    [Fact]
    public void ShouldUseUnknownVersionWhenVersionCannotBeNormalizedToThreeElements()
    {
        // Given: A version without a build element.
        var version = new Version(1, 2);

        // When: Display information is created.
        var information = AboutInformation.Create(version);

        // Then: The incomplete version is not presented as an application version.
        Assert.Equal("不明", information.Version);
    }

    [Fact]
    public void ShouldExposeApplicationNameAndFixedHttpsRepository()
    {
        // Given: A valid application version.
        var version = new Version(1, 2, 3);

        // When: Display information is created.
        var information = AboutInformation.Create(version);

        // Then: The public About contract contains the fixed identity and repository.
        Assert.Equal("EFT Screenshot Navi", information.ApplicationName);
        Assert.Equal(new Uri("https://github.com/yone-k/eft_ss_navi"), information.GitHubUri);
        Assert.Equal(Uri.UriSchemeHttps, information.GitHubUri.Scheme);
    }
}
