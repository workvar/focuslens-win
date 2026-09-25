# Changelog

All notable changes to FocusLens for Windows are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/). Planned work is tracked in [ROADMAP.md](ROADMAP.md).

## [Unreleased]

Working towards **v1.0.0** (Phase 1).

### Added
- Ollama as an AI provider, alongside Claude and OpenAI, with streaming replies.
- Screen context: active-window capture with on-device Windows OCR. Images are not kept.
- Meeting detection, audio capture, offline transcription, and AI meeting summaries.
- Velopack auto-update from GitHub Releases and a tag-driven GitHub Actions release pipeline.
- Google sign-in through Supabase (PKCE with a loopback redirect).

### Changed
- Visual refresh toward the forest theme, with light and dark themes.
- Upgraded to .NET 10.
