using FocusLens.Core.Models;

namespace FocusLens.Core.Ai.Chat;

/// <summary>
/// Keeps only the last few real turns. The current question is already inside the prompt, and long
/// earlier answers make small local models repeat themselves, so they are shortened.
/// </summary>
public static class HistoryTrimmer
{
    public static IReadOnlyList<Message> Trim(
        IReadOnlyList<Message> recent, string currentQuestion, int maxMessages = 4, int maxAssistantChars = 500)
    {
        var turns = recent
            .Where(m => m.RoleEnum is MessageRole.User or MessageRole.Assistant)
            .Where(m => !string.IsNullOrWhiteSpace(m.ContentMd))
            .ToList();

        if (turns.Count > 0 && turns[^1].RoleEnum == MessageRole.User && turns[^1].ContentMd.Trim() == currentQuestion.Trim())
            turns.RemoveAt(turns.Count - 1);

        return turns.TakeLast(maxMessages).Select(m => Shorten(m, maxAssistantChars)).ToList();
    }

    private static Message Shorten(Message message, int maxAssistantChars)
    {
        if (message.RoleEnum != MessageRole.Assistant || message.ContentMd.Length <= maxAssistantChars) return message;
        return new Message
        {
            Id = message.Id, ConversationId = message.ConversationId, Role = message.Role,
            ContentMd = message.ContentMd[..maxAssistantChars] + "...", CreatedAt = message.CreatedAt,
        };
    }
}
