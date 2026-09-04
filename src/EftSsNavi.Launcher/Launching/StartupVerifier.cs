using System.Diagnostics;

namespace EftSsNavi.Launcher.Launching;

public sealed class StartupVerifier
{
    public async Task<bool> StartAndWaitAsync(
        string applicationPath,
        TimeSpan timeout,
        Action<IReadOnlyList<string>> startApplication,
        CancellationToken cancellationToken = default)
    {
        var eventName = $"Local\\EftSsNavi.Startup.{Guid.NewGuid():N}";
        using var started = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        startApplication(["--startup-success-event", eventName]);
        return await Task.Run(() => WaitHandle.WaitAny([started, cancellationToken.WaitHandle], timeout) == 0, CancellationToken.None);
    }

    public Task<bool> StartAndWaitAsync(string applicationPath, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        StartProcessAndWaitAsync(applicationPath, timeout, cancellationToken);

    private static async Task<bool> StartProcessAndWaitAsync(string applicationPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var eventName = $"Local\\EftSsNavi.Startup.{Guid.NewGuid():N}";
        using var started = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        var info = new ProcessStartInfo(applicationPath)
        {
            WorkingDirectory = Path.GetDirectoryName(applicationPath)!,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("--startup-success-event");
        info.ArgumentList.Add(eventName);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("The application could not be started.");
        var signaled = await Task.Run(
            () => WaitHandle.WaitAny([started, cancellationToken.WaitHandle], timeout) == 0,
            CancellationToken.None);
        if (!signaled && !process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }

        return signaled;
    }
}

public sealed class UpdatedLauncherVerifier
{
    public async Task<bool> StartAndWaitAsync(
        string launcherPath,
        TimeSpan timeout,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<int?>> startLauncher,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            return await startLauncher(launcherPath, ["--verify-startup"], timeoutSource.Token) == 0;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return false;
        }
    }

    public Task<bool> StartAndWaitAsync(string launcherPath, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        StartAndWaitAsync(launcherPath, timeout, StartProcessAsync, cancellationToken);

    private static async Task<int?> StartProcessAsync(
        string launcherPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(launcherPath)
        {
            WorkingDirectory = Path.GetDirectoryName(launcherPath)!,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("The updated launcher could not be started.");
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
