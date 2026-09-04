using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EftSsNavi.Launcher.Updates;

public sealed class UpdateProgressWindow : IDisposable
{
    private const uint CloseMessage = 0x8001;
    private static readonly WindowProcedure Procedure = HandleMessage;
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new(false);
    private IntPtr window;
    private Exception? startupError;
    public bool IsOpen => window != IntPtr.Zero && startupError is null;

    public UpdateProgressWindow()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "EftSsNavi update progress" };
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("The update window did not start.");
        if (startupError is not null) throw new InvalidOperationException("The update window could not be created.", startupError);
    }

    public void Dispose()
    {
        var handle = window;
        if (handle != IntPtr.Zero) _ = PostMessage(handle, CloseMessage, IntPtr.Zero, IntPtr.Zero);
        _ = thread.Join(TimeSpan.FromSeconds(5));
        ready.Dispose();
    }

    private void Run()
    {
        try
        {
            var className = $"EftSsNaviProgress{Guid.NewGuid():N}";
            var windowClass = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = Marshal.GetFunctionPointerForDelegate(Procedure), Instance = GetModuleHandle(null), ClassName = className };
            if (RegisterClassEx(ref windowClass) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            window = CreateWindowEx(0x8, className, "EftSsNavi", 0x00C00000u | 0x10000000u, int.MinValue, int.MinValue, 420, 120, IntPtr.Zero, IntPtr.Zero, windowClass.Instance, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            _ = CreateWindowEx(0, "STATIC", "更新を適用しています。しばらくお待ちください。", 0x50000001u, 12, 28, 380, 32, window, IntPtr.Zero, windowClass.Instance, IntPtr.Zero);
            _ = ShowWindow(window, 5); _ = UpdateWindow(window); ready.Set();
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0) { _ = TranslateMessage(ref message); _ = DispatchMessage(ref message); }
        }
        catch (Exception ex) { startupError = ex; ready.Set(); }
        finally { window = IntPtr.Zero; }
    }

    private static IntPtr HandleMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == 0x10) return IntPtr.Zero;
        if (message == CloseMessage) { _ = DestroyWindow(window); return IntPtr.Zero; }
        if (message == 0x2) { PostQuitMessage(0); return IntPtr.Zero; }
        return DefWindowProc(window, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass { public uint Size; public uint Style; public IntPtr Procedure; public int ClassExtra; public int WindowExtra; public IntPtr Instance; public IntPtr Icon; public IntPtr Cursor; public IntPtr Background; public string? MenuName; public string ClassName; public IntPtr SmallIcon; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Window; public uint Id; public IntPtr WParam; public IntPtr LParam; public uint Time; public int X; public int Y; public uint Private; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, IntPtr window, uint minimum, uint maximum);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr window);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
}
