namespace FocusLens.Core.Models;

public enum MessageRole
{
    User,
    Assistant,
    Chart,
    Error,
}

public sealed class Conversation
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "New Chat";
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long? ArchivedAt { get; set; }
    public long? SupabaseSyncedAt { get; set; }

    public static Conversation New(string title = "New Chat", DateTimeOffset? now = null)
    {
        var ts = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        return new Conversation { Id = Guid.NewGuid().ToString(), Title = title, CreatedAt = ts, UpdatedAt = ts };
    }
}

public sealed class Message
{
    public string Id { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public string Role { get; set; } = "user";
    public string ContentMd { get; set; } = "";
    public string? ChartJson { get; set; }
    public long CreatedAt { get; set; }
    public int? TokenCount { get; set; }

    public MessageRole RoleEnum =>
        Enum.TryParse<MessageRole>(Role, ignoreCase: true, out var role) ? role : MessageRole.Error;

    public static Message Make(
        string conversationId, MessageRole role, string contentMd,
        string? chartJson = null, DateTimeOffset? now = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        ConversationId = conversationId,
        Role = role.ToString().ToLowerInvariant(),
        ContentMd = contentMd,
        ChartJson = chartJson,
        CreatedAt = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
        TokenCount = Math.Max(1, contentMd.Length / 4),
    };
}
