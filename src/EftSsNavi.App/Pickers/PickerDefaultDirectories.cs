namespace EftSsNavi.App.Pickers;

public sealed class PickerDefaultDirectories
{
    public PickerDefaultDirectories(string applicationBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        BundledMaps = Path.Combine(applicationBaseDirectory, "Assets", "Maps");
    }

    public string BundledMaps { get; }
}
