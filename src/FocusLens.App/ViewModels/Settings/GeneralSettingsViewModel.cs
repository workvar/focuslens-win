using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services;
using FocusLens.App.Services.Auth;
using FocusLens.Core.Paths;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Appearance, start-at-login, agent control, account and data location.</summary>
public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly AgentLauncher _agent;
    private readonly Action _applyTheme;

    [ObservableProperty] private int _appearanceIndex;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _agentStatusText = "";

    public SupabaseAuthService Auth { get; }
    public IReadOnlyList<string> AppearanceOptions { get; } = new[] { "Match system", "Light", "Dark" };
    public string DataFolder => AppPaths.Root;

    public GeneralSettingsViewModel(AppSettings settings, AgentLauncher agent, SupabaseAuthService auth, Action applyTheme)
    {
        _settings = settings;
        _agent = agent;
        _applyTheme = applyTheme;
        Auth = auth;
        _appearanceIndex = (int)settings.Appearance;
        _startWithWindows = agent.IsAutoStartEnabled();
        RefreshAgentStatus();
    }

    partial void OnAppearanceIndexChanged(int value)
    {
        _settings.Appearance = (AppearanceMode)value;
        _settings.Save();
        _applyTheme();
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
