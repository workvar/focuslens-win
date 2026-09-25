# Changelog

All notable changes to FocusLens for Windows are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/). Planned work is tracked in [ROADMAP.md](ROADMAP.md).

## [Unreleased]

Working towards **v1.0.0** (Phase 1).

### Added
- Ollama as an AI provider, alongside Claude and OpenAI, with streaming replies.
- Screen context: active-window capture with on-device Windows OCR. Images are not kept.
- Meeting detection, audio capture, offline transcription, and AI meeting summaries.
- Velopack auto-update from GitHub Releases and a tag-driven GitHub Actions release pipeline.
- Update prompt: a tray notification announces new releases, and an Updates tab in Settings shows the notes and downloads only after the user agrees. See [docs/auto-update.md](docs/auto-update.md).
- Google sign-in through Supabase (PKCE with a loopback redirect).
- Focus Mode (Phase 3, v1.2.0): set a goal and a length; a patience bar drains while you are off topic, nudges at halfway, then closes the tab or window or blocks it with an overlay. Floating widget, tray timer, allowlist, and saved sessions with insights. See [docs/release-notes/v1.2.0.md](docs/release-notes/v1.2.0.md).
- Focus Mode performance settings: check interval (default 6 s), pause when away or locked, visual effects (Automatic, Full, Minimal), and an optional separate Ollama model for focus checks.
- Per-request AI options (token limit, temperature, reasoning off, Ollama keep-alive and model override), used by Focus Mode for one-word answers.

### Fixed
- Chat now finds older activity: it searches the newest on-screen text plus keyword matches across 14 days, instead of only the oldest 1500 snapshots in the last 7 days.
- Chat no longer repeats earlier answers: history is limited to the last few real turns and the current question is not sent twice.
- Chat apps built on WebView2 or Electron (such as WhatsApp) are asked to expose their text to UI Automation, and list items are read as messages.
- When nothing matches, the answer says what was searched instead of a generic app summary.

### Changed
- Visual refresh toward the forest theme, with light and dark themes.
- Upgraded to .NET 10.
