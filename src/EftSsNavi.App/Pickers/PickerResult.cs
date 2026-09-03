namespace EftSsNavi.App.Pickers;

public sealed record PickerResult(string? Path, string? ErrorMessage)
{
    public bool IsSuccess => ErrorMessage is null;

    public bool IsCanceled => IsSuccess && Path is null;

    public static PickerResult Selected(string path) => new(path, null);

    public static PickerResult Canceled() => new(null, null);

    public static PickerResult Failed(string errorMessage) => new(null, errorMessage);
}
