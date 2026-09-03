using System.Xml.Linq;

namespace EftSsNavi.App.Tests.Configuration;

public sealed class ApplicationMapContentTests
{
    private static readonly string[] UnbundledMapPaths =
    [
        @"Assets\Maps\labyrinth-re3mr.png",
        @"Assets\Maps\terminal-re3mr.jpg",
    ];

    [Fact]
    public void ShouldRemoveUnbundledMapsFromSdkDefaultContentItems()
    {
        // Given: The application project consumed by normal builds and map picker defaults.
        var project = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsNavi.App",
            "EftSsNavi.App.csproj"));

        // When: Explicit removals from SDK default content are collected.
        var removed = project.Descendants("Content")
            .Select(element => (string?)element.Attribute("Remove"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Then: Source-only maps cannot leak into the normal output directory.
        Assert.All(UnbundledMapPaths, path => Assert.Contains(path, removed));
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
