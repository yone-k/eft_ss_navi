namespace EftSsNavi.App.Imaging;

public enum MapImageLoadFailureKind
{
    Missing,
    UnsupportedFormat,
    InvalidImage,
    DecodeFailed,
    AccessDenied,
    IoError,
}
