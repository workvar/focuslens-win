using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusLens.Core.Ai;
using FocusLens.Core.Guide;

namespace FocusLens.App.Views.Settings;

public partial class GuideSettingsPanel : UserControl
{
    private GuideSettings? _settings;
    private bool _loading;

    public GuideSettingsPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Load();
        IsVisibleChanged += (_, _) => { if (IsVisible) ShowActiveProvider(); };
    }

    /// <summary>The panel's DataContext is the GuideSettings instance shared with the coordinator.</summary>
    private void Load()
    {
        if (DataContext is not GuideSettings settings) return;
        _settings = settings;
        _loading = true;
        Enabled.IsChecked = settings.Enabled;
        Hotkey.Text = settings.HotkeyDisplay;
        Follow.Value = Math.Clamp(settings.FollowLevel, 1, 5);
        ShowTag.IsChecked = settings.ShowTag;
        Model.Text = settings.Model;
        AnthropicModel.Text = settings.AnthropicModel;
        OpenAiModel.Text = settings.OpenAiModel;
        NvidiaModel.Text = settings.NvidiaModel;
        DeepSeekModel.Text = settings.DeepSeekModel;
        ShowActiveProvider();
        WebSearch.IsChecked = settings.WebSearch;
        SearxUrl.Text = settings.SearxUrl;
        FollowText.Text = FollowLabel(settings.FollowLevel);
        HoldFill.IsChecked = settings.HoldFill;
        HoldFillSeconds.Value = Math.Clamp(settings.HoldFillSeconds, GuideSettings.HoldFillMin, GuideSettings.HoldFillMax);
        HoldFillText.Text = SecondsLabel(HoldFillSeconds.Value);
        _loading = false;
    }

    private void OnChanged(object sender, RoutedEventArgs e) => Save();

    private void OnFollowChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        FollowText.Text = FollowLabel((int)e.NewValue);
        Save();
    }

    private void OnHoldFillChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        HoldFillText.Text = SecondsLabel(e.NewValue);
        Save();
    }

    private void OnRecordHotkey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (_settings is null) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin
            or Key.LeftShift or Key.RightShift) return;                       // wait for the real key

        var modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= GuideSettings.ModAlt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= GuideSettings.ModControl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= GuideSettings.ModShift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= GuideSettings.ModWin;

        // Needs Ctrl, Alt or Win, so Guide can never fire from typing an ordinary letter.
        if ((modifiers & (GuideSettings.ModAlt | GuideSettings.ModControl | GuideSettings.ModWin)) == 0) return;

        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyVirtualKey = KeyInterop.VirtualKeyFromKey(key);
        _settings.HotkeyDisplay = Describe(modifiers, key);
        Hotkey.Text = _settings.HotkeyDisplay;
        Save();
    }

    private void Save()
    {
        if (_loading || _settings is null) return;
        _settings.Enabled = Enabled.IsChecked == true;
        _settings.FollowLevel = (int)Follow.Value;
        _settings.ShowTag = ShowTag.IsChecked == true;
        _settings.Model = Model.Text;
        _settings.AnthropicModel = AnthropicModel.Text.Trim();
        _settings.OpenAiModel = OpenAiModel.Text.Trim();
        _settings.NvidiaModel = NvidiaModel.Text.Trim();
        _settings.DeepSeekModel = DeepSeekModel.Text.Trim();
        _settings.WebSearch = WebSearch.IsChecked == true;
        _settings.SearxUrl = SearxUrl.Text.Trim();
        _settings.HoldFill = HoldFill.IsChecked == true;
        _settings.HoldFillSeconds = HoldFillSeconds.Value;
        _settings.Save();   // the coordinator re-registers the shortcut on Changed
    }

    /// <summary>Shows the model field for the provider selected on the AI tab.</summary>
    private void ShowActiveProvider()
    {
        var provider = AiSettings.Load().Provider;
        OllamaModelRow.Visibility = provider == AiProviderKind.Ollama ? Visibility.Visible : Visibility.Collapsed;
        AnthropicModelRow.Visibility = provider == AiProviderKind.Claude ? Visibility.Visible : Visibility.Collapsed;
        OpenAiModelRow.Visibility = provider == AiProviderKind.OpenAi ? Visibility.Visible : Visibility.Collapsed;
        NvidiaModelRow.Visibility = provider == AiProviderKind.Nvidia ? Visibility.Visible : Visibility.Collapsed;
        DeepSeekModelRow.Visibility = provider == AiProviderKind.DeepSeek ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string SecondsLabel(double seconds) => $"{seconds:0.#} s";

    private static string FollowLabel(int level) => level <= 2 ? "Lazy" : level == 3 ? "Relaxed" : "Snappy";

    private static string Describe(int modifiers, Key key)
    {
        var parts = new List<string>();
        if ((modifiers & GuideSettings.ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & GuideSettings.ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & GuideSettings.ModShift) != 0) parts.Add("Shift");
        if ((modifiers & GuideSettings.ModWin) != 0) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
