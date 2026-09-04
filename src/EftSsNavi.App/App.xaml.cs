using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace EftSsNavi.App;

public partial class App : Application
{
    private const string MainWindowTitle = "EFT Screenshot Navi";
    private const string DefaultInstanceId = "default";
    private const int RestoreWindowCommand = 9;

    private Window? _window;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLineArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var instanceId = Environment.GetEnvironmentVariable("EFTSSNAVI_TEST_INSTANCE_ID") ?? DefaultInstanceId;
        var mutexName = $"Local\\EftSsNavi.App.SingleInstance.{NormalizeInstanceId(instanceId)}";
        _singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            BringExistingWindowToForeground();
            Exit();
            return;
        }

        _ownsSingleInstanceMutex = true;
        _window = new MainWindow();
        _window.Closed += OnMainWindowClosed;

        var startupSuccessEventName = GetOptionValue(commandLineArguments, "--startup-success-event");
        if (!string.IsNullOrWhiteSpace(startupSuccessEventName))
        {
            _window.Activated += SignalStartupSuccess;
        }

        _window.Activate();

        void SignalStartupSuccess(object sender, WindowActivatedEventArgs eventArgs)
        {
            if (_window is null || eventArgs.WindowActivationState == WindowActivationState.Deactivated)
            {
                return;
            }

            _window.Activated -= SignalStartupSuccess;
            TrySignalEvent(startupSuccessEventName);
        }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        if (_window is not null)
        {
            _window.Closed -= OnMainWindowClosed;
            _window = null;
        }

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _ownsSingleInstanceMutex = false;
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
    }

    private static string? GetOptionValue(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], option, StringComparison.Ordinal))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private static string NormalizeInstanceId(string instanceId)
    {
        var normalized = new string(instanceId
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            .Take(64)
            .ToArray());
        return string.IsNullOrEmpty(normalized) ? DefaultInstanceId : normalized;
    }

    private static void BringExistingWindowToForeground()
    {
        var windowHandle = FindWindow(null, MainWindowTitle);
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        _ = ShowWindow(windowHandle, RestoreWindowCommand);
        _ = SetForegroundWindow(windowHandle);
    }

    private static void TrySignalEvent(string eventName)
    {
        try
        {
            using var startupSuccessEvent = EventWaitHandle.OpenExisting(eventName);
            startupSuccessEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Invalid or stale launcher events do not prevent normal application startup.
        }
        catch (UnauthorizedAccessException)
        {
            // The event must belong to the current session and caller.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}
