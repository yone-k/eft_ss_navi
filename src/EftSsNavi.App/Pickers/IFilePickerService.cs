using Microsoft.UI.Xaml;

namespace EftSsNavi.App.Pickers;

public interface IFilePickerService
{
    Task<PickerResult> PickMapImageAsync(Window owner, string? defaultDirectory);

    Task<PickerResult> PickFolderAsync(Window owner);
}
