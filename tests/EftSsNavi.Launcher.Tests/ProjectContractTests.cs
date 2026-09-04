using System.Xml.Linq;

namespace EftSsNavi.Launcher.Tests;

public sealed class ProjectContractTests
{
    [Fact]
    public void ShouldPublishFrameworkDependentSingleFileWinExeWithoutUiFrameworks()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../src/EftSsNavi.Launcher/EftSsNavi.Launcher.csproj"));
        var document = XDocument.Load(path);
        string Value(string name) => document.Descendants(name).Single().Value;
        Assert.Equal("WinExe", Value("OutputType")); Assert.Equal("win-x64", Value("RuntimeIdentifier"));
        Assert.Equal("false", Value("SelfContained")); Assert.Equal("true", Value("PublishSingleFile"));
        Assert.DoesNotContain(document.Descendants("PackageReference"), x => (string?)x.Attribute("Include") is { } name && (name.Contains("WindowsAppSDK") || name.Contains("WPF") || name.Contains("WindowsForms")));
    }
}
