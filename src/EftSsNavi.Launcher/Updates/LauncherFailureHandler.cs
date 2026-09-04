using System.Diagnostics;
using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Updates;

public sealed class LauncherFailureHandler
{
    private readonly Action<string> startApplication;
    private readonly IUpdateUserInterface userInterface;

    public LauncherFailureHandler(IUpdateUserInterface userInterface)
        : this(path => Process.Start(new ProcessStartInfo(path)
        {
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = true,
        }), userInterface)
    {
    }

    public LauncherFailureHandler(Action<string> startApplication, IUpdateUserInterface userInterface)
    {
        this.startApplication = startApplication;
        this.userInterface = userInterface;
    }

    public void Handle(UpdateCheckMode mode, string applicationPath)
    {
        if (mode == UpdateCheckMode.Manual)
        {
            userInterface.Notify(UpdateNotice.CheckFailed);
        }
        else if (File.Exists(applicationPath))
        {
            startApplication(applicationPath);
        }
    }
}
