using FocusLens.App.Services;
using FocusLens.App.Services.Auth;
using FocusLens.Core.Ai;
using FocusLens.Core.Ai.Chat;
using FocusLens.Core.Data;
using FocusLens.Core.Health;
using FocusLens.Core.Logging;
using FocusLens.Core.Meetings;
using FocusLens.Core.Paths;
using FocusLens.Core.Repositories;
using FocusLens.Platform.Windows.Audio;
using FocusLens.Platform.Windows.Shell;

namespace FocusLens.App;

/// <summary>Composition root: builds and owns every long-lived service, wired once at startup.</summary>
public sealed class AppServices : IDisposable
{
    public FileLog Log { get; } = new("app");
    public AppSettings Settings { get; }
    public AiSettings Ai { get; set; }
    public ISecretStore Secrets { get; } = new DpapiSecretStore();

    public Db Db { get; }
    public ActivityRepository Activity { get; }
    public ConversationStore Conversations { get; }
    public MeetingStore Meetings { get; }

    public StreamingAiClient AiClient { get; }
    public AiQueryService Query { get; }

    public AgentLauncher Agent { get; } = new();
    public HealthMonitor Health { get; }
    public SupabaseAuthService Auth { get; }
    public TrayIconService Tray { get; }
    public UpdateService Updates { get; }

    public MeetingSessionCoordinator MeetingSession { get; }
    public MeetingPipeline MeetingPipeline { get; }
    public MeetingSummarizer MeetingSummarizer { get; }
    public MeetingDetectionService MeetingDetection { get; }

    public AppServices(Action<string> openMeeting)
    {
        Settings = AppSettings.Load();
        Ai = AiSettings.Load();

        Db = new Db(AppPaths.DatabaseFile);
        Db.Initialize();

        Activity = new ActivityRepository(Db);
        Conversations = new ConversationStore(Db);
        Meetings = new MeetingStore(Db);

        AiClient = new StreamingAiClient(() => Ai.Resolve(Secrets));
        Query = new AiQueryService(Conversations, Activity, AiClient);

        Health = new HealthMonitor(new IHealthProbe[]
        {
            new AgentProbe(Agent),
            new SqliteProbe(Db),
            new ChromaProbe(),
            new LlmProbe(() => Ai, Secrets),
        });

        Auth = new SupabaseAuthService(Settings, Secrets);
        Tray = new TrayIconService();
        Updates = new UpdateService((message, ex) => Log.Error(message, ex));

        MeetingSummarizer = new MeetingSummarizer(AiClient, Meetings, () => Ai.AllowCloudMeetingSummary);
        var summarizer = MeetingSummarizer;
        var notifier = new MeetingNotifier(Tray, openMeeting);
        MeetingPipeline = new MeetingPipeline(
            Meetings, new SystemSpeechTranscriber(), summarizer, notifier,
            new WindowsMeetingCaptureFactory(), () => Ai.KeepMeetingAudio);

        MeetingSession = new MeetingSessionCoordinator(MeetingPipeline);
        MeetingSession.Attach((IMeetingRecorder)MeetingPipeline);
        MeetingDetection = new MeetingDetectionService(MeetingSession, Tray, () => Ai);
    }

    /// <summary>Marks meetings a crash left in "recording" as interrupted, on startup.</summary>
    public Task RecoverMeetingsAsync() => Meetings.MarkOrphanedAsInterruptedAsync();

    public void SaveAi() => Ai.Save();

    public void Dispose()
    {
        MeetingDetection.Dispose();
        Updates.Dispose();
        Tray.Dispose();
    }
}
