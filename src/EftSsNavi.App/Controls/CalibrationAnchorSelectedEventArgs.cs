namespace EftSsNavi.App.Controls;

public sealed class CalibrationAnchorSelectedEventArgs(int anchorIndex) : EventArgs
{
    public int AnchorIndex { get; } = anchorIndex;
}
