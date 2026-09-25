using System.Windows;

namespace FocusLens.App.ViewModels;

/// <summary>Marshals work onto the WPF dispatcher from background threads.</summary>
public static class UiThread
{
    public static void Post(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
