using System.Windows;
using FocusLens.App.Views;

namespace FocusLens.App.Services.Dialogs;

/// <summary>Single entry point for "are you sure?" prompts, so every destructive action asks the same way.</summary>
public static class ConfirmDialog
{
    /// <summary>Returns true only when the user explicitly confirms. Must be called on the UI thread.</summary>
    public static bool Ask(string title, string message, string confirmText = "Delete", bool destructive = true)
    {
        var owner = Application.Current?.MainWindow;
        var dialog = new ConfirmDialogWindow(title, message, confirmText, destructive)
        {
            Owner = owner is { IsVisible: true } ? owner : null,
        };
        if (dialog.Owner is null) dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }

    public static bool AskDeleteChat(string? title) => Ask(
        "Delete this chat?",
        $"\"{Trim(title)}\" and all of its messages will be permanently deleted. This cannot be undone.");

    public static bool AskDeleteMeeting(string? title) => Ask(
        "Delete this meeting?",
        $"\"{Trim(title)}\", including its transcript, notes and action items, will be permanently deleted. This cannot be undone.");

    public static bool AskDeleteRecording(string? title) => Ask(
        "Delete this recording?",
        $"The saved audio for \"{Trim(title)}\" will be permanently deleted. The transcript and notes are kept. This cannot be undone.");

    private static string Trim(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Length > 60 ? title[..57] + "..." : title;
}
