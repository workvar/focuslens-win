using System.Speech.Recognition;
using FocusLens.Core.Meetings;

namespace FocusLens.Platform.Windows.Audio;

/// <summary>
/// Offline transcription with the Windows desktop speech recognizer (System.Speech). It needs no
/// network or model download, but accuracy is modest; swap in a Whisper-based ITranscriber for
/// better quality without touching anything else.
/// </summary>
public sealed class SystemSpeechTranscriber : ITranscriber
{
    public Task<bool> RequestAccessAsync()
    {
        try
        {
            var available = SpeechRecognitionEngine.InstalledRecognizers().Count > 0;
            return Task.FromResult(available);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(AudioChunk chunk, string meetingId, CancellationToken ct) =>
        Task.Run<IReadOnlyList<TranscriptSegment>>(() => Recognize(chunk, meetingId, ct), ct);

    private static IReadOnlyList<TranscriptSegment> Recognize(AudioChunk chunk, string meetingId, CancellationToken ct)
    {
        var segments = new List<TranscriptSegment>();
        try
        {
            using var engine = new SpeechRecognitionEngine();
            engine.LoadGrammar(new DictationGrammar());
            engine.SetInputToWaveFile(chunk.Path);

            engine.SpeechRecognized += (_, e) =>
            {
                var text = e.Result.Text?.Trim();
                if (string.IsNullOrEmpty(text)) return;
                var start = chunk.StartMs + (int)e.Result.Audio.AudioPosition.TotalMilliseconds;
                var end = start + (int)e.Result.Audio.Duration.TotalMilliseconds;
                segments.Add(new TranscriptSegment
                {
                    MeetingId = meetingId,
                    Track = chunk.Track.Id(),
                    StartMs = start,
                    EndMs = end,
                    Text = text,
                    Confidence = e.Result.Confidence,
                });
            };

            // RecognizeAsync raises events on a worker thread; wait for the file to be consumed.
            using var done = new ManualResetEventSlim();
            engine.RecognizeCompleted += (_, _) => done.Set();
            engine.RecognizeAsync(RecognizeMode.Multiple);
            while (!done.Wait(250))
                if (ct.IsCancellationRequested) { engine.RecognizeAsyncCancel(); break; }
        }
        catch (InvalidOperationException)
        {
            throw new TranscriptionException(TranscriptionFailure.EngineUnavailable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new TranscriptionException(TranscriptionFailure.RecognitionFailed, ex.Message);
        }
        return segments;
    }
}
