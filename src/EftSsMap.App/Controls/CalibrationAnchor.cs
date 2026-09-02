using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Controls;

public readonly record struct CalibrationAnchor(
    int Number,
    PixelPoint Position,
    bool WillBeReplaced);
