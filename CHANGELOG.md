# Changelog

All notable user-visible changes are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
loosely tracks [Semantic Versioning](https://semver.org/).

## [2.1.6] — 2026-06-02

### Fixed

- **Log-Pfad: Logs lagen im nicht-schreibbaren Installationsverzeichnis (TASK-216).**
  GUI- und CLI-Logger schrieben bisher nach `<install-dir>/logs/` — bei
  Installation in `C:\Program Files\` kein Schreibzugriff für normale Nutzer,
  Serilog schluckte den Fehler still → keine Logs obwohl Level auf Debug stand.
  
  Neuer SSOT-Pfad via `ConfigPathResolver.LogDirectory`:
  - Primär: `%LOCALAPPDATA%\UMI\logs\` (user-schreibbar, konsistent mit
    Config-Ablage seit v2.1.2)
  - Fallback: `<exe-dir>\logs\` nur wenn LocalAppData nicht schreibbar
  - Wenn beide nicht schreibbar: Console.Error-Warnung + Debug-Output statt
    stillschweigendem Versagen
  
  Alte Logs in `C:\Program Files\UMI\logs\` bleiben unberührt (kein
  Auto-Delete, kein Auto-Move).

### Added

- **Support-Bundle-Button in Einstellungen → Tools (TASK-216).**
  Neuer Button "Support-Paket erstellen" (immer sichtbar, nicht nur bei Debug).
  Öffnet Ordner-Picker, erzeugt `umi-support-<Timestamp>.zip` mit:
  - Bis zu 10 neuesten `*.log`-Dateien aus `%LOCALAPPDATA%\UMI\logs\`
  - `config.json` (ohne Backup)
  - `bundle-info.txt` mit Version, OS, Log-Level, Workbench-Pfad, Kamera-Anzahl
  
  Fehlende Dateien stoppen das Bundle nicht — sie werden als Hinweis in
  `bundle-info.txt` vermerkt. Nach Erstellen: Explorer öffnet automatisch
  den Zielordner.

## [2.1.5] — 2026-06-02

### Fixed

- **Burst-Detection erzeugte keine Sport_HHmmss-Ordner auf Disk (TASK-215).**
  Wenn der Burst-Toggle in der Camera-Card aktiviert wurde, schrieb das
  ViewModel nur `features.burst_detection=true`, aber
  `burst_detection_config.active_profiles` blieb leer. Damit lud
  `BurstProfileLoader` 0 Profile → `SequenceGroupingService` markierte keine
  Gruppe als Sequenz → keine `<Mode>_HHmmss/`-Unterordner beim
  Importieren oder "Organisieren".
  
  **Fixes in dieser Version:**
  1. **Startup-Repair-Pass:** `RepairEmptyCameraBurstConfigAsync` läuft beim
     App-Start nach dem FileTypes-Repair-Pass. Kameras mit
     `BurstDetection=true` und leeren `ActiveProfiles` werden automatisch mit
     den Profil-Defaults aus dem Kamera-Typ-Preset (oder allen verfügbaren
     Profilen als Fallback) befüllt. Config wird gespeichert, idempotent.
  2. **Defensive LoadBurstConfig (ImportPipelineService + FolderSortService):**
     Wenn `ActiveProfiles` leer ist aber Profile auf Disk vorhanden sind,
     werden alle verfügbaren Profile als Fallback geladen statt still 0 Profile
     zu übergeben.
  3. **_unsorted-Path-Fix in FolderSortService:** Dateien in Ordnern die
     keiner registrierten Kamera zugeordnet sind (`_unsorted`), bekommen nun
     eine On-the-fly-BurstDetectionConfig mit allen verfügbaren Profilen.
     Vorher: kompletter Skip der Burst-Erkennung für diese Dateien.
  4. **Kamera-Anlegen-Pfade (Wizard + Add-Camera-Dialog):** Neue Kameras mit
     BurstDetection=true bekommen beim Anlegen sofort
     `BurstDetectionConfig.ActiveProfiles` aus dem Typ-Preset
     (`default_burst_profiles`), kein leeres Array mehr.
  5. **Type-Presets:** `Mirrorless.umi` und `DSLR.umi` haben nun
     `default_burst_profiles: ["Sport", "Astro", "HighISO", "Timelapse"]`.

## [2.1.4] — 2026-05-10

### Removed

- **LensCorrection-Feature aus dem Camera-Card entfernt.** Der Toggle hatte
  kein Backend — Tooltip war ehrlich mit "(coming soon)" markiert, aber UMI
  ist nicht der richtige Ort dafür. Lightroom macht das beim Import. Configs
  mit `features.lens_correction=true` bleiben gültig (Schema-kompatibel),
  das Feld wird nur nicht mehr im UI angeboten.
- **Racerender-PostProcessor-Stub aus der DI-Registration entfernt.** Der
  Stub bleibt im Code für eine eventuelle spätere Integration, läuft aber
  nicht mehr automatisch — kein Stub-Log mehr im Default-Betrieb.

## [2.1.3] — 2026-05-10

### Added

- **GUI Process tab → "Organisieren" jetzt voll funktional.** Die Action-Karte
  war bisher ein Stub mit *"In der GUI noch nicht verfügbar"*. Jetzt sortiert
  sie den Workbench nach EXIF-Datum in `yyyy-MM-dd/{Kamera}/{Typ}/`-Bäume —
  dieselbe Logik wie `umi process --sort full` auf der CLI. Status,
  Fortschritt und Fehlerliste landen auf der Karte.
- **Retroaktive Fotoserien-Erkennung.** Der Toggle *"Fotoserien erkennen"*
  auf der Organisieren-Karte triggert eine Burst-Detection auf dem schon
  importierten Workbench. Photos die zu einem Burst-Profil passen (Sport,
  Astro, Timelapse, …) wandern in `<Mode>_HHmmss/`-Unterordner innerhalb
  ihres Photo-Zielordners — exakt wie beim Import.
- **`IFolderSortService` als SSOT für Sort-Logik.** Die Sort-Logik lebt jetzt
  in `UMI.Core/Services/FolderSortService.cs`. CLI (`process --sort`) und
  GUI rufen denselben Service auf — keine Doppelimplementierung mehr.
  3 neue Smoke-Tests in `UMI.Core.Tests`.

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
