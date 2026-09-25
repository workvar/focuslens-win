using FocusLens.Core.Settings;

namespace FocusLens.App.ViewModels.Settings;

public sealed record TrackingCatalogEntry(TrackingItem Item, string Title, string Detail);

public sealed record TrackingCatalogSection(string Title, string Footer, IReadOnlyList<TrackingCatalogEntry> Entries);

/// <summary>Plain-language description of every data-collection switch, grouped for the settings page.</summary>
public static class TrackingCatalog
{
    public static readonly IReadOnlyList<TrackingCatalogSection> Sections = new[]
    {
        new TrackingCatalogSection("Focused app",
            "Recorded once a second about the app you are using right now. Stored only on this PC.",
            new[]
            {
                new TrackingCatalogEntry(TrackingItem.ActiveApp, "Active app and time",
                    "The name of the foreground app and how long you use it. Turning this off stops the dashboard and focus score."),
                new TrackingCatalogEntry(TrackingItem.WindowTitles, "Window titles",
                    "The title of the focused window, such as a document name or page title."),
                new TrackingCatalogEntry(TrackingItem.BrowserUrls, "Browser page URLs",
                    "The address of the page in the focused browser tab."),
                new TrackingCatalogEntry(TrackingItem.IdleDetection, "Idle detection",
                    "Notices when you stop using the keyboard and mouse. Without it, idle time counts as active."),
                new TrackingCatalogEntry(TrackingItem.FocusedScreenText, "On-screen text",
                    "Text read from the focused window every 10 to 30 seconds. No images or recordings are kept."),
            }),
        new TrackingCatalogSection("Activity signals",
            "Counts and events only. Which keys you press and what you copy are never recorded.",
            new[]
            {
                new TrackingCatalogEntry(TrackingItem.InputActivity, "Typing and mouse activity",
                    "How many keystrokes, clicks and scrolls happen in each app, sampled every 5 seconds."),
                new TrackingCatalogEntry(TrackingItem.DocumentPaths, "Open file paths",
                    "File paths shown in the focused window's title by editors and viewers."),
                new TrackingCatalogEntry(TrackingItem.ClipboardActivity, "Copy activity",
                    "How often you copy, and in which app. The copied content is never read, and password manager copies are ignored."),
            }),
        new TrackingCatalogSection("System", "Stored only on this PC.",
            new[]
            {
                new TrackingCatalogEntry(TrackingItem.AppLaunches, "App launches and quits",
                    "When apps with a visible window open and close."),
                new TrackingCatalogEntry(TrackingItem.ScreenState, "Lock, sleep and wake",
                    "When the PC locks, unlocks, sleeps or wakes. Helps explain gaps in the day."),
            }),
        new TrackingCatalogSection("Background",
            "Things happening outside the window you are focused on. Never counted as focused time.",
            new[]
            {
                new TrackingCatalogEntry(TrackingItem.BackgroundWindowText, "Text from background windows",
                    "Text from other visible windows, one at a time."),
                new TrackingCatalogEntry(TrackingItem.BrowserTabs, "Open browser tabs",
                    "Titles of the open tabs in Chrome, Edge, Firefox, Brave, Opera, Vivaldi and Arc."),
            }),
        new TrackingCatalogSection("AI chat",
            "Controls what the chat is allowed to see when you ask a question.",
            new[]
            {
                new TrackingCatalogEntry(TrackingItem.SendScreenTextToAi, "Share screen text and tabs with AI",
                    "Lets the chat quote on-screen text and open tabs. With a cloud provider this text leaves your PC; a local Ollama model keeps it here."),
                new TrackingCatalogEntry(TrackingItem.SendSignalsToAi, "Share activity signals with AI",
                    "Lets the chat use typing counts, open file paths, copy counts and lock or sleep events. Same rule for cloud providers."),
            }),
    };
}
