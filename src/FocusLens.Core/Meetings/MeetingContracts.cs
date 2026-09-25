using System.Threading.Channels;
using FocusLens.Core.Models;

namespace FocusLens.Core.Meetings;

/// <summary>A slice of recorded audio waiting for transcription.</summary>
public sealed record AudioChunk(string Path, TranscriptTrack Track, int StartMs, int DurationMs);

public abstract record MeetingProcessingEvent
{
    public sealed record Transcribing(double Progress) : MeetingProcessingEvent;
    public sealed record Summarizing : MeetingProcessingEvent;
    public sealed record Finished : MeetingProcessingEvent;
    public sealed record Failed(MeetingError Error) : MeetingProcessingEvent;
}

/// <summary>Runs after recording stops: finishes transcription, then summarises.</summary>
public interface IMeetingProcessor
{
    IAsyncEnumerable<MeetingProcessingEvent> ProcessAsync(string meetingId, CancellationToken ct = default);
}

/// <summary>Owns audio capture for the duration of a meeting.</summary>
public interface IMeetingRecorder
{
    Task BeginAsync(string meetingId, MeetingCandidate candidate, DateTime startedAt);
    void Pause();
    void Resume();
    Task EndAsync(int durationS);
}

public sealed class MeetingCaptureException : Exception
{
    public MeetingError Error { get; }
    public MeetingCaptureException(MeetingError error, string? message = null, Exception? inner = null)
        : base(message ?? error.UserMessage(), inner) => Error = error;
}

public enum TranscriptionFailure
{
    NotAuthorized,
    EngineUnavailable,
    RecognitionFailed,
}

public sealed class TranscriptionException : Exception
{
    public TranscriptionFailure Failure { get; }
    public TranscriptionException(TranscriptionFailure failure, string? message = null)
        : base(message ?? failure.ToString()) => Failure = failure;
}

/// <summary>Captures the microphone and the other participants' audio into chunked files.</summary>
public interface IMeetingCapture
{
    ChannelReader<AudioChunk> Chunks { get; }
    Task StartAsync(string directory, string? appId);
    void Pause();
    void Resume();
    Task StopAsync();
    void DeleteAudio();
}

public interface IMeetingCaptureFactory
{
    IMeetingCapture Create();
}

/// <summary>Turns an audio chunk into transcript segments.</summary>
public interface ITranscriber
{
    Task<bool> RequestAccessAsync();
    Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(AudioChunk chunk, string meetingId, CancellationToken ct);
}

public interface IMeetingNotifier
{
    Task NotifyStartedAsync(string meetingId, string title);
    Task NotifyCompleteAsync(string meetingId, string title, string? summary);
    Task NotifyFailedAsync(string meetingId, MeetingError error);
}

public sealed class NullMeetingNotifier : IMeetingNotifier
{
    public Task NotifyStartedAsync(string meetingId, string title) => Task.CompletedTask;
    public Task NotifyCompleteAsync(string meetingId, string title, string? summary) => Task.CompletedTask;
    public Task NotifyFailedAsync(string meetingId, MeetingError error) => Task.CompletedTask;
}

/// <summary>Stand-in processor that fakes progress; useful before a transcriber is configured.</summary>
public sealed class NoopMeetingProcessor : IMeetingProcessor
{
    public TimeSpan StepDelay { get; init; } = TimeSpan.FromMilliseconds(400);

    public async IAsyncEnumerable<MeetingProcessingEvent> ProcessAsync(
        string meetingId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var step = 1; step <= 4; step++)
        {
            yield return new MeetingProcessingEvent.Transcribing(step / 4.0);
            await Task.Delay(StepDelay, ct);
        }
        yield return new MeetingProcessingEvent.Summarizing();
        await Task.Delay(StepDelay, ct);
        yield return new MeetingProcessingEvent.Finished();
    }
}
