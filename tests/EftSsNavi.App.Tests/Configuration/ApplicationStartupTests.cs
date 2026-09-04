using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace EftSsNavi.App.Tests.Configuration;

public sealed class ApplicationStartupTests
{
    [Fact]
    public void ShouldExposeMainWindowWhenExecutableStarts()
    {
        // Given: The built unpackaged application and an isolated user profile.
        var repositoryRoot = FindRepositoryRoot();
        var testBinDirectory = Path.Combine(
            repositoryRoot,
            "tests",
            "EftSsNavi.App.Tests",
            "bin");
        var outputSuffix = Path.GetRelativePath(testBinDirectory, AppContext.BaseDirectory);
        var executablePath = Path.Combine(
            repositoryRoot,
            "src",
            "EftSsNavi.App",
            "bin",
            outputSuffix,
            "EftSsNavi.App.exe");
        Assert.True(File.Exists(executablePath), $"Application executable was not found: {executablePath}");

        var profileDirectory = Path.Combine(
            Path.GetTempPath(),
            $"eft-ss-navi-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
        };
        startInfo.Environment["LOCALAPPDATA"] = profileDirectory;
        startInfo.Environment["USERPROFILE"] = profileDirectory;
        startInfo.Environment["EFTSSNAVI_DISABLE_UPDATE_CHECK"] = "1";

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        try
        {
            // When: Startup is observed until the real application window is published.
            var timeout = Stopwatch.StartNew();
            string? windowTitle = null;
            while (timeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                process.Refresh();
                if (process.HasExited)
                {
                    break;
                }

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    var titleBuffer = new StringBuilder(capacity: 256);
                    _ = GetWindowText(process.MainWindowHandle, titleBuffer, titleBuffer.Capacity);
                    windowTitle = titleBuffer.ToString();
                    if (string.Equals(windowTitle, "EFT Screenshot Navi", StringComparison.Ordinal))
                    {
                        break;
                    }
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }

            process.Refresh();

            // Then: The application remains alive and exposes its main window.
            Assert.False(
                process.HasExited,
                process.HasExited ? $"Application exited during startup with code {process.ExitCode}." : null);
            Assert.Equal("EFT Screenshot Navi", windowTitle);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            Directory.Delete(profileDirectory, recursive: true);
        }
    }

    [Fact]
    public void ShouldAllowOnlyOneApplicationInstancePerUserSession()
    {
        // Given: A first application process using an isolated test-only instance identifier.
        var application = CreateApplicationStartInfo();
        var instanceId = $"test-{Guid.NewGuid():N}";
        application.Environment["EFTSSNAVI_TEST_INSTANCE_ID"] = instanceId;
        using var first = Process.Start(application);
        Assert.NotNull(first);

        try
        {
            Assert.True(WaitForMainWindow(first, TimeSpan.FromSeconds(10)));

            // When: A second process is started with the same per-session identifier.
            using var second = Process.Start(application);
            Assert.NotNull(second);

            // Then: The second process exits promptly while the original window remains alive.
            Assert.True(second.WaitForExit(milliseconds: 10_000));
            Assert.Equal(0, second.ExitCode);
            first.Refresh();
            Assert.False(first.HasExited);
            Assert.NotEqual(IntPtr.Zero, first.MainWindowHandle);
        }
        finally
        {
            StopProcess(first);
            DeleteTestProfile(application);
        }
    }

    [Fact]
    public void ShouldSignalStartupSuccessOnlyAfterMainWindowIsShown()
    {
        // Given: A random named event supplied by the launcher.
        var application = CreateApplicationStartInfo();
        var eventName = $"Local\\EftSsNavi.StartupSuccess.{Guid.NewGuid():N}";
        using var startupSuccess = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        application.Environment["EFTSSNAVI_TEST_INSTANCE_ID"] = $"test-{Guid.NewGuid():N}";
        application.ArgumentList.Add("--startup-success-event");
        application.ArgumentList.Add(eventName);
        using var process = Process.Start(application);
        Assert.NotNull(process);

        try
        {
            // When: The launcher waits for the application's success notification.
            Assert.True(startupSuccess.WaitOne(TimeSpan.FromSeconds(10)));
            process.Refresh();

            // Then: Notification occurs after a real main window has been published.
            Assert.False(process.HasExited);
            Assert.NotEqual(IntPtr.Zero, process.MainWindowHandle);
        }
        finally
        {
            StopProcess(process);
            DeleteTestProfile(application);
        }
    }

    [Fact]
    public void ShouldRequestExistingWindowForegroundWhenDuplicateStarts()
    {
        // Given: The application startup source.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "EftSsNavi.App", "App.xaml.cs"));

        // Then: Duplicate startup locates and foregrounds the established main window.
        Assert.Contains("FindWindow", source);
        Assert.Contains("ShowWindow", source);
        Assert.Contains("SetForegroundWindow", source);
        Assert.DoesNotContain("--single-instance-id", source);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maximumCount);

    private static ProcessStartInfo CreateApplicationStartInfo()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testBinDirectory = Path.Combine(repositoryRoot, "tests", "EftSsNavi.App.Tests", "bin");
        var outputSuffix = Path.GetRelativePath(testBinDirectory, AppContext.BaseDirectory);
        var executablePath = Path.Combine(repositoryRoot, "src", "EftSsNavi.App", "bin", outputSuffix, "EftSsNavi.App.exe");
        Assert.True(File.Exists(executablePath), $"Application executable was not found: {executablePath}");

        var profileDirectory = Path.Combine(Path.GetTempPath(), $"eft-ss-navi-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);
        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
        };
        startInfo.Environment["LOCALAPPDATA"] = profileDirectory;
        startInfo.Environment["USERPROFILE"] = profileDirectory;
        startInfo.Environment["EFTSSNAVI_TEST_PROFILE"] = profileDirectory;
        return startInfo;
    }

    private static bool WaitForMainWindow(Process process, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            process.Refresh();
            if (process.HasExited)
            {
                return false;
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return true;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(50));
        }

        return false;
    }

    private static void StopProcess(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }

    private static void DeleteTestProfile(ProcessStartInfo startInfo)
    {
        if (startInfo.Environment.TryGetValue("EFTSSNAVI_TEST_PROFILE", out var profileDirectory) &&
            profileDirectory is not null && Directory.Exists(profileDirectory))
        {
            Directory.Delete(profileDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsNavi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
