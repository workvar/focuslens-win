using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services;

namespace FocusLens.App.ViewModels;

/// <summary>First-run flow: what is collected, privacy defaults, optional sign-in, then start tracking.</summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly Action _complete;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsPrivacy), nameof(IsAccount), nameof(IsLastStep))]
    private int _step;

    [ObservableProperty] private bool _startWithWindows = true;

    public bool IsWelcome => Step == 0;
    public bool IsPrivacy => Step == 1;
    public bool IsAccount => Step == 2;
    public bool IsLastStep => Step == 2;
    public bool CanSignIn => _services.Auth.IsConfigured;
    public Services.Auth.SupabaseAuthService Auth => _services.Auth;

    public OnboardingViewModel(AppServices services, Action complete)
    {
        _services = services;
        _complete = complete;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _services.Settings.StartWithWindows = value;
        _services.Settings.Save();
    }

    [RelayCommand]
    private void Next()
    {
        if (Step < 2) Step++;
        else _complete();
    }

    [RelayCommand]
    private void Back()
    {
        if (Step > 0) Step--;
    }

    [RelayCommand]
    private Task SignInAsync() => _services.Auth.SignInWithGoogleAsync();

    [RelayCommand]
    private void Finish() => _complete();
}
