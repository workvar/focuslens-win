using System.Runtime.CompilerServices;
using FocusLens.Core.Paths;
using FocusLens.Core.Repositories;

namespace FocusLens.Core.Meetings;

/// <summary>
/// The real recorder and processor. While recording it feeds audio chunks to the transcriber
/// as they appear; after Stop it waits for the queue to drain, summarises, and finalises.
/// </summary>
public sealed class MeetingPipeline : IMeetingRecorder, IMeetingProcessor
{
    private readonly MeetingStore _store;
    private readonly ITranscriber _transcriber;
    private readonly MeetingSummarizer _summarizer;
    private readonly IMeetingNotifier _notifier;
    private readonly IMeetingCaptureFactory _captureFactory;
    private readonly Func<bool> _keepAudio;

    private IMeetingCapture? _capture;
    private Task? _transcription;
    private string? _meetingId;
    private string _title = "";
    private int _chunksSeen;
    private int _chunksDone;
    private int _durationS;
    private MeetingError? _engineFailure;

    public MeetingPipeline(
        MeetingStore store, ITranscriber transcriber, MeetingSummarizer summarizer,
        IMeetingNotifier notifier, IMeetingCaptureFactory captureFactory, Func<bool> keepAudio)
    {
        _store = store;
        _transcriber = transcriber;
        _summarizer = summarizer;
        _notifier = notifier;
        _captureFactory = captureFactory;
        _keepAudio = keepAudio;
    }

    public async Task BeginAsync(string meetingId, MeetingCandidate candidate, DateTime startedAt)
    {
        Reset();
        _title = candidate.ResolvedTitle;

        if (!await _transcriber.RequestAccessAsync())
            throw new TranscriptionException(TranscriptionFailure.NotAuthorized);

        var directory = AppPaths.MeetingDir(meetingId);
        await _store.CreateAsync(Meeting.New(meetingId, candidate, startedAt, directory));

        var capture = _captureFactory.Create();
        _capture = capture;
        _meetingId = meetingId;
        await capture.StartAsync(directory, candidate.AppId);

        _transcription = Task.Run(() => TranscribeLoopAsync(capture, meetingId));
        await _notifier.NotifyStartedAsync(meetingId, _title);
    }

    public void Pause() => _capture?.Pause();
    public void Resume() => _capture?.Resume();

    public async Task EndAsync(int durationS)
    {
        _durationS = durationS;
        if (_capture is not null) await _capture.StopAsync();
        if (_meetingId is not null)
            await _store.FinishAsync(_meetingId, "transcribing", durationS);
    }

    public async IAsyncEnumerable<MeetingProcessingEvent> ProcessAsync(
        string meetingId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_transcription is { } task)
        {
            while (!task.IsCompleted)
            {
                yield return new MeetingProcessingEvent.Transcribing(Progress);
                await Task.Delay(250, ct);
            }
            await task;
        }
        yield return new MeetingProcessingEvent.Transcribing(1);

        if (_engineFailure is { } failure)
        {
            await _notifier.NotifyFailedAsync(meetingId, failure);
            await _store.FinishAsync(meetingId, "failed", _durationS);
            yield return new MeetingProcessingEvent.Failed(failure);
            yield break;
        }

        yield return new MeetingProcessingEvent.Summarizing();

        string? summary = null;
        var summarizeFailed = false;
        try { summary = await _summarizer.SummarizeAsync(meetingId, _title, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { summarizeFailed = true; }

        await FinishAsync(meetingId, summary);
        yield return summarizeFailed
            ? new MeetingProcessingEvent.Failed(MeetingError.SummarizationFailed)
            : new MeetingProcessingEvent.Finished();
    }

    private double Progress => _chunksSeen == 0 ? 0 : Math.Min(1, _chunksDone / (double)_chunksSeen);

    private async Task TranscribeLoopAsync(IMeetingCapture capture, string meetingId)
    {
        await foreach (var chunk in capture.Chunks.ReadAllAsync())
        {
            Interlocked.Increment(ref _chunksSeen);
            try
            {
                var segments = await _transcriber.TranscribeAsync(chunk, meetingId, CancellationToken.None);
                await _store.AppendSegmentsAsync(segments);
            }
            catch (TranscriptionException ex) when (ex.Failure == TranscriptionFailure.EngineUnavailable)
            {
                _engineFailure = MeetingError.TranscriptionFailed;
            }
            catch
            {
                // A single bad chunk should not lose the whole meeting.
            }
            Interlocked.Increment(ref _chunksDone);
        }
    }

    private async Task FinishAsync(string meetingId, string? summary)
    {
        if (!_keepAudio())
        {
            _capture?.DeleteAudio();
            await _store.ClearAudioAsync(meetingId);
        }
        await _store.FinishAsync(meetingId, "complete", _durationS);
        await _notifier.NotifyCompleteAsync(meetingId, _title, summary);
        Reset();
    }

    private void Reset()
    {
        _transcription = null;
        _capture = null;
        _meetingId = null;
        _chunksSeen = 0;
        _chunksDone = 0;
        _durationS = 0;
        _engineFailure = null;
    }
}
