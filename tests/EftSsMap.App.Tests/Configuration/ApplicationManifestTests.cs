using System.Xml.Linq;

namespace EftSsMap.App.Tests.Configuration;

public sealed class ApplicationManifestTests
{
    private static readonly XNamespace CompatibilityNamespace =
        "urn:schemas-microsoft-com:compatibility.v1";

    [Fact]
    public void ShouldDeclareWindows10And11CompatibilityContext()
    {
        var document = XDocument.Load(FindRepositoryFile("src", "EftSsMap.App", "app.manifest"));

        var supportedOperatingSystemIds = document
            .Descendants(CompatibilityNamespace + "supportedOS")
            .Select(element => (string?)element.Attribute("Id"));

        Assert.Contains(
            "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}",
            supportedOperatingSystemIds,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EftSsMap.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. segments]);
    }
}
