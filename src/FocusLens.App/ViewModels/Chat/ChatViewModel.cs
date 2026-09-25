using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Ai;
using FocusLens.Core.Ai.Chat;
using FocusLens.Core.Models;
using FocusLens.Core.Repositories;

namespace FocusLens.App.ViewModels.Chat;

/// <summary>Chat about your own activity, with streamed answers and optional charts.</summary>
public sealed partial class ChatViewModel : ObservableObject
{
    private readonly ConversationStore _store;
    private readonly AiQueryService _query;
    private CancellationTokenSource? _cts;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SendCommand))] private string _inputText = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SendCommand))] private bool _isStreaming;
    [ObservableProperty] private Conversation? _conversation;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    public IReadOnlyList<string> Suggestions { get; } = new[]
    {
        "How focused was I today?",
        "Where did my time go this week?",
        "What did I do in my browser yesterday?",
    };

    public bool IsEmpty => Messages.Count == 0;

    public ChatViewModel(ConversationStore store, AiQueryService query)
    {
        _store = store;
        _query = query;
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task OpenAsync(Conversation? conversation)
    {
        Cancel();
        Conversation = conversation;
        Messages.Clear();
        if (conversation is null) return;

        foreach (var message in await _store.FetchMessagesAsync(conversation.Id))
            Messages.Add(ChatMessageViewModel.From(message));
    }

    public Task StartNewAsync() => OpenAsync(null);

    [RelayCommand]
    private void UseSuggestion(string text) => InputText = text;

    private bool CanSend() => !IsStreaming && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var question = InputText.Trim();
        InputText = "";
        var conversation = Conversation ??= await _store.CreateConversationAsync();

        Messages.Add(new ChatMessageViewModel(MessageRole.User, question));
        await _store.AppendMessageAsync(Message.Make(conversation.Id, MessageRole.User, question));
        if (conversation.Title == "New Chat")
            await _store.SetTitleAsync(Truncate(question, 48), conversation.Id);

        var reply = new ChatMessageViewModel(MessageRole.Assistant, "") { IsStreaming = true };
        Messages.Add(reply);

        IsStreaming = true;
        _cts = new CancellationTokenSource();
        try
        {
            await StreamAnswerAsync(question, conversation, reply, _cts.Token);
        }
        finally
        {
            reply.IsStreaming = false;
            IsStreaming = false;
        }
    }

    private async Task StreamAnswerAsync(string question, Conversation conversation, ChatMessageViewModel reply, CancellationToken ct)
    {
        try
        {
            await foreach (var evt in _query.SendAsync(question, conversation, ct))
            {
                switch (evt)
                {
                    case StreamEvent.Token token:
                        reply.Content += token.Text;
                        break;
                    case StreamEvent.Error error:
                        await ShowErrorAsync(conversation, reply, error.Exception);
                        return;
                    case StreamEvent.Done done:
                        await FinishAsync(conversation, reply, done.Chart);
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (reply.Content.Length > 0)
                await _store.AppendMessageAsync(Message.Make(conversation.Id, MessageRole.Assistant, reply.Content));
            else
                Messages.Remove(reply);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(conversation, reply, ex);
        }
    }

    private async Task FinishAsync(Conversation conversation, ChatMessageViewModel reply, ChartPayload? chart)
    {
        await _store.AppendMessageAsync(Message.Make(conversation.Id, MessageRole.Assistant, reply.Content));
        if (chart is { Points.Count: > 0 } && chart.Type != ChartType.None)
        {
            var chartJson = chart.ToJson();
            Messages.Add(new ChatMessageViewModel(MessageRole.Chart, "", chart));
            await _store.AppendMessageAsync(Message.Make(conversation.Id, MessageRole.Chart, "", chartJson));
        }
    }

    private async Task ShowErrorAsync(Conversation conversation, ChatMessageViewModel reply, Exception ex)
    {
        var text = ex is AiException ? ex.Message : $"Something went wrong: {ex.Message}";
        Messages.Remove(reply);
        Messages.Add(new ChatMessageViewModel(MessageRole.Error, text));
        await _store.AppendMessageAsync(Message.Make(conversation.Id, MessageRole.Error, text));
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "...";
}
