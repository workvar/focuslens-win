using System.Windows;
using FocusLens.App.Services;
using FocusLens.App.ViewModels;
using FocusLens.App.Views;

namespace FocusLens.App;

public partial class App : Application
{
    private const string SingleInstanceName = @"Local\FocusLens.App.Single";
    private const string ShowEventName = @"Local\FocusLens.App.Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private AppServices? _services;
    private MainViewModel? _main;
    private MainWindow? _window;
    private bool _quitting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, SingleInstanceName, out var isFirst);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        if (!isFirst)
        {
            _showEvent.Set();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            _services?.Log.Error("Unhandled UI exception", args.Exception);
            args.Handled = true;
        };

        _services = new AppServices(meetingId => Dispatcher.Invoke(() => OpenMeeting(meetingId)));
        ThemeManager.Apply(_services.Settings);

        _main = new MainViewModel(_services);
        _window = new MainWindow { DataContext = _main };
        ThemeManager.Attach(_window);
        _main.FocusSurfaces.Install();
        _services.Guide.Start();
        _services.HoldFill.Start();
        _window.StateChanged += (_, _) => { if (_window.WindowState == WindowState.Minimized) _window.Hide(); };
        _window.Closing += (_, args) =>
        {
            if (_quitting) return;
            args.Cancel = true;
            _window.Hide();
        };

        _services.Tray.OpenRequested += ShowWindow;
        _services.Tray.PauseToggled += () => Dispatcher.Invoke(_main.TogglePause);
        _services.Tray.QuitRequested += () => Dispatcher.Invoke(Quit);
        _services.Tray.UpdateRequested += () => _ = Dispatcher.InvokeAsync(HandleUpdateRequestAsync);
        _services.Updates.StateChanged += OnUpdateStateChanged;
        _main.Settings.Updates.RestartRequested += () => _ = Dispatcher.InvokeAsync(ApplyUpdateAsync);
        _services.Updates.Start();

        ListenForShowRequests();
        ShowWindow();

        await _services.RecoverMeetingsAsync();
        _services.MeetingDetection.Start();
        _ = _services.Auth.RestoreAsync();
        if (!_services.Agent.IsRunning() && _services.Settings.OnboardingCompleted) _services.Agent.Start();

        await _main.InitializeAsync();
    }

    private void ShowWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_window is null) return;
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    private void OpenMeeting(string meetingId)
    {
        ShowWindow();
        _ = _main?.ShowMeetingAsync(meetingId);
    }

    private void ListenForShowRequests()
    {
        var handle = _showEvent!;
        _ = Task.Run(() =>
        {
            while (handle.WaitOne()) ShowWindow();
        });
    }

    /// <summary>Keeps the tray menu in step with the updater and tells the user once when a release is out.</summary>
    private void OnUpdateStateChanged(UpdateState state) => _ = Dispatcher.InvokeAsync(() =>
    {
        var tray = _services!.Tray;
        tray.SetUpdateItem(state.Status switch
        {
            UpdateStatus.Available => $"Update available (v{state.Version})",
            UpdateStatus.Downloading => "Downloading update...",
            UpdateStatus.Ready => $"Restart to update (v{state.Version})",
            _ => "Check for updates",
        });

        if (state.Status == UpdateStatus.Available && state.Error is null)
        {
            tray.Notify("Update available", $"FocusLens {state.Version} is out. Click to see what's new.", () => _ = ShowUpdatesAsync());
        }
    });

    private async Task ShowUpdatesAsync()
    {
        ShowWindow();
        await _main!.ShowUpdatesAsync();
    }

    private async Task HandleUpdateRequestAsync()
    {
        var updates = _services!.Updates;
        if (!updates.IsInstalled)
        {
            _services.Tray.Notify("FocusLens", "Updates are only available in the installed app.");
            return;
        }

        switch (updates.State.Status)
        {
            case UpdateStatus.Ready:
                await ApplyUpdateAsync();
                break;
            case UpdateStatus.Available:
            case UpdateStatus.Downloading:
                await ShowUpdatesAsync();
                break;
            default:
                await updates.CheckAsync();
                if (updates.State.Status == UpdateStatus.UpToDate) _services.Tray.Notify("FocusLens", "You are up to date.");
                else if (updates.State.Status == UpdateStatus.Failed) _services.Tray.Notify("FocusLens", "Could not check for updates.");
                break;
        }
    }

    /// <summary>Stops the agent so its files are free, then lets Velopack swap versions and relaunch.</summary>
    private async Task ApplyUpdateAsync()
    {
        _quitting = true;
        await _services!.Agent.StopAsync();
        _services.Updates.ApplyAndRestart();
    }

    /// <summary>Quitting from the tray stops the UI only; the agent keeps tracking until stopped in Settings.</summary>
    private void Quit()
    {
        _quitting = true;
        _services?.Updates.ApplyOnExit();
        _services?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
