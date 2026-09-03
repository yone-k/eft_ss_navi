using EftSsNavi.App.Pickers;

namespace EftSsNavi.App.Tests.Pickers;

public sealed class PickerDefaultDirectoriesTests
{
    [Fact]
    public void ShouldResolveBundledMapsBelowApplicationDirectory()
    {
        // Given: The application is installed in a known directory.
        var applicationDirectory = Path.Combine("C:", "EftSsNavi");
        var defaults = new PickerDefaultDirectories(applicationDirectory);

        // When: The bundled-map picker directory is requested.
        var result = defaults.BundledMaps;

        // Then: It points at the map assets shipped beside the executable.
        Assert.Equal(
            Path.Combine(applicationDirectory, "Assets", "Maps"),
            result);
    }
}
