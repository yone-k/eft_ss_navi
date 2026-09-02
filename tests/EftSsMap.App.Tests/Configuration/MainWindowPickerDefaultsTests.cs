namespace EftSsMap.App.Tests.Configuration;

public sealed class MainWindowPickerDefaultsTests
{
    [Fact]
    public void ShouldOpenMapPickerInBundledAssetsWithoutCalibrationPicker()
    {
        // Given: The main-window source that opens its image picker.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml.cs"));

        // Then: Map images start in bundled assets and screenshots come only from monitoring.
        Assert.Contains(
            "PickMapImageAsync(this, _pickerDefaultDirectories.BundledMaps)",
            source);
        Assert.DoesNotContain("PickCalibrationScreenshotAsync", source);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsMap.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
