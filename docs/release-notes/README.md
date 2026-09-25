# Release notes

One file per release, named `vX.Y.Z.md`. The release workflow publishes `docs/release-notes/v<version>.md` as the GitHub Release body. If the file is missing it falls back to auto-generated notes.

## Phase to version map

| Phase | Version | Notes file |
|---|---|---|
| 1 Foundations | 1.0.0 | [v1.0.0.md](v1.0.0.md) |
| 2 Memory and polish | 1.1.0 | to be written when the phase completes |
| 3 Focus tools | 1.2.0 | to be written when the phase completes |
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
