using System.Globalization;
using System.Text;

namespace FocusLens.Core.Guide;

/// <summary>
/// Comparing a label the model wrote with a label UI Automation reported.
///
/// This used to be trim plus lowercase, and matching was then exact. That is stricter than it
/// sounds, because the two sides almost never agree on the small stuff: UI Automation hands back
/// "&amp;File" with the accelerator marker still in it, a menu item is "Save As…" with a real ellipsis
/// and the model writes "Save As...", a row is "Wi&#8209;Fi" with a non-breaking hyphen, or "Downloads:"
/// with a colon the model drops. Every one of those came back as a control that is not on the
/// screen, and after a few of them the guide stopped and told the user the controls did not exist,
/// when in fact they were right there.
///
/// So both sides are put through the same reduction first. It only removes decoration; it never
/// lets a different control match, because what survives is still compared whole. "Downloads" still
/// does not match "Downloads folder".
/// </summary>
public static class GuideLabel
{
    /// <summary>Characters different apps use for the same thing, rewritten to the plain ASCII the model types.</summary>
    private static readonly (char From, string To)[] Replacements =
    {
        ('‑', "-"), ('‐', "-"), ('‒', "-"), ('–', "-"), ('—', "-"),
        ('…', "..."),
        ('‘', "'"), ('’', "'"), ('“', "\""), ('”', "\""),
        (' ', " "), (' ', " "), (' ', " "),
    };

    private const string Trailing = ".:;,!?\"' ";
    private const string Leading = "\"' ";

    /// <summary>A label reduced to the part both sides can be expected to agree on.</summary>
    public static string Normalise(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Windows puts the keyboard accelerator in the name itself ("&File", "Save &As") and
        // escapes a real ampersand as "&&". Rather than try to tell the two apart, every ampersand
        // goes from both sides: the model writes "Find & Replace" where the app reports
        // "Find && Replace", and dropping them makes those meet. Nothing else is lost, because two
        // controls are never distinguished by an ampersand alone.
        var value = text.Replace('&', ' ');

        // A shortcut tacked on after a tab, as some toolkits report menu items.
        var tab = value.IndexOf('\t');
        if (tab >= 0) value = value[..tab];

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var replaced = false;
            foreach (var (from, to) in Replacements)
            {
                if (c != from) continue;
                builder.Append(to);
                replaced = true;
                break;
            }
            if (!replaced) builder.Append(c);
        }

        // Case and accents. Folding means "Añadir" and "Anadir" meet, which matters when the model
        // retypes a label from a non-English interface.
        value = StripAccents(builder.ToString()).ToLowerInvariant().Trim();

        // Decoration around the edges: quotes, a trailing ellipsis, a trailing colon on a field's
        // own label, and any punctuation left at the ends.
        value = value.TrimEnd(Trailing.ToCharArray()).TrimStart(Leading.ToCharArray());

        // Runs of whitespace become one space.
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// True when two labels name the same control. Empty never matches anything: an unnamed control
    /// is not something Guide should point at.
    /// </summary>
    public static bool Same(string a, string b)
    {
        var left = Normalise(a);
        return left.Length > 0 && left == Normalise(b);
    }

    private static string StripAccents(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
