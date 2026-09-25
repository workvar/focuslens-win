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
            // Another instance is running: ask it to show its window, then exit.
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
        ThemeManager.Apply(_services.Settings.Appearance);

        _main = new MainViewModel(_services);
        _window = new MainWindow { DataContext = _main };
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

    /// <summary>Quitting from the tray stops the UI only; the agent keeps tracking until stopped in Settings.</summary>
    private void Quit()
    {
        _quitting = true;
        _services?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
