using System.Numerics;
using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Presentation;

public sealed record MainViewState(
    PixelPoint? MarkerPosition,
    PixelPoint? MarkerDirection,
    Vector3? WorldPosition,
    string? FileName,
    MainViewStatus Status);
