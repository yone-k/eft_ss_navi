using System.Numerics;
using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Presentation;

public sealed record MainViewState(
    PixelPoint? MarkerPosition,
    PixelPoint? MarkerDirection,
    Vector3? WorldPosition,
    string? FileName,
    MainViewStatus Status);
