# FocusLens Roadmap (Windows)

FocusLens ships in four phases. Each phase maps to one minor version and one GitHub Release, and each release publishes the matching file in [`docs/release-notes/`](docs/release-notes/).

Last reviewed: 2026-09-25. The macOS app follows the same schedule in [focuslens](https://github.com/workvar/focuslens).

## Schedule at a glance

| Phase | Theme | Version | Status |
|---|---|---|---|
| 1 | Foundations: local AI, capture, meetings, redesign, updates | v1.0.0 | In progress (2 of 6 done) |
| 2 | Memory and polish: Chroma DB, UI and UX fixes | v1.1.0 | Planned |
| 3 | Focus tools: goal tracker, distraction blocker, PPT generation | v1.2.0 | Planned |
| 4 | Intelligence: session grouping, adaptive focus, professional metrics | v1.3.0 | Planned |

A phase is released when every box in it is checked. Work-in-progress builds can go out earlier as pre-releases (for example `v1.0.0-beta.1`), which the workflow marks as pre-release automatically.

## Phase 1: Foundations (v1.0.0)

- [x] Connects to Ollama
- [x] Captures screen context
- [ ] Audio recording
- [ ] Meeting recording
- [ ] Redesign on the forest theme
- [ ] Auto updater and GitHub Actions

Repo status on Windows:

| Item | Where it lives | State |
|---|---|---|
| Ollama | `FocusLens.Core/Ai/Streams/OllamaStream.cs`, `Health/LlmProbe.cs`, AI settings panel | Done |
| Screen context | `Platform.Windows/Ocr/` (Windows.Media.Ocr), `Core/Context/` | Done |
| Audio recording | `Platform.Windows/Audio/` (audio session monitor, chunked WAV writer, capture) | Code written, needs verification |
| Meeting recording | `Core/Meetings/`, `SystemSpeechTranscriber.cs`, `MeetingSummarizer.cs` | Code written, needs verification |
| Forest redesign | `App/Themes/` (light and dark dictionaries), `ThemeManager.cs`; tokens in the project doc `design/forest-theme-tokens.md` | Partly applied |
| Auto updater and Actions | Velopack (`Services/UpdateService.cs`), `.github/workflows/release.yml`, `scripts/publish.ps1` | Wired, no release cut yet |

## Phase 2: Memory and polish (v1.1.0)

- [ ] Chroma DB integration
- [ ] UI fixes
- [ ] UX fixes

Repo status: the Chroma index is wired into the app: background indexing, semantic matches in AI chat, and a Memory settings tab. Scripts live in `src/FocusLens.Core/Resources/chroma/`. Docs and tests are still to do.

## Phase 3: Focus tools (v1.2.0)

- [ ] Focus tracker (set a goal)
- [ ] Distraction blocker (act, nudge, protect)
- [ ] PPT generation

## Phase 4: Intelligence (v1.3.0)

- [ ] Grouping of session contexts
- [ ] Adaptive focus and AI classifiers
- [ ] Professional metrics and scoring

## How the schedule drives releases

1. Tick boxes in this file as items land on `main`.
2. When a phase is complete, write `docs/release-notes/vX.Y.0.md` (copy the template in that folder) and add a `CHANGELOG.md` entry.
3. Push the tag `vX.Y.0`. The release workflow publishes with the notes file and builds the Velopack installer and update feed. The version comes from the tag, so there is no version file to bump. If no notes file exists it falls back to auto-generated notes.
