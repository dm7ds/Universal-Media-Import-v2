# Changelog

All notable user-visible changes are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
loosely tracks [Semantic Versioning](https://semver.org/).

## [2.1.2] — 2026-05-10

### Fixed

- **Wizard cameras now have file types.** Cameras created through the Setup
  Wizard were saved with empty `file_types.photo` / `file_types.video` arrays
  because the wizard called `BuildFromPreset(null)` instead of looking up the
  selected camera type preset. Result on the user side: imports said *"keine
  Dateien gefunden"* even though CR3/DNG/ARW files were sitting in the source
  folder. The wizard now resolves the type preset
  (`config/presets/types/<Type>.umi`) and seeds Features + FileTypes from it.
- **Auto-repair for previously broken cameras.** A startup pass on every
  config load (`MainViewModel.LoadAsync`) detects cameras whose `file_types`
  arrays are empty, backfills them from their type preset and saves. The
  repair is logged at *Info* level so it shows up in the user's log file.
- **Config now lives under `%LOCALAPPDATA%\UMI\config\`.** Previously it sat
  under `Program Files\UMI\config\`, where non-elevated UMI hit
  `UnauthorizedAccessException` on `config.json.bak` (and sometimes on
  `config.json` itself). The path resolver moved to LocalAppData and runs a
  one-time bootstrap that copies the existing config + presets + profiles
  from the install dir on first launch — the legacy directory is left alone.
- **Backup failures are no longer fatal.** `ConfigWriterService.SaveAsync`
  used to call `CreateBackupAsync` first and let an `UnauthorizedAccessException`
  on the `.bak` copy abort the entire save. The backup is now best-effort:
  failure is captured in `LastBackupError` and the live config still gets
  written. The error surfaces in the import summary's collapsible issues area.
- **Per-file scan errors don't kill the batch.** A user with ~12k files in
  one folder reported UMI crashing during the scan phase. Root cause:
  `MetadataReader.ReadPhotoMetadata` let `MetadataExtractor` throw out of
  the `Parallel.ForEachAsync` loop on the first unreadable file, killing
  the rest. The reader now falls back to a filesystem-only `PhotoMetadata`
  on any read error, and the scan loop wraps each file in its own try/catch.
  Skipped files are listed as a collapsible warning section on the camera
  card (`<rel-path> (<ExceptionType>: <message>)`).
- **Devices tab: deleting a fixed path actually clears the camera config.**
  Previously `ExecuteConfirmDelete` removed the entry from the UI list but
  left `source_type=fixed_path` and `source_path` set on the camera, so the
  next `RefreshStorageSummary` re-added the path on the import card. Now
  resets `source_type=sd_card` and `source_path=null` via `UpdateCamera`.
- **Setup wizard finish surfaces errors.** `SetupWizardViewModel.ExecuteFinish`
  was an `async void` with no catch; any exception in `FinishAsync` bubbled
  up to the global `DispatcherUnhandledException` (which sets `Handled=true`),
  so a click on **Fertigstellen** with default `LogLevel=Off` looked like
  it did nothing. Wraps the await and shows a message box with type, message
  and stack trace when something goes wrong.

### Added

- **Build identity in every log file.** GUI and CLI startup banners now
  emit the running version, the short git commit and the UTC build date,
  e.g.

  ```
  =========================================================
  UMI v2.1.2 (commit 9fd99a1, built 2026-05-10T15:00:26Z)
  Runtime: .NET 8.0.x on Microsoft Windows 10.0.xxxxx
  ...
  =========================================================
  ```

  Captured at compile time via an MSBuild target in
  `Directory.Build.props` that calls `git rev-parse --short HEAD` and
  stamps `AssemblyMetadata` attributes onto the produced assembly.
  `UMI.Core.Utilities.BuildInfo` reads them at runtime.
- **Collapsible "issues" area on the camera card.** Below the import
  result line, a `Expander` appears (only when there is something to
  show) with a count badge and a scrollable list of skipped files. The
  optional config-backup error renders as a one-liner at the top of
  the list.

## [2.1.1] — 2026-05-07

Initial public release on
[`dm7ds/Universal-Media-Import-v2`](https://github.com/dm7ds/Universal-Media-Import-v2).

### Highlights

- Tri-state logging (Off / Info / Debug, default Off)
- Background-download update flow with one-click installer launch
- Two-repo release setup: dev mirror via
  [`scripts/publish-to-public.ps1`](scripts/publish-to-public.ps1)
- Inno Setup installer with .NET 8 runtime auto-install, language hand-off
  to the GUI, uninstall config-keep prompt, white wizard-panel bitmaps
- DriveWatcher uses `Win32_VolumeChangeEvent` and emits an initial scan
  for already-mounted drives
- ConfigMigrator skeleton ready for future schema changes
- Code-signing policy and dormant SignPath workflow integration
