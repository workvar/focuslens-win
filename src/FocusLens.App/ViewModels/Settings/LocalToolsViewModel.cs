using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Ai;
using FocusLens.Core.Health;
using FocusLens.Core.Setup;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>
/// The Local tools tab: detects Ollama, a model and Chroma DB on this PC and, when one is missing, offers to
/// download and set it up. Anything already installed is left alone.
/// </summary>
public sealed partial class LocalToolsViewModel : ObservableObject
{
    private readonly AiSettings _ai;

    [ObservableProperty] private string _modelName = OllamaSetup.SuggestedModel;
    [ObservableProperty] private bool _needsSetup;

    public ToolSetupItemViewModel Ollama { get; }
    public ToolSetupItemViewModel Model { get; }
    public ToolSetupItemViewModel Chroma { get; }
    public ObservableCollection<string> SuggestedModels { get; } = new() { "llama3.2", "llama3.1", "qwen2.5", "mistral", "gemma2", "phi3" };

    /// <summary>Raised after an install so other screens (the model dropdown, the status list) refresh.</summary>
    public event Action? Changed;

    public LocalToolsViewModel(AiSettings ai, HealthMonitor health)
    {
        _ai = ai;
        async Task Changes()
        {
            await UpdateNeedsSetupAsync();
            _ = health.RefreshAsync(force: true);
            Changed?.Invoke();
        }

        Ollama = new ToolSetupItemViewModel(
            "Ollama", "Runs AI models on this PC, so your activity never leaves it.", "Download and install", OllamaSetup.DownloadPage,
            async () =>
            {
                if (!OllamaSetup.IsInstalled) return (false, "Not installed.");
                var running = await new OllamaClient(_ai.OllamaHost).IsRunningAsync();
                return (true, running ? "Installed and running." : "Installed, but not running yet. It starts when you install a model or open Ollama.");
            },
            async (progress, ct) =>
            {
                await OllamaSetup.InstallAsync(progress, ct);
                progress.Report(new SetupProgress(-1, "Starting Ollama..."));
                await OllamaSetup.EnsureRunningAsync(_ai.OllamaHost, ct);
            },
            () => ("Install Ollama?", "FocusLens will download the official Ollama installer from ollama.com (about 1 GB) and install it for your Windows account. Continue?"),
            Changes);

        Model = new ToolSetupItemViewModel(
            "AI model", "A model must be downloaded before chat and meeting summaries can run locally.", "Download model", null,
            async () =>
            {
                var models = await new OllamaClient(_ai.OllamaHost).ListModelsAsync();
                return models.Count > 0
                    ? (true, $"{models.Count} installed: {string.Join(", ", models.Take(3))}{(models.Count > 3 ? ", ..." : "")}")
                    : (false, OllamaSetup.IsInstalled ? "No model downloaded yet." : "Install Ollama first.");
            },
            async (progress, ct) =>
            {
                if (!await OllamaSetup.EnsureRunningAsync(_ai.OllamaHost, ct))
                    throw new SetupException("Ollama is not running. Install or start Ollama first.");
                var name = ModelName.Trim();
                await new OllamaClient(_ai.OllamaHost).PullAsync(name, new Progress<(double, string)>(p =>
                    progress.Report(new SetupProgress(p.Item1 > 0 ? p.Item1 : -1, $"{name}: {p.Item2}"))), ct);
                if (_ai.OllamaModel.Trim().Length == 0)
                {
                    _ai.OllamaModel = name; // first model becomes the active one so chat works right away
                    _ai.Save();
                }
            },
            () => ("Download this model?", $"FocusLens will ask Ollama to download \"{ModelName.Trim()}\". Models are usually 2 to 5 GB. Continue?"),
            Changes);

        Chroma = new ToolSetupItemViewModel(
            "Chroma DB", "A local search index that lets FocusLens remember and find past activity.", "Set up Chroma", ChromaSetup.PythonDownloadPage,
            async () =>
            {
                var run = await PythonRunner.RunAsync("import chromadb; print(chromadb.__version__)", new Dictionary<string, string>(), CancellationToken.None);
                if (run.Started && run.ExitCode == 0) return (true, $"chromadb {run.Output.Trim()} is installed.");
                return (false, run.Started ? "Python is installed, but the chromadb package is missing." : "Python 3 is not installed.");
            },
            (progress, ct) => ChromaSetup.InstallAsync(progress, ct),
            () => ("Set up Chroma DB?", "FocusLens will install Python 3.12 if you do not have it (through winget), create a private environment in its own data folder, and install the chromadb package (about 100 MB). Your own Python is not changed. Continue?"),
            Changes);

        _ = CheckAllAsync();
    }

    public async Task CheckAllAsync()
    {
        await Task.WhenAll(Ollama.RecheckAsync(), Model.RecheckAsync(), Chroma.RecheckAsync());
        await UpdateNeedsSetupAsync();
    }

    private Task UpdateNeedsSetupAsync()
    {
        var localAi = _ai.Provider == AiProviderKind.Ollama;
        NeedsSetup = (localAi && (!Ollama.IsReady || !Model.IsReady)) || !Chroma.IsReady;
        return Task.CompletedTask;
    }
}
