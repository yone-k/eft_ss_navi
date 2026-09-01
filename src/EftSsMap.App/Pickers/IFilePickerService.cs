using Microsoft.UI.Xaml;

namespace EftSsMap.App.Pickers;

public interface IFilePickerService
{
    Task<PickerResult> PickMapImageAsync(Window owner);

    Task<PickerResult> PickCalibrationScreenshotAsync(Window owner);

    Task<PickerResult> PickFolderAsync(Window owner);
}
