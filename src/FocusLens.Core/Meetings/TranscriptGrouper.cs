namespace FocusLens.Core.Meetings;

/// <summary>Groups recognised words into utterances by pause length and size.</summary>
public static class TranscriptGrouper
{
    public sealed record Word(string Text, int StartMs, int EndMs, double Confidence);
    public sealed record Utterance(string Text, int StartMs, int EndMs, double Confidence);

    public static List<Utterance> Group(IEnumerable<Word> words, int gapMs = 700, int maxChars = 240)
    {
        var utterances = new List<Utterance>();
        var current = new List<Word>();

        void Flush()
        {
            if (current.Count == 0) return;
            utterances.Add(new Utterance(
                string.Join(" ", current.Select(w => w.Text)),
                current[0].StartMs, current[^1].EndMs, current.Average(w => w.Confidence)));
            current.Clear();
        }

        foreach (var word in words)
        {
            if (current.Count == 0)
            {
                current.Add(word);
                continue;
            }
            var gap = word.StartMs - current[^1].EndMs;
            var projected = current.Sum(w => w.Text.Length) + current.Count + word.Text.Length;
            if (gap > gapMs || projected > maxChars) Flush();
            current.Add(word);
        }
        Flush();
        return utterances;
    }
}
