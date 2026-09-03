using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Controls;

public readonly record struct CalibrationAnchor(
    int Number,
    PixelPoint Position,
    bool WillBeReplaced);
