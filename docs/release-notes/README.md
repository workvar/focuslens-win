# Release notes

One file per release, named `vX.Y.Z.md`. The release workflow publishes `docs/release-notes/v<version>.md` as the GitHub Release body. If the file is missing it falls back to auto-generated notes.

## Phase to version map

| Phase | Version | Notes file |
|---|---|---|
| 1 Foundations | 1.0.0 | [v1.0.0.md](v1.0.0.md) |
| 2 Memory and polish | 1.1.0 | [v1.1.0.md](v1.1.0.md) (draft, phase not complete) |
| 3 Focus tools | 1.2.0 | [v1.2.0.md](v1.2.0.md) (draft, not yet verified on a PC) |
| 4 Intelligence | 1.3.0 | to be written when the phase completes |

Patch releases (1.0.1, 1.0.2) carry fixes only and get a short notes file of their own. Pre-release tags such as `v1.0.0-beta.1` reuse the notes of their base version.

## Template

```markdown
# FocusLens vX.Y.0: <Phase name>

One sentence on what this release is about.

## Highlights
- ...

## Also in this release
- ...

## Known issues
- ...

## Requirements
- Windows 10 version 2004 (build 19041) or later, 64-bit

## Upgrading
Existing installs update automatically in the background and apply the update on restart.
```

Rules: describe what the user can now do, not which files changed; list known issues honestly; do not use unreleased items.

## Test builds

Tags before 1.0.0 were cut to test the release pipeline and the updater. Each has its own notes file.

| Tag | Notes file | What it is |
|---|---|---|
| v0.0.1 | [v0.0.1.md](v0.0.1.md) | First build with Velopack auto-update |
| v0.0.2 | [v0.0.2.md](v0.0.2.md) | Same code as v0.0.1 (see note below) |
| v0.0.3 | [v0.0.3.md](v0.0.3.md) | Dark mode and dashboard fixes |
| v0.0.4 | [v0.0.4.md](v0.0.4.md) | Readable controls, recording controls, Permissions, Local tools setup |

The v0.0.2 tag points at the same commit as v0.0.1, so `v0.0.2.md` describes changes that first shipped in v0.0.3.
