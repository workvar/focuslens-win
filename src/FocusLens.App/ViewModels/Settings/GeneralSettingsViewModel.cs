using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services;
using FocusLens.App.Services.Auth;
using FocusLens.Core.Paths;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Start-at-login, agent control, account and data location.</summary>
public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly AgentLauncher _agent;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _agentStatusText = "";

    public SupabaseAuthService Auth { get; }
    public string DataFolder => AppPaths.Root;

    public GeneralSettingsViewModel(AppSettings settings, AgentLauncher agent, SupabaseAuthService auth)
    {
        _settings = settings;
        _agent = agent;
        Auth = auth;
        _startWithWindows = agent.IsAutoStartEnabled();
        RefreshAgentStatus();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _settings.StartWithWindows = value;
        _settings.Save();
        _agent.SetAutoStart(value);
    }

    public void RefreshAgentStatus() => AgentStatusText = _agent.Status() switch
    {
        AgentStatus.Running => "Running",
        AgentStatus.Paused => "Running, tracking paused",
        _ => _agent.AgentExists ? "Stopped" : "Agent not found next to the app",
    };

    [RelayCommand]
    private void StartAgent()
    {
        _agent.Start();
        RefreshAgentStatus();
    }

    [RelayCommand]
    private async Task StopAgentAsync()
    {
        await _agent.StopAsync();
        RefreshAgentStatus();
    }

    [RelayCommand]
    private void OpenDataFolder() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DataFolder) { UseShellExecute = true });

    [RelayCommand]
    private Task SignInAsync() => Auth.SignInWithGoogleAsync();

    [RelayCommand]
    private void SignOut() => Auth.SignOut();
}
