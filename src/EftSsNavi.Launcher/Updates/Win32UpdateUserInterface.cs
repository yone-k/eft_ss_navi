using System.Runtime.InteropServices;
using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Updates;

public sealed class Win32UpdateUserInterface : IUpdateUserInterface
{
    public UpdateChoice Choose(UpdateCheckMode mode, UpdateCandidate candidate)
    {
        var text = mode == UpdateCheckMode.Automatic
            ? $"新しいバージョン {candidate.DisplayVersion} を利用できます。\n\nはい: 更新する\nいいえ: このバージョンを無視する\nキャンセル: 今はしない"
            : $"新しいバージョン {candidate.DisplayVersion} を利用できます。更新しますか？";
        var result = MessageBox(IntPtr.Zero, text, "EftSsNavi", mode == UpdateCheckMode.Automatic ? 0x23u : 0x24u);
        return result switch { 6 => UpdateChoice.Update, 7 when mode == UpdateCheckMode.Automatic => UpdateChoice.Ignore, 7 => UpdateChoice.Close, _ => UpdateChoice.Later };
    }
    public void Notify(UpdateNotice notice)
    {
        var text = notice switch
        {
            UpdateNotice.UpToDate => "最新バージョンを使用しています。",
            UpdateNotice.FailedVersionOnCooldown => "このバージョンは直前の更新に失敗したため、しばらく更新できません。",
            UpdateNotice.DistributionNotWritable => "更新できません。EftSsNaviをデスクトップなど書き込み可能な場所へ移動してください。",
            UpdateNotice.VersionMismatch => "ランチャーとアプリのバージョンが一致しません。ZIPを新しいフォルダーへ再展開してください。",
            UpdateNotice.RecoveryFailed => "更新を復旧できません。ZIPを新しいフォルダーへ再展開してください。",
            UpdateNotice.ApplicationExitTimedOut => "アプリを終了できなかったため、更新を中止しました。",
            _ => "更新情報を確認できませんでした。",
        };
        _ = MessageBox(IntPtr.Zero, text, "EftSsNavi", 0x40);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
