using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Ai.Chat;
using FocusLens.Core.Models;

namespace FocusLens.App.ViewModels.Chat;

/// <summary>One bubble in the chat transcript. Content grows while a reply streams in.</summary>
public sealed partial class ChatMessageViewModel : ObservableObject
{
    public MessageRole Role { get; }

    [ObservableProperty] private string _content;
    [ObservableProperty] private ChartPayload? _chart;
    [ObservableProperty] private bool _isStreaming;

    public bool IsUser => Role == MessageRole.User;
    public bool IsAssistant => Role == MessageRole.Assistant;
    public bool IsError => Role == MessageRole.Error;
    public bool IsChart => Role == MessageRole.Chart;

    public ChatMessageViewModel(MessageRole role, string content, ChartPayload? chart = null)
    {
        Role = role;
        _content = content;
        _chart = chart;
    }

    public static ChatMessageViewModel From(Message message) =>
        new(message.RoleEnum, message.ContentMd, ChartPayload.FromJson(message.ChartJson));
}
