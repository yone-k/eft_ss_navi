using System.Diagnostics;
using System.Reflection;
using EftSsNavi.Launcher.Launching;
using EftSsNavi.Launcher.State;
using EftSsNavi.Launcher.Transactions;
using EftSsNavi.Launcher.Updates;

namespace EftSsNavi.Launcher;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var options = LaunchArguments.Parse(args);
            if (options.Mode == LaunchMode.Cleanup) return await CleanupAsync(options);
            if (options.Mode == LaunchMode.ApplyUpdate) return await ApplyUpdateAsync(options);
            if (options.Mode == LaunchMode.VerifyStartup) return await VerifyStartupAsync();
            return await RunCheckAndLaunchAsync(options);
        }
        catch (ArgumentException) { return 64; }
        catch { return 1; }
    }

    private static async Task<int> RunCheckAndLaunchAsync(LaunchArguments options)
    {
        try
        {
            return await RunCheckAndLaunchCoreAsync(options);
        }
        catch (Exception exception) when (exception is not ArgumentException)
        {
            var root = AppContext.BaseDirectory;
            var appPath = Path.Combine(root, "app", "EftSsNavi.App.exe");
            var mode = options.Mode == LaunchMode.Manual ? UpdateCheckMode.Manual : UpdateCheckMode.Automatic;
            new LauncherFailureHandler(new Win32UpdateUserInterface()).Handle(mode, appPath);
            return 0;
        }
    }

    private static async Task<int> RunCheckAndLaunchCoreAsync(LaunchArguments options)
    {
        var manual = options.Mode == LaunchMode.Manual;
        var root = AppContext.BaseDirectory;
        if (manual && !new ManualLaunchValidator().IsValid(options, root)) return 64;
        if (!manual && new ExistingApplicationCoordinator().TryActivate()) return 0;
        var appPath = Path.Combine(root, "app", "EftSsNavi.App.exe");
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EftSsNavi");
        var store = new LauncherStateStore(Path.Combine(local, "launcher.json"));
        var cleanup = new CleanupManager(Path.Combine(local, "updates"), store);
        await cleanup.RetryPendingAsync();
        var pendingRecovery = new PendingUpdateRecovery(Path.Combine(local, "updates"), root, cleanup);
        if (await pendingRecovery.HasPendingAsync())
        {
            if (manual) { new Win32UpdateUserInterface().Notify(UpdateNotice.CheckFailed); return 0; }
            if (!await pendingRecovery.RecoverAsync())
            {
                new Win32UpdateUserInterface().Notify(UpdateNotice.RecoveryFailed);
                if (File.Exists(appPath)) Process.Start(new ProcessStartInfo(appPath) { WorkingDirectory = Path.GetDirectoryName(appPath)!, UseShellExecute = true });
                return 0;
            }
        }
        if (!new DistributionInspector().VersionsMatch(root))
        {
            new Win32UpdateUserInterface().Notify(UpdateNotice.VersionMismatch);
            if (!manual && File.Exists(appPath)) Process.Start(new ProcessStartInfo(appPath) { WorkingDirectory = Path.GetDirectoryName(appPath)!, UseShellExecute = true });
            return 0;
        }
        var disabled = string.Equals(Environment.GetEnvironmentVariable("EFTSSNAVI_DISABLE_UPDATE_CHECK"), "1", StringComparison.Ordinal);
#if DEBUG
        disabled = true;
#endif
        LauncherRunResult result = new(manual ? LauncherAction.KeepApplication : LauncherAction.StartApplication);
        if (!disabled)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            var coordinator = new LauncherCoordinator(new UpdateCheckService(client), store, new Win32UpdateUserInterface(), () => DateTimeOffset.UtcNow);
            result = await coordinator.RunAsync(manual ? UpdateCheckMode.Manual : UpdateCheckMode.Automatic, version);
        }
        var handedOff = false;
        if (result.Action == LauncherAction.ApplyUpdate && result.Candidate is { } candidate)
        {
            if (!new DistributionInspector().CanWrite(root))
            {
                new Win32UpdateUserInterface().Notify(UpdateNotice.DistributionNotWritable);
                result = new(manual ? LauncherAction.KeepApplication : LauncherAction.StartApplication);
            }
            else
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                var transaction = Path.Combine(local, "updates", Guid.NewGuid().ToString("N"));
                try
                {
                    var zip = await new UpdateAssetDownloader(client).DownloadAsync(candidate.DownloadUri, transaction);
                    var stage = await new UpdatePackageStager().StageAsync(zip, candidate.Sha256, Path.Combine(transaction, "staging"), candidate.NormalizedVersion);
                    if (stage.Status != PackageStageStatus.Succeeded) new Win32UpdateUserInterface().Notify(UpdateNotice.CheckFailed);
                    else
                    {
                        var callerPid = manual ? options.CallerPid!.Value : Environment.ProcessId;
                        var callerSessionId = manual ? options.CallerSessionId!.Value : Process.GetCurrentProcess().SessionId;
                        var callerPath = manual ? options.CallerPath! : Environment.ProcessPath!;
                        new UpdateHandoff().Start(root, transaction, candidate.NormalizedVersion, callerPid, callerSessionId, callerPath, manual ? options.EventName : null);
                        handedOff = true;
                    }
                }
                catch { new Win32UpdateUserInterface().Notify(UpdateNotice.CheckFailed); }
            }
        }
        if (!handedOff && !manual && File.Exists(appPath)) Process.Start(new ProcessStartInfo(appPath) { WorkingDirectory = Path.GetDirectoryName(appPath)!, UseShellExecute = true });
        return 0;
    }

    private static async Task<int> ApplyUpdateAsync(LaunchArguments options)
    {
        using var progress = new UpdateProgressWindow();
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EftSsNavi");
        var updates = Path.Combine(local, "updates");
        var transaction = Path.GetFullPath(options.TransactionDirectory!);
        if (!new ApplyLaunchValidator().IsValid(options, updates)) return 64;
        try
        {
            using var handoffReady = EventWaitHandle.OpenExisting(options.HandoffEventName!);
            handoffReady.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return 64;
        }
        if (options.EventName is { Length: > 0 })
        {
            try
            {
                using var shutdown = EventWaitHandle.OpenExisting(options.EventName);
                shutdown.Set();
            }
            catch (WaitHandleCannotBeOpenedException) { return 64; }
        }
        var distribution = Path.GetFullPath(options.DistributionRoot!);
        try
        {
            using var caller = Process.GetProcessById(options.CallerPid!.Value);
            if (!caller.WaitForExit(30_000))
            {
                new Win32UpdateUserInterface().Notify(UpdateNotice.ApplicationExitTimedOut);
                await StartCleanupAsync(distribution, transaction, updates);
                return 4;
            }
        }
        catch (ArgumentException) { }
        var staging = Path.Combine(transaction, "staging");
        var journal = Path.Combine(transaction, "journal.json");
        var update = await new UpdateTransaction().ApplyAsync(
            options.TargetVersion!, distribution, staging, journal,
            _ => new UpdatedLauncherVerifier().StartAndWaitAsync(Path.Combine(distribution, "EftSsNavi.exe"), TimeSpan.FromSeconds(30)));
        if (!update.Succeeded)
        {
            var store = new LauncherStateStore(Path.Combine(local, "launcher.json"));
            await new FailedUpdateHandler(store).HandleAsync(options.TargetVersion!, distribution);
        }
        await StartCleanupAsync(distribution, transaction, updates);
        return update.Succeeded ? 0 : 5;
    }

    private static async Task<int> VerifyStartupAsync()
    {
        var appPath = Path.Combine(AppContext.BaseDirectory, "app", "EftSsNavi.App.exe");
        if (!File.Exists(appPath)) return 5;
        return await new StartupVerifier().StartAndWaitAsync(appPath, TimeSpan.FromSeconds(30)) ? 0 : 5;
    }

    private static async Task StartCleanupAsync(string distribution, string transaction, string updatesRoot)
    {
        var executable = Path.Combine(distribution, "EftSsNavi.exe");
        var store = new LauncherStateStore(Path.Combine(Path.GetDirectoryName(updatesRoot)!, "launcher.json"));
        try
        {
            if (!File.Exists(executable))
            {
                await new CleanupManager(updatesRoot, store).DeferAsync(transaction);
                return;
            }

            var readyEventName = $"Local\\EftSsNavi.Cleanup.{Guid.NewGuid():N}";
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, readyEventName);
            using var current = Process.GetCurrentProcess();
            var info = new ProcessStartInfo(executable) { WorkingDirectory = distribution, UseShellExecute = false };
            foreach (var argument in new[]
            {
                "--cleanup", "--caller-pid", Environment.ProcessId.ToString(),
                "--caller-session-id", current.SessionId.ToString(),
                "--caller-path", Environment.ProcessPath!,
                "--handoff-ready-event", readyEventName,
                "--transaction-dir", transaction,
            }) info.ArgumentList.Add(argument);
            using var cleanupProcess = Process.Start(info);
            if (cleanupProcess is null || !ready.WaitOne(TimeSpan.FromSeconds(10)))
            {
                await new CleanupManager(updatesRoot, store).DeferAsync(transaction);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            await new CleanupManager(updatesRoot, store).DeferAsync(transaction);
        }
    }

    private static async Task<int> CleanupAsync(LaunchArguments options)
    {
        var updates = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EftSsNavi", "updates");
        var target = Path.GetFullPath(options.TransactionDirectory!);
        if (!new CleanupLaunchValidator().IsValid(options, updates)) return 64;
        try
        {
            using var ready = EventWaitHandle.OpenExisting(options.HandoffEventName!);
            ready.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return 64;
        }
        var store = new LauncherStateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EftSsNavi", "launcher.json"));
        var cleanup = new CleanupManager(updates, store);
        try
        {
            using var process = Process.GetProcessById(options.CallerPid!.Value);
            if (!process.WaitForExit(30_000)) { await cleanup.DeferAsync(target); return 1; }
        }
        catch (ArgumentException) { }
        return await cleanup.TryCleanupAsync(target) ? 0 : 1;
    }

    private static bool IsChildPath(string path, string root) => path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

}
