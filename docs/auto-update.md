# Auto update (Windows)

FocusLens checks GitHub Releases for a newer version, tells the user, and downloads only after they agree. Updates are handled by [Velopack](https://velopack.io).

## What the user sees

1. About 30 seconds after launch, and then every 4 hours, the app checks the latest GitHub release. Checking never downloads anything.
2. If a newer version exists, a tray notification says so. Clicking it opens **Settings, Updates**. The tray menu item changes to **Update available (vX)**.
3. The Updates tab shows the release notes under **What's new**. The user clicks **Download update** and a progress bar shows the download.
4. When the download finishes the tab offers **Restart and update**. The tray item reads **Restart to update (vX)**.
5. On restart the tracking agent is stopped, Velopack swaps versions, and the app relaunches and starts the new agent.

If the user quits without restarting, a downloaded update is applied when the app exits.

Updates only work in the installed app. A run from source or a portable build shows "Updates are only available in the installed app."

## How it works

| Piece | File |
|---|---|
| Checks, downloads, applies. Raises `StateChanged` | `src/FocusLens.App/Services/UpdateService.cs` |
| State record and `UpdateStatus` enum | `src/FocusLens.App/Services/UpdateState.cs` |
| Updates tab view model | `src/FocusLens.App/ViewModels/Settings/UpdatesSettingsViewModel.cs` |
| Updates tab view | `src/FocusLens.App/Views/Settings/UpdatesSettingsPanel.xaml` |
| Tray menu text, toast, and stop-agent-then-restart | `src/FocusLens.App/App.xaml.cs`, `Services/TrayIconService.cs` |
| Velopack install hooks | `src/FocusLens.App/Program.cs` |

`UpdateStatus` values: `Idle`, `Checking`, `UpToDate`, `Available`, `Downloading`, `Ready`, `Failed`. A failed download returns to `Available` with `Error` set, so the user can retry. The tray toast is shown once per release, and not again after a failed download.

The background loop stops checking once a release has been found, so a newer release published afterwards is picked up on the next launch or a manual **Check for updates**.

The feed is `https://github.com/workvar/focuslens-win`. Change `RepoUrl` in `UpdateService.cs` if the repository moves.

## Publishing an update

1. Add `docs/release-notes/vX.Y.Z.md` and a `CHANGELOG.md` entry.
2. Push the tag: `git tag vX.Y.Z && git push origin vX.Y.Z`. The version comes from the tag.
3. The `Release` workflow publishes the app and agent, packs the installer and update feed with `vpk pack`, and creates the GitHub release.

The workflow passes the same `docs/release-notes/vX.Y.Z.md` to `vpk pack --releaseNotes`, which is what fills **What's new** in the app. Pre-release tags such as `1.0.0-beta.1` reuse the notes for `1.0.0`. If no notes file exists the section is hidden.

## Testing an update

1. Publish `v1.0.0-beta.1` and install it with the installer from the release.
2. Publish `v1.0.0-beta.2`.
3. In the installed beta.1, open Settings, Updates, and click **Check for updates**. It should report beta.2 with its notes.
4. Click **Download update**, then **Restart and update**, and confirm the app relaunches on beta.2 with the agent running.

## Troubleshooting

- **"Only available in the installed app":** run the installer rather than the build output.
- **"Could not reach GitHub":** check the network and that the latest release has the Velopack files (`RELEASES`, `.nupkg`, `Setup.exe`) attached.
- **What's new is empty:** the release was packed without a notes file for that version.
- Errors are written to the app log (`Update check failed`, `Update download failed`).
