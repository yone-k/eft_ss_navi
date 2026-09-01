using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace EftSsMap.App.Tests.Configuration;

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
            "EftSsMap.App.Tests",
            "bin");
        var outputSuffix = Path.GetRelativePath(testBinDirectory, AppContext.BaseDirectory);
        var executablePath = Path.Combine(
            repositoryRoot,
            "src",
            "EftSsMap.App",
            "bin",
            outputSuffix,
            "EftSsMap.App.exe");
        Assert.True(File.Exists(executablePath), $"Application executable was not found: {executablePath}");

        var profileDirectory = Path.Combine(
            Path.GetTempPath(),
            $"eft-ss-map-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
        };
        startInfo.Environment["LOCALAPPDATA"] = profileDirectory;
        startInfo.Environment["USERPROFILE"] = profileDirectory;

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
                    if (string.Equals(windowTitle, "EFT Screenshot Map", StringComparison.Ordinal))
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
            Assert.Equal("EFT Screenshot Map", windowTitle);
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maximumCount);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsMap.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
