namespace FocusLens.Core.Ai;

/// <summary>
/// Sending one picture along with the prompt.
///
/// Every provider agrees that an image goes in the last user message and disagrees about
/// everything else: Anthropic wants a content block with a base64 source, the OpenAI-shaped APIs
/// want an "image_url" holding a data URL, and Ollama wants a plain "images" array of base64
/// strings beside the text. The three shapes live here so the per-provider stream classes keep
/// building their own bodies and only ask for the messages.
///
/// Only one image is ever sent. Guide sends the screen it is looking at, and a second picture
/// would double the cost of every planning call for nothing.
/// </summary>
public static class AiImageAttachment
{
    /// <summary>How the provider expects the picture to be attached.</summary>
    public enum Style
    {
        Anthropic,
        /// <summary>OpenAI, NVIDIA, DeepSeek: the chat-completions shape.</summary>
        OpenAi,
        Ollama,
    }

    /// <summary>
    /// The messages for a request, with <paramref name="image"/> attached to the final user turn
    /// when there is one. Without an image this is the plain text shape, so the existing path is
    /// unchanged.
    /// </summary>
    public static List<object> Messages(IReadOnlyList<Models.Message> history, string prompt,
        byte[]? image, Style style)
    {
        var messages = history
            .Select(m => (object)new
            {
                role = m.RoleEnum == Models.MessageRole.Assistant ? "assistant" : "user",
                content = m.ContentMd,
            })
            .ToList();

        if (image is not { Length: > 0 })
        {
            messages.Add(new { role = "user", content = prompt });
            return messages;
        }

        var base64 = Convert.ToBase64String(image);
        messages.Add(style switch
        {
            Style.Anthropic => new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "image", source = new { type = "base64", media_type = "image/png", data = base64 } },
                    new { type = "text", text = prompt },
                },
            },
            Style.OpenAi => new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64}" } },
                },
            },
            // Ollama keeps the text where it was and takes the picture beside it.
            _ => new { role = "user", content = prompt, images = new[] { base64 } },
        });
        return messages;
    }
}

/// <summary>
/// Which models can actually look at a picture.
///
/// Sending one to a model that cannot is not harmless: most providers answer with a 400 rather
/// than ignoring it, so a guide that attached a screenshot to a text-only model would fail every
/// planning call. When there is any doubt the answer is no, and Guide simply carries on with the
/// text description of the screen, which is what it did before vision existed.
/// </summary>
public static class AiVisionSupport
{
    /// <summary>
    /// Substrings of model names known to take images. Checked against the model actually being
    /// used, not the provider, because every provider serves both kinds.
    /// </summary>
    private static readonly string[] Seeing =
    {
        "gpt-4o", "gpt-4.1", "gpt-5", "o3", "o4",
        "claude",
        "llava", "bakllava", "moondream", "minicpm-v", "llama3.2-vision", "llama-3.2-vision",
        "qwen2-vl", "qwen2.5-vl", "qwen3-vl", "gemma3", "mistral-small3", "pixtral",
        "vision", "-vl", "internvl", "nemotron-nano-vl", "phi-4-multimodal", "granite3.2-vision",
    };

    /// <summary>Text-only models whose names would otherwise match something above.</summary>
    private static readonly string[] Blind =
    {
        "deepseek-r1", "deepseek-chat", "deepseek-coder", "claude-instant",
    };

    public static bool CanSee(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return false;
        var name = model.ToLowerInvariant();
        if (Blind.Any(name.Contains)) return false;
        return Seeing.Any(name.Contains);
    }

    /// <summary>
    /// The model this request will actually use: the per-request override when there is one, and
    /// the provider's own default otherwise.
    /// </summary>
    public static string? ModelFor(AiProvider provider, AiRequestOptions options) => provider switch
    {
        AiProvider.Ollama o => string.IsNullOrWhiteSpace(options.OllamaModel) ? o.Model : options.OllamaModel,
        AiProvider.Claude => Fallback(options.Model, StreamingAiClient.Models.Claude),
        AiProvider.OpenAi => Fallback(options.Model, StreamingAiClient.Models.OpenAi),
        AiProvider.Nvidia => Fallback(options.Model, StreamingAiClient.Models.Nvidia),
        AiProvider.DeepSeek => Fallback(options.Model, StreamingAiClient.Models.DeepSeek),
        _ => null,
    };

    public static bool CanSee(AiProvider provider, AiRequestOptions options) =>
        CanSee(ModelFor(provider, options));

    private static string Fallback(string? model, string fallback) =>
        string.IsNullOrWhiteSpace(model) ? fallback : model;
}
