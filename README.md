# FocusLens for Windows

A contextual AI productivity tool for your PC. FocusLens observes your digital activity, understands what you are working on, shows where your time goes, and lets you ask an AI questions about your habits. It can run fully on-device with Ollama.

macOS version: [focuslens](https://github.com/workvar/focuslens). Both apps follow the same [roadmap](ROADMAP.md).

## Status

FocusLens is in **Phase 1 of 4** (target release v1.0.0).

| Phase | Theme | Version | Status |
|---|---|---|---|
| 1 | Foundations: Ollama, screen context, audio and meeting recording, forest redesign, auto updates | v1.0.0 | In progress (2 of 6 done) |
| 2 | Chroma DB integration, UI and UX fixes | v1.1.0 | Planned |
| 3 | Focus tracker, distraction blocker, PPT generation | v1.2.0 | Planned |
| 4 | Session grouping, adaptive focus, professional metrics | v1.3.0 | Planned |

Details and per-item status are in [ROADMAP.md](ROADMAP.md). Release history is in [CHANGELOG.md](CHANGELOG.md) and [docs/release-notes/](docs/release-notes/).

## Features

- **Activity tracking.** A background agent records the foreground app and window, browser tabs, idle time, input activity, and clipboard changes, stored locally in SQLite.
- **Dashboard.** Daily summaries by category with a focus score and charts.
- **AI chat.** Streaming chat with Ollama, Claude, or OpenAI, with conversation history.
- **Screen context.** Active-window capture with on-device Windows OCR.
- **Meetings.** Automatic detection, audio capture, offline transcription, and summaries with action items.
- **Privacy controls.** Pause tracking and exclude apps or URLs.
- **Themes.** Forest light and dark themes.
- **Auto update.** Velopack checks GitHub Releases in the background and asks before downloading. See [docs/auto-update.md](docs/auto-update.md).

## Architecture

```
src/
  FocusLens.App/               WPF app: views, view models, themes, tray, updater, auth
  FocusLens.Agent/             Background tracking process (capture loop, monitors, privacy filter)
  FocusLens.Core/              Platform-neutral logic: AI streams, context, meetings, storage, privacy
  FocusLens.Platform.Windows/  Windows APIs: UI Automation, OCR, audio, DPAPI, auto-start
scripts/publish.ps1            Builds agent and app into dist\
build/StopAgent.targets        Stops a running agent before rebuilds
```

Data flow: Windows APIs, then the agent's capture loop, then local SQLite, then summaries for the dashboard and context for the AI chat.

## Privacy

- Activity events, window titles, URLs, and screen text are stored locally under `%LOCALAPPDATA%\FocusLens` (override with `FOCUSLENS_DATA_DIR`).
- OCR and meeting transcription run on your PC.
- API keys are stored with Windows DPAPI, not in plain text.
- With **Ollama**, AI requests never leave your PC. With **Claude** or **OpenAI**, the context the app builds for your question is sent to that provider, so use Ollama if that is not acceptable.
- Supabase handles Google sign-in and your profile only; activity data never goes through it.

## Install

Download the installer from the [latest release](https://github.com/workvar/focuslens-win/releases/latest) and run it. It bundles the .NET runtime and starts the agent for you.

## Build from source

Requirements: Windows 10 version 2004 (build 19041) or later, the [.NET 10 SDK](https://dotnet.microsoft.com/download), and Visual Studio 2022 or the `dotnet` CLI. [Ollama](https://ollama.com) is optional.

```powershell
git clone https://github.com/workvar/focuslens-win.git
cd focuslens-win
dotnet build FocusLens.sln
dotnet run --project src/FocusLens.App
```

Building the app also copies the agent next to it, and the app launches it.

### Sign-in configuration

Google sign-in needs a Supabase project. Set these environment variables before launching:

```powershell
$env:FOCUSLENS_SUPABASE_URL = "https://YOUR_PROJECT_ID.supabase.co"
$env:FOCUSLENS_SUPABASE_ANON_KEY = "YOUR_SUPABASE_PUBLISHABLE_KEY"
```

Enable the Google provider in Supabase and add `http://127.0.0.1/callback` (any port) as an allowed redirect URL.

### Publish a local package

```powershell
pwsh scripts/publish.ps1 -SelfContained -Version 1.0.0
# output in dist\, run dist\FocusLens.exe
```

## Releasing

Releases are driven by the [roadmap](ROADMAP.md) phases.

1. Complete the phase and tick its boxes in `ROADMAP.md`.
2. Add `docs/release-notes/vX.Y.0.md` and a `CHANGELOG.md` entry.
3. Push the tag: `git tag v1.0.0; git push origin v1.0.0`.

The `Release` workflow publishes the app and agent, packs a Velopack installer and update feed, and creates the GitHub Release with your notes file. The version comes from the tag.
