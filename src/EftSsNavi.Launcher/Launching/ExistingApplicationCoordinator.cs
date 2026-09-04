using System.Runtime.InteropServices;

namespace EftSsNavi.Launcher.Launching;

public sealed class ExistingApplicationCoordinator
{
    public const string MutexName = @"Local\EftSsNavi.App.SingleInstance.default";
    public const string WindowTitle = "EFT Screenshot Navi";
    private readonly Func<bool> exists;
    private readonly Action foreground;
    public ExistingApplicationCoordinator() : this(MutexExists, ForegroundWindow) { }
    public ExistingApplicationCoordinator(Func<bool> exists, Action foreground) { this.exists = exists; this.foreground = foreground; }
    public bool TryActivate()
    {
        if (!exists()) return false;
        foreground();
        return true;
    }
    private static bool MutexExists()
    {
        try { if (!Mutex.TryOpenExisting(MutexName, out var mutex)) return false; mutex.Dispose(); return true; }
        catch (UnauthorizedAccessException) { return true; }
    }
    private static void ForegroundWindow()
    {
        var window = FindWindow(null, WindowTitle); if (window == IntPtr.Zero) return;
        _ = ShowWindow(window, 9); _ = SetForegroundWindow(window);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string windowName);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}
