using FocusLens.Agent;
using FocusLens.Agent.Monitors;
using FocusLens.Agent.Stores;
using FocusLens.Core.Data;
using FocusLens.Core.Logging;
using FocusLens.Core.Paths;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Capture;

// FocusLens background agent: samples the focused app once a second and writes to SQLite.
// Single instance per user. The app stops it by signalling the named stop event.

const string MutexName = @"Local\FocusLensAgent.Single";
const string StopEventName = @"Local\FocusLensAgent.Stop";

using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
if (!isFirstInstance) return;

var log = new FileLog("agent");
log.Info("FocusLens agent starting");

try
{
    var db = new Db(AppPaths.DatabaseFile);
    db.Initialize();
    log.Info($"Database ready at {db.Path}");

    var privacy = new PrivacyFilter();
    var tracking = new TrackingSettingsProvider();
    var pause = new PauseGate();
    var foreground = new ForegroundCapture();
    var idle = new IdleDetector();
    var status = new RecordingStatusPublisher();

    var signalStore = new SignalStore(db, log);
    var screenshotStore = new ScreenshotStore(db, log);
    var tabStore = new TabStore(db, log);
    var textReader = new ScreenTextReader(log);
    if (!textReader.CanOcr) log.Warn("Windows OCR is unavailable; screen text falls back to UI Automation only");

    using var writer = new BatchWriter(db, privacy, log);
    using var input = new InputMonitor(signalStore, privacy, tracking, pause, foreground);
    using var systemEvents = new SystemEventMonitor(signalStore, privacy, tracking);
    using var clipboard = new ClipboardMonitor(signalStore, privacy, tracking, pause, foreground);
    using var background = new BackgroundTracker(textReader, screenshotStore, tabStore, privacy, idle, tracking, pause, log);

    var documents = new DocumentTracker(signalStore, privacy, tracking);
    var screenText = new ScreenTextScheduler(textReader, screenshotStore, privacy, tracking, pause, log);
    var loop = new CaptureLoop(foreground, idle, tracking, privacy, pause, status, writer, documents, screenText, input, log);

    input.Start();
    systemEvents.Start();
    clipboard.Start();
    background.Start();

    _ = Task.Run(() => { signalStore.PruneOld(); screenshotStore.PruneOld(); });

    using var cts = new CancellationTokenSource();
    using var stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
    _ = Task.Run(() => { stopEvent.WaitOne(); cts.Cancel(); });
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

    log.Info("Capture loop started, sampling every second");
    await loop.RunAsync(cts.Token);

    writer.Flush();
    log.Info("Agent exited cleanly");
}
catch (Exception ex)
{
    log.Error("Agent failed", ex);
    Environment.ExitCode = 1;
}
