using System.Diagnostics;

namespace EftSsMap.App.Tests.Configuration;

public sealed class ApplicationPublishTests
{
    private static readonly string[] BundledMapFileNames =
    [
        "catalog.json",
        "customs-tarkov-dev.png",
        "factory-tarkov-dev.png",
        "ground-zero-tarkov-dev.png",
        "interchange-ground-tarkov-dev.png",
        "lighthouse-tarkov-dev.png",
        "markers.json",
        "reserve-tarkov-dev.png",
        "shoreline-tarkov-dev.png",
        "streets-of-tarkov-tarkov-dev.png",
        "woods-tarkov-dev.png",
    ];

    private static readonly string[] UnbundledMapFileNames =
    [
        "labyrinth-re3mr.png",
        "terminal-re3mr.jpg",
    ];

    [Fact]
    public async Task ShouldCopyApplicationXamlResourcesToPublishDirectory()
    {
        // Given: A built application and an isolated publish directory.
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "EftSsMap.App",
            "EftSsMap.App.csproj");
        var publishDirectory = Path.Combine(
            Path.GetTempPath(),
            $"eft-ss-map-publish-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publishDirectory);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-t:CopyApplicationXamlResourcesToPublishDirectory");
        startInfo.ArgumentList.Add("-p:Configuration=Release");
        startInfo.ArgumentList.Add("-p:RuntimeIdentifier=win-x64");
        startInfo.ArgumentList.Add($"-p:PublishDir={publishDirectory}{Path.DirectorySeparatorChar}");

        Process? process = null;
        try
        {
            // When: The publish XAML-resource copy target is executed.
            process = Process.Start(startInfo);
            Assert.NotNull(process);
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);

            var diagnosticOutput = string.Join(
                Environment.NewLine,
                await standardOutput,
                await standardError);

            // Then: The target succeeds and emits the application's compiled XAML resources.
            Assert.True(process.ExitCode == 0, diagnosticOutput);
            Assert.True(
                File.Exists(Path.Combine(publishDirectory, "EftSsMap.App.pri")),
                diagnosticOutput);
            Assert.True(
                File.Exists(Path.Combine(publishDirectory, "App.xbf")),
                diagnosticOutput);
            Assert.True(
                File.Exists(Path.Combine(publishDirectory, "MainWindow.xbf")),
                diagnosticOutput);
        }
        finally
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process?.Dispose();
            Directory.Delete(publishDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ShouldPublishBundledMapAssetsAndThirdPartyNotices()
    {
        // Given: The application project and an isolated publish directory.
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "EftSsMap.App",
            "EftSsMap.App.csproj");
        var sourceMapDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "EftSsMap.App",
            "Assets",
            "Maps");
        var publishDirectory = Path.Combine(
            Path.GetTempPath(),
            $"eft-ss-map-assets-{Guid.NewGuid():N}");
        var buildDirectory = Path.Combine(
            Path.GetTempPath(),
            $"eft-ss-map-build-{Guid.NewGuid():N}");

        Assert.All(
            UnbundledMapFileNames,
            fileName => Assert.True(File.Exists(Path.Combine(sourceMapDirectory, fileName))));

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("win-x64");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add($"-p:BaseOutputPath={buildDirectory}{Path.DirectorySeparatorChar}");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(publishDirectory);

        Process? process = null;
        try
        {
            // When: A release artifact is published.
            process = Process.Start(startInfo);
            Assert.NotNull(process);
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            var diagnosticOutput = string.Join(
                Environment.NewLine,
                await standardOutput,
                await standardError);

            // Then: Every exact calibration image and its attribution notice are distributed.
            Assert.True(process.ExitCode == 0, diagnosticOutput);
            var mapDirectory = Path.Combine(publishDirectory, "Assets", "Maps");
            Assert.Equal(
                BundledMapFileNames,
                Directory.GetFiles(mapDirectory)
                    .Select(Path.GetFileName)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            Assert.True(
                File.Exists(Path.Combine(publishDirectory, "THIRD-PARTY-NOTICES.md")),
                diagnosticOutput);
        }
        finally
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process?.Dispose();
            if (Directory.Exists(publishDirectory))
            {
                Directory.Delete(publishDirectory, recursive: true);
            }

            if (Directory.Exists(buildDirectory))
            {
                Directory.Delete(buildDirectory, recursive: true);
            }
        }
    }

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
