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

### Fixed
- Chat now finds older activity: it searches the newest on-screen text plus keyword matches across 14 days, instead of only the oldest 1500 snapshots in the last 7 days.
- Chat no longer repeats earlier answers: history is limited to the last few real turns and the current question is not sent twice.
- Chat apps built on WebView2 or Electron (such as WhatsApp) are asked to expose their text to UI Automation, and list items are read as messages.
- When nothing matches, the answer says what was searched instead of a generic app summary.

### Changed
- Visual refresh toward the forest theme, with light and dark themes.
- Upgraded to .NET 10.
