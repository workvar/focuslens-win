using FocusLens.Core.Permissions;

namespace FocusLens.App.Services.Permissions;

/// <summary>The tracking agent is a separate process; without it nothing is recorded.</summary>
public sealed class AgentPermission : IPermissionCheck
{
    private readonly AgentLauncher _agent;
    public AgentPermission(AgentLauncher agent) => _agent = agent;

    public string Id => "agent";
    public string Name => "Background tracking";
    public string Purpose => "Lets the FocusLens agent record which app and window you are using.";
    public string GrantLabel => "Start agent";

    public Task<PermissionStatus> CheckAsync() =>
        Task.FromResult(!_agent.AgentExists
            ? new PermissionStatus(PermissionState.Unavailable, "FocusLensAgent.exe is missing from the install folder. Reinstall FocusLens.")
            : _agent.IsRunning()
                ? new PermissionStatus(PermissionState.Granted, "Running.")
                : new PermissionStatus(PermissionState.Denied, "The agent is not running."));

    public Task GrantAsync()
    {
        _agent.Start();
        return Task.Delay(600); // give the process a moment to take its mutex before the re-check
    }
}

/// <summary>Start at sign-in keeps tracking and meeting detection going without you opening the app.</summary>
public sealed class StartupPermission : IPermissionCheck
{
    private readonly AgentLauncher _agent;
    private readonly AppSettings _settings;

    public StartupPermission(AgentLauncher agent, AppSettings settings)
    {
        _agent = agent;
        _settings = settings;
    }

    public string Id => "startup";
    public string Name => "Start with Windows";
    public string Purpose => "Starts FocusLens when you sign in so meetings are never missed.";
    public string GrantLabel => "Turn on";

    public Task<PermissionStatus> CheckAsync() =>
        Task.FromResult(_agent.IsAutoStartEnabled()
            ? new PermissionStatus(PermissionState.Granted, "On.")
            : new PermissionStatus(PermissionState.Denied, "Off. FocusLens only tracks while you have started it."));

    public Task GrantAsync()
    {
        _agent.SetAutoStart(true);
        _settings.StartWithWindows = true;
        _settings.Save();
        return Task.CompletedTask;
    }
}
