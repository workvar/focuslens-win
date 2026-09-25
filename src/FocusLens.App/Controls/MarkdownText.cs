using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FocusLens.App.Controls;

/// <summary>
/// A tiny markdown renderer for chat answers: headings, bullets, **bold**, `code` and fenced code.
/// It builds a TextBlock's inlines directly, so text stays selectable-free and lightweight while streaming.
/// </summary>
public sealed class MarkdownText : TextBlock
{
    private static readonly Regex Inline = new(@"(\*\*[^*]+\*\*|`[^`]+`)", RegexOptions.Compiled);

    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownText),
        new PropertyMetadata("", (d, _) => ((MarkdownText)d).Render()));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public MarkdownText() => TextWrapping = TextWrapping.Wrap;

    private void Render()
    {
        Inlines.Clear();
        var inFence = false;
        var first = true;

        foreach (var raw in (Markdown ?? "").Replace("\r", "").Split('\n'))
        {
            if (raw.TrimStart().StartsWith("```")) { inFence = !inFence; continue; }
            if (!first) Inlines.Add(new LineBreak());
            first = false;

            if (inFence)
            {
                Inlines.Add(new Run(raw) { FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = FontSize - 1 });
                continue;
            }

            var line = raw;
            var heading = Regex.Match(line, @"^\s{0,3}#{1,6}\s+(.*)$");
            if (heading.Success)
            {
                Inlines.Add(new Run(heading.Groups[1].Value) { FontWeight = FontWeights.SemiBold });
                continue;
            }

            var bullet = Regex.Match(line, @"^(\s*)[-*]\s+(.*)$");
            if (bullet.Success)
            {
                Inlines.Add(new Run(bullet.Groups[1].Value + "•  "));
                line = bullet.Groups[2].Value;
            }
            AddInline(line);
        }
    }

    private void AddInline(string line)
    {
        var position = 0;
        foreach (Match match in Inline.Matches(line))
        {
            if (match.Index > position) Inlines.Add(new Run(line[position..match.Index]));
            var token = match.Value;
            if (token.StartsWith("**"))
                Inlines.Add(new Run(token[2..^2]) { FontWeight = FontWeights.SemiBold });
            else
                Inlines.Add(new Run(token[1..^1])
                {
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                });
            position = match.Index + match.Length;
        }
        if (position < line.Length) Inlines.Add(new Run(line[position..]));
    }
}
