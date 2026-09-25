using FocusLens.Core.Data;
using FocusLens.Core.Models;

namespace FocusLens.Core.Repositories;

/// <summary>Persistent multi-conversation chat storage.</summary>
public sealed class ConversationStore
{
    private readonly Db _db;

    public event Action? Changed;

    public ConversationStore(Db db) => _db = db;

    public async Task<IReadOnlyList<Conversation>> FetchActiveConversationsAsync() =>
        (await _db.QueryAsync<Conversation>(
            "SELECT * FROM conversations WHERE archived_at IS NULL ORDER BY updated_at DESC")).ToList();

    public async Task<IReadOnlyList<Message>> FetchMessagesAsync(string conversationId) =>
        (await _db.QueryAsync<Message>(
            "SELECT * FROM messages WHERE conversation_id = @conversationId ORDER BY created_at ASC",
            new { conversationId })).ToList();

    /// <summary>Most recent messages that fit a rough token budget, oldest first.</summary>
    public async Task<IReadOnlyList<Message>> RecentMessagesForContextAsync(string conversationId, int budgetTokens)
    {
        var all = await FetchMessagesAsync(conversationId);
        var kept = new List<Message>();
        var spent = 0;
        foreach (var message in all.Reverse())
        {
            var cost = message.TokenCount ?? Math.Max(1, message.ContentMd.Length / 4);
            if (spent + cost > budgetTokens) break;
            spent += cost;
            kept.Add(message);
        }
        kept.Reverse();
        return kept;
    }

    public async Task<Conversation> CreateConversationAsync(string title = "New Chat")
    {
        var conversation = Conversation.New(title);
        await _db.ExecuteAsync(@"
            INSERT INTO conversations (id, title, created_at, updated_at, archived_at, supabase_synced_at)
            VALUES (@Id, @Title, @CreatedAt, @UpdatedAt, NULL, NULL)", conversation);
        Changed?.Invoke();
        return conversation;
    }

    public async Task<Message> AppendMessageAsync(Message message)
    {
        await Task.Run(() => _db.InTransaction((connection, transaction) =>
        {
            Dapper.SqlMapper.Execute(connection, @"
                INSERT INTO messages (id, conversation_id, role, content_md, chart_json, created_at, token_count)
                VALUES (@Id, @ConversationId, @Role, @ContentMd, @ChartJson, @CreatedAt, @TokenCount)",
                message, transaction);
            Dapper.SqlMapper.Execute(connection,
                "UPDATE conversations SET updated_at = @CreatedAt WHERE id = @ConversationId",
                message, transaction);
        }));
        Changed?.Invoke();
        return message;
    }

    public async Task SetTitleAsync(string title, string conversationId)
    {
        await _db.ExecuteAsync(
            "UPDATE conversations SET title = @title, updated_at = @now WHERE id = @conversationId",
            new { title, conversationId, now = Now() });
        Changed?.Invoke();
    }

    public async Task ArchiveAsync(string conversationId)
    {
        await _db.ExecuteAsync(
            "UPDATE conversations SET archived_at = @now, updated_at = @now WHERE id = @conversationId",
            new { conversationId, now = Now() });
        Changed?.Invoke();
    }

    public async Task DeleteAsync(string conversationId)
    {
        await _db.ExecuteAsync("DELETE FROM conversations WHERE id = @conversationId", new { conversationId });
        Changed?.Invoke();
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
