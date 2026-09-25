using Velopack;
using FocusLens.App.Services;

namespace FocusLens.App;

/// <summary>
/// Custom entry point so Velopack can handle its install, update and uninstall hooks before any
/// WPF code runs. The agent is stopped around updates and uninstall so its files are never locked.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUpdateFastCallback(_ => StopAgent())
            .OnBeforeUninstallFastCallback(_ =>
            {
                StopAgent();
                new AgentLauncher().SetAutoStart(false);
            })
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static void StopAgent() => new AgentLauncher().StopAsync().GetAwaiter().GetResult();
}
