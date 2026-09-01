namespace EftSsMap.App.Imaging;

public sealed class MapImageLoadResult
{
    private MapImageLoadResult(
        LoadedMapImage? image,
        MapImageLoadFailureKind? failureKind,
        string? errorMessage)
    {
        Image = image;
        FailureKind = failureKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => Image is not null;

    public LoadedMapImage? Image { get; }

    public MapImageLoadFailureKind? FailureKind { get; }

    public string? ErrorMessage { get; }

    internal static MapImageLoadResult Success(LoadedMapImage image) =>
        new(image, null, null);

    internal static MapImageLoadResult Failure(
        MapImageLoadFailureKind failureKind,
        string errorMessage) =>
        new(null, failureKind, errorMessage);
}
