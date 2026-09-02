using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;

namespace EftSsMap.App.Pickers;

public sealed class FilePickerService : IFilePickerService
{
    private static readonly string[] MapImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private readonly SemaphoreSlim pickerGate = new(1, 1);

    public Task<PickerResult> PickMapImageAsync(Window owner, string? defaultDirectory) =>
        PickFileAsync(owner, MapImageExtensions, defaultDirectory, "マップ画像の選択に失敗しました。");

    public Task<PickerResult> PickFolderAsync(Window owner) =>
        RunPickerAsync(
            async windowId =>
            {
                var picker = new FolderPicker(windowId);
                var result = await picker.PickSingleFolderAsync();
                return result is null ? PickerResult.Canceled() : PickerResult.Selected(result.Path);
            },
            owner,
            "フォルダーの選択に失敗しました。");

    private Task<PickerResult> PickFileAsync(
        Window owner,
        IReadOnlyCollection<string> extensions,
        string? defaultDirectory,
        string failureMessage) =>
        RunPickerAsync(
            async windowId =>
            {
                var picker = new FileOpenPicker(windowId);
                if (!string.IsNullOrWhiteSpace(defaultDirectory))
                {
                    picker.SuggestedFolder = defaultDirectory;
                }

                foreach (var extension in extensions)
                {
                    picker.FileTypeFilter.Add(extension);
                }

                var result = await picker.PickSingleFileAsync();
                return result is null ? PickerResult.Canceled() : PickerResult.Selected(result.Path);
            },
            owner,
            failureMessage);

    private async Task<PickerResult> RunPickerAsync(
        Func<WindowId, Task<PickerResult>> pickAsync,
        Window owner,
        string failureMessage)
    {
        var gateEntered = false;
        try
        {
            await pickerGate.WaitAsync();
            gateEntered = true;

            ArgumentNullException.ThrowIfNull(owner);
            var windowHandle = WindowNative.GetWindowHandle(owner);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            return await pickAsync(windowId);
        }
        catch (Exception exception)
        {
            return PickerResult.Failed($"{failureMessage} {exception.Message}");
        }
        finally
        {
            if (gateEntered)
            {
                pickerGate.Release();
            }
        }
    }
}
