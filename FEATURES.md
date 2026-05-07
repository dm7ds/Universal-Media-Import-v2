# UMI v2 — Feature Reference

> How does UMI behave from a user perspective? What can you do, and what happens when you do it?

---

## Multi-Card / Multi-Source

**Plug in everything, UMI does the rest.**

Connect multiple SD cards, USB cameras, and NAS sources simultaneously — UMI detects all of them in parallel and works through imports automatically. No manual waiting or intervention required.

- Multiple card readers at the same time? No problem.
- SD card + USB camera at the same time? No problem.
- Dashcam folder + SD card at the same time? No problem.

Detection runs in parallel. Imports are processed in sequence — one card after the other, automatically. As soon as one import finishes, the next starts immediately. The dashboard shows live progress.

### Card Types

| Type | Behavior |
|------|-----------|
| **Fixed** | Card belongs permanently to one camera — import starts without asking |
| **Floating** | Card switches between cameras — UMI asks (with suggestion from history) |
| **Unknown** | First use — UMI analyzes EXIF and suggests a camera, optional registration |

---

## Drive Detection

UMI uses a hybrid mechanism so that already-mounted cards, hot-plugged cards and card swaps inside an existing reader are all caught:

- **WMI volume events** (`Win32_VolumeChangeEvent`, EventType 2 / 3) — fires for every mount/unmount, including SD swaps inside a permanently connected card reader (where the older `Win32_LogicalDisk` queries used to miss the event)
- **Initial scan at app startup** — emits `DriveArrived` for every removable drive that is already mounted, so consumers (Wizard, ImportViewModel, Watch/Quick CLI) see the current state without waiting for a hot-plug
- **Polling fallback** every 4 s — backstop for card readers whose drivers don't report card swaps to WMI

This logic lives in `DriveWatcherService` (UMI.Core) and is shared between CLI and GUI.

---

## Watch Mode

Continuously running import daemon. Plug in cards, pull them out, insert the next — UMI runs through.

- SD cards detected immediately (event-based via WMI; polling as backstop)
- USB/MTP cameras polled every 5 seconds
- Fixed-path sources (NAS, dashcam) detected via folder watching
- Cards already inserted at startup are also detected
- Duplicate protection: same card is only imported once per session

---

## Quick Mode

Fast event backup without overhead. Same multi-card logic as Watch — plug in everything, everything gets copied.

- No GPS, no sequence detection, no post-processing
- Smart Date: before 08:00 UMI suggests yesterday's date (night shooting belongs to the previous day)

---

## Import Pipeline

What happens when a card is imported:

```
Detection → Scan (read EXIF) → Pre-Processing → Sequence Detection → Copy → Post-Processing
```

- **Scan:** Reads metadata from all files (native, without ExifTool, ~5 ms per file). 4 files scanned in parallel.
- **Pre-Processing:** Modular processors run BEFORE copying (EIS sorting, metadata backup, SRT conversion, GPS preparation)
- **Sequence Detection:** Groups bursts, astro series, and timelapses into subfolders
- **Copy:** Multiple files simultaneously (default: 1 parallel, configurable), live progress in dashboard
- **Post-Processing:** Gyroflow stabilization via GPU Queue, metadata restore, move, cleanup

### Pre/PostProcessor Pattern

Pre- and post-processing runs over registered processors — no monolithic code, no if/else cascades.

**PreProcessors** (run in order BEFORE copying):

| Processor | Order | Condition |
|-----------|-------|-----------|
| `MetadataBackupPreProcessor` | 10 | `features.metadata_backup: true` |
| `SrtConversionPreProcessor` | 15 | `custom_settings.import_srt_sidecars: true` (when SRT sidecars present) |
| `GpsInjectionPreProcessor` | 20 | `features.gps_injection: true` |
| `EisSortingPreProcessor` | 25 | `features.eis_detection: true` |
| `BurstDetectionPreProcessor` | 30 | `features.burst_detection: true` |

**PostProcessors** (run AFTER copying):

| Processor | Condition |
|-----------|-----------|
| `GyroflowPostProcessor` | `features.gyroflow: true`, via GPU Queue |
| `RacerenderPostProcessor` | Stub (not yet production-ready) |

`PreProcessingOrchestrator` and `PostProcessingOrchestrator` orchestrate the chain — new processors are only registered, no core code changed.

### Date Filter

The import filter respects the capture date of media:

- `--date yyyy-MM-dd` limits import to files from a specific day
- UTC-aware: QuickTime `CreateDate` is converted from UTC to local time before comparison (prevents off-by-one for night recordings)
- Applies to all source types (SD, MTP, Fixed-Path)

---

## Sequence Detection

Photos are automatically detected and grouped by capture pattern:

| Profile | Interval | Result |
|---------|-----------|--------|
| Sport/Burst | < 2 s between photos | `Photo/Sport_154111/` |
| Astro | > 10 s between photos | `Photo/Astro_214902/` |
| Timelapse | 2–30 s, evenly spaced | `Photo/Timelapse_120000/` |
| Single shots | Everything else | `Photo/` |

---

## Gyroflow Stabilization

Automatically stabilize shaky action cam videos (Gyroflow must be installed).

- Videos with electronic stabilization (EIS/RockSteady) are automatically detected and skipped
- `--mode all` or `--force` overrides EIS detection
- Multiple videos stabilized in parallel (CPU-dependent, default: core count − 1)

### GPU Task Queue

Gyroflow runs via a persistent GPU Queue — not directly inline.

- **Persistence:** Queue is SQLite-backed (WAL mode), survives app restart and crashes
- **Crash Recovery:** Tasks with status `InProgress` at startup are automatically reset to `Pending` and reprocessed
- **Worker:** Default 1 worker (`max_workers: 1`) — prevents GPU deadlocks during concurrent stabilizations
- **Auto-Start:** Queue starts automatically at app startup (`auto_start: true`)
- **Cleanup:** Completed tasks deleted after configurable time (`purge_completed_after_hours: 24`)
- **Batch Support:** Multiple videos from one import are queued as a batch — batch status aggregates all tasks
- **Event System:** For GUI binding: `TaskStarted`, `TaskProgress` (percent, ETA, frames), `TaskCompleted`, `TaskFailed`, `BatchCompleted`, `QueueEmpty`

**Post-Stabilize per task:** After each successfully stabilized file, automatically runs: Metadata Restore → Move to destination path → Delete temp files (via `IPostProcessingService`).

**Configuration in `config.json`:**

```json
"gpu_queue": {
  "enabled": true,
  "max_workers": 1,
  "auto_start": true,
  "purge_completed_after_hours": 24
}
```

---

## GPS Injection

Automatically embed GPS coordinates from external trackers (GPX files) into videos.

- **Smart Matching:** UMI finds the matching GPS track by timestamp and video duration
- Runs automatically on import when enabled in camera config
- `umi gps` command for retroactive corrections or when tracks arrive later

---

## FolderName

The folder name in the workbench is controlled by `CameraConfig.FolderName`.

- When `folder_name` is set in camera config, it is used as the subfolder name: `Workbench/2026-02-20/MyName/`
- Fallback: CameraID is used when `folder_name` is empty or `null`: `Workbench/2026-02-20/GoPro11/`
- Configurable per camera in GUI (camera card → "Folder Name" field) and directly in `config.json`

```json
"GoPro11": {
  "folder_name": "GoPro",
  ...
}
```

Result: `Workbench/2026-02-20/GoPro/Video/` instead of `Workbench/2026-02-20/GoPro11/Video/`

---

## PostProcess / Color Grading Pipeline

For the workflow: Stabilize → Color Grading (DaVinci Resolve) → GPS → done.

```
1. Import --stabilize     Raw videos → Gyroflow/
2. Process --stabilize    Stabilized → Video/postprocess/
3. DaVinci Resolve        Grading → postprocess/exported/
4. GPS inject             Done → Video/ (stable + graded + GPS)
```

Each step requires the output of the previous. `Video/` contains only finished, fully processed videos at the end.

---

## Source Types

| Source | Detection | Special |
|--------|-----------|---------|
| **SD Card** | Immediate (`Win32_VolumeChangeEvent`) + initial scan | VSN-based camera assignment |
| **MTP/USB** | Polling (5 s) | Direct download, no temp staging |
| **Fixed-Path** | Folder watching | New files only, history tracking |

---

## Folder Structure After Import

```
Workbench/
├── .umi/                           UMI metadata root (all sidecar data)
│   ├── metadata/                   EXIF backups (.meta.json)
│   ├── history/                    Process history (.history.json)
│   ├── gps/                        Optimized GPS tracks (_optimized.gpx)
│   ├── thumbnails/                 Thumbnail cache (.thumb.jpg, .preview.jpg)
│   ├── review/                     Review sidecars (.umi-review.json)
│   └── sequences/                  Sequence sidecars (.umi-sequences.json)
└── 2026-02-20/
    └── GoPro11/
        ├── Video/                  Finished videos
        ├── Gyroflow/               Raw videos (temporary, until stabilized)
        ├── Stabilized/             Stabilized videos
        ├── Photo/                  Single photos
        ├── Photo/Sport_154111/     Burst sequence (timestamp)
        └── Photo/Astro_214902/     Astro sequence
    └── MyDSLR/
        ├── Video/
        └── Photo/
```

---

## Metadata

- **Reading:** Native via Mp4Parser (~5 ms), ExifTool only as fallback
- **Writing:** Always ExifTool (GPS injection, metadata restore)
- **Backup:** `.meta.json` per file under `.umi/metadata/`, restorable after Resolve export

### Metadata Migration

`MetadataMigrationService` migrates older workbenches from the per-date `.metadata/` layout and scattered sidecars to the central `.umi/` structure. Migration runs automatically on startup when the old layout is detected.

### Config Schema Migration

`ConfigMigrator` (UMI.Core.Configuration) is a chained version-step framework that brings older `config.json` documents up to the current schema. The chain is empty until a schema-breaking change ships, but `ConfigWriterService.LoadAsync` already calls into it on every load, writes a `.pre-migration-<oldVersion>` backup of the original document, applies the chain, and re-saves with the migrated `version` field.

---

## Thumbnail Cache Service

`ThumbnailCacheService` extracts and caches thumbnails for RAW files.

- **Supported formats:** CR3, CR2, ARW, NEF, DNG, ORF, RW2, RAF
- **CR3 fast path:** Direct ISOBMFF extraction (<5 ms, no ExifTool). HEIF fallback for HDR PQ CR3 (LibHeifSharp → JPEG)
- **Other RAW:** MetadataExtractor (`ExifThumbnailDirectory`) → ExifTool fallback
- **Cache path:** `{Workbench}/.umi/thumbnails/` — invalidated by `LastWriteTimeUtc` comparison
- **Preview size:** Full PRVW extraction (up to 1080 px for review use)

---

## Archive

`umi archive --project <name>` archives the workbench into a project structure.

- Discovers all date folders in the workbench (yyyy-MM-dd pattern)
- Optional `--include-delivery` flag archives DaVinci Resolve exports too
- Dry-run support via global `--dry-run` flag

---

## Verify

`umi verify` — post-import verification or standalone workbench check.

- **`--post-import`:** Uses `.umi.db` as reference (verifies every copied file is present and intact)
- **Default (workbench scan):** Scans workbench without DB reference — checks file integrity and metadata consistency
- `--source <CameraID|ALL>` limits verification to a specific camera

---

## Update Mechanism

UMI checks the public GitHub release for newer versions and downloads the installer in the background once a release is available.

- **Endpoint:** `https://api.github.com/repos/dm7ds/Universal-Media-Import-v2/releases/latest`
- **Trigger:** Application startup (when `app_settings.check_for_updates_on_startup: true`, the default since 2.1.0)
- **Comparison:** Assembly version (`Major.Minor.Build`) vs. tag name; tags must follow `vX.Y.Z` format
- **Asset selection:** First release asset whose name matches `*Setup*.exe` (i.e. `UMI_Setup_2.1.1.exe`)

### GUI flow

1. Update detected → banner appears in `MainWindow` ("Version X.Y.Z verfügbar — Download läuft im Hintergrund…")
2. Background download into `%TEMP%\UMI_Setup_<version>.exe` with progress reporting via `IProgress<double>`; the banner button switches to **Cancel** while the download runs
3. On completion, banner switches to "Version X.Y.Z is ready to install" with an **Install** button
4. Clicking **Install** launches the staged installer via `ProcessStartInfo { UseShellExecute = true }` (UAC) and shuts down the GUI so the installer can replace the binaries

The auto-download can be turned off in **Settings → Tools** (`check_for_updates_on_startup`).

### CLI flow

```
umi update                # equivalent to --check
umi update --check        # show installed vs. latest, exit
umi update --apply        # check + download + launch installer + exit
```

`UpdateCommand` reuses `IUpdateService` (`GitHubReleaseChecker`) and reports progress on a single overwriting line.

---

## Logging

Tri-state log level configured via **Settings → Tools → Logging**:

| Level | What lands in `logs/umi-gui-*.log` |
|-------|------------------------------------|
| **Off** (default) | Nothing — no log file is created at all |
| **Info** | Standard operational messages |
| **Debug** | Verbose output for development / troubleshooting |

The level is read from `app_settings`-adjacent `logging.level` in `config.json`. The GUI logger is configured before any DI service is resolved (`App.xaml.cs.BuildLogger`) so the log file's first byte already reflects the configured level. Changing the level in the GUI requires a restart.

A separate `--debug` command-line flag opens a developer console with `Verbose` level regardless of the configured `Off`/`Info`/`Debug` value.

---

## GUI — `umi-gui.exe`

WPF desktop application for Windows, implemented in `src/UMI.GUI/` (.NET 8, MVVM).

### Main Tabs

| Tab | Content |
|-----|---------|
| **Import** | Camera selection, date range filter, start/cancel import, live progress |
| **Process** | Post-processing actions in 3 sub-groups (Video / Photo / Tools) |
| **Settings** | Sub-tabs: Cameras / Profiles / Devices / Tools / Burst |

### Process Tab — Sub-Groups

| Sub-Tab | Actions |
|---------|---------|
| **Video** | Stabilize (Gyroflow + GPU Queue), GPS Inject, Restore Metadata, Finalize |
| **Photo** | Sequence Reviewer |
| **Tools** | Sort by Date, Workbench Statistics, Generate Thumbnails |

### Settings Tab — Sub-Tabs

| Index | Tab | Content |
|-------|-----|---------|
| 0 | **Cameras** | Inline expandable camera cards, feature bubbles, FolderName |
| 1 | **Profiles** | Config profile management (save, load, delete) |
| 2 | **Devices** | SD cards, MTP devices, fixed paths — grouped list with inline edit |
| 3 | **Tools** | Tool paths (ExifTool, Gyroflow, FFprobe), language selection, log level, restart wizard |
| 4 | **Burst** | Burst detection profiles, rule editor |

### App Mode

The user-facing label is `Easy / Standard / Advanced`. Internally these map to `app_settings.mode` values `dau / simple / advanced`.

- **Easy / Dau:** Minimalist — only the essentials visible
- **Standard / Simple:** Balanced feature display (default)
- **Advanced:** All features and options visible

Switched via the ComboBox in the toolbar.

### Date Range Filter (GUI)

Available in Import tab and Process tab:

- Calendar popup with from/to date + time input
- Quick filters: "Today", "Yesterday" — set date range with one click
- Active filter shown as chip (e.g. "20.02.26 08:00 – 21.02.26 23:59")
- Clear filter button when active
- Reusable `DateFilterViewModel` — identical in both tabs

### Camera Setup (Settings → Cameras Tab)

- Click camera card → expands inline (no split screen)
- Feature bubbles as clickable toggles: colored = active, dark background = inactive, no border
- FolderName field controls folder name on import
- Registered SD cards per camera: viewable and manageable

---

## Sequence Reviewer

Full-screen window for reviewing, rating, and tagging burst sequences.

- **Overview Grid:** Thumbnail grid of all sequences in a folder, filterable (All / Rated / Unrated) plus optional Tag/Rating/Date/Camera filters that further hide non-matching sequences
- **Filmstrip:** Per-sequence photo strip with lazy thumbnail loading
- **Rating:** Star rating per sequence (persisted to `.umi/review/.umi-review.json`)
- **Tagging:** Free-text tags per photo and per sequence
- **Cross-sequence filter aggregation:** When a Tag or Rating filter is active, photos from all sequences in the current profile are shown together so favourites/ratings are visible globally
- **Bulk actions:** All copy/move/delete logic is bundled in the **Export…** dialog (no separate Copy/Move buttons in the toolbar)
- **Sidecar persistence:** Review data saved as `.umi-review.json` under `.umi/review/`
- **Sequence sidecars:** Sequence grouping data in `.umi/sequences/.umi-sequences.json`
- Launched from the **Process → Photo** sub-tab action card

---

## Burst Visualizer V2

Full-screen panel for visualizing and optimizing burst detection profiles.

- **Thumbnail Grid:** Resizable grid of all photos in a folder, loaded via `IBurstVisualizerService`
- **Profile Evaluation:** Evaluates the selected burst profile against loaded photos, color-coded per sequence
- **Auto-Preset Generator:** Multi-series via Median-Clustering (`SeriesBoundaryFactor = 10.0`) — generates `MaxGapSeconds` and `MinCount` from a user selection
- **Debounced re-evaluation:** Profile change → 300 ms debounce → re-evaluate
- **Accept Preset:** Saves generated preset via `BurstProfileLoader` and notifies `BurstTabViewModel`
- Launched from the **Settings → Burst** tab

---

## Profiles Command / Profiles Tab

`umi profiles list|show|delete` — manage config profiles.

GUI: Settings → Profiles tab shows saved profiles with load/save/delete.

---

## EXIF Scan Command

`umi exif-scan <path>` — analyze EXIF fields in a photo folder.

- Recursive folder scan
- Shows which fields are present across all images
- Output formats: `table` (default) or `json`

---

## Setup Wizard

First-run wizard that guides users through initial configuration.

- **Auto-start:** Wizard launches automatically when no `config.json` exists
- **Manual restart:** "Restart Setup Wizard" button in Settings → Tools tab
- **9 Steps:** Welcome (mode selection) → Workbench → Source Detection → Camera Confirm → Add Cards → More Cameras? → Tools → GPS → Summary (Tools and GPS are skipped in Easy mode)
- **Auto SD detect:** Live SD card detection in the Source Detection step, including initial scan for already-mounted readers
- **Camera Model Database:** `config/camera-models.json` with 180+ entries — Make/Model → CameraType lookup (exact model match, make rules, volume label rules)
- **AppMode selection:** Welcome step lets the user choose Easy / Standard / Advanced
- **Setup language hand-off:** Inno Setup writes `{app}\config\install-language.txt` after install; on first run the GUI reads this hint, applies the matching UI culture, and persists the language to the new `config.json` so the wizard runs in the same language the user picked in the installer

---

## i18n

Full English + German localization.

- **3 .resx pairs:** GUI (700+ keys), CLI (367 keys), Core (12 keys) — EN + DE parity
- **XAML:** `{helpers:Localize Key}` extension
- **Language setting:** `AppSettings.Language` (default `"en"`), Language dropdown in **Settings → Tools**
- **Feature labels:** Dynamic via `CoreStrings`

---

## Help System

Context-sensitive in-app help window.

- **MdXaml:** Markdown rendering in WPF (`MdXaml` library)
- **F1:** Opens help at the current context anchor
- **Context-aware:** Each tab/sub-tab has its own anchor (e.g. `Help_AnchorDevices`, `Help_AnchorBurst`, `Help_AnchorSequenceReviewer`)
- **Languages:** EN + DE help content under `docs/help/{en,de}/`

---

## Installer

Inno Setup 6 script under `installer/umi-setup.iss`. Produces `UMI_Setup_<version>.exe`.

- **.NET 8 detection:** Reads `sharedfx\Microsoft.WindowsDesktop.App` and `sharedfx\Microsoft.NETCore.App` subkey names under both `HKLM64` and `HKLM32` (the .NET runtime msi writes to either depending on the variant). If no 8.x runtime is present, the installer downloads `https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe` via PowerShell and installs it silently
- **Templates-only config staging:** `build.ps1` stages `publish/config-clean/` from `git ls-files config/` before invoking ISCC, so user-side `config.json`, `*.bak` and locally created Gyroflow presets never end up in the released setup
- **Uninstall config prompt:** `InitializeUninstall()` shows a YES/NO dialog ("Remove configuration?", default = No) so users can keep their cameras + SD card registry across reinstalls
- **Setup language hand-off:** the chosen Inno Setup language (`german` / `english`) is written to `{app}\config\install-language.txt`; UMI consumes the hint on first run (see Setup Wizard)
- **Uninstall display:** `UninstallDisplayName` set explicitly, the legacy "UMI deinstallieren" start-menu shortcut omitted (Apps & Features is the standard Windows path), `UninstallDisplayIcon` points at `{app}\umi-icon.ico`
- **Wizard images:** Both `WizardImageFile` (164×314) and `WizardSmallImageFile` (55×58) are regenerated from `installer/assets/umi-icon.ico` via `scripts/regenerate-wizard-bitmaps.ps1` (white background, centered logo, "Universal Media / Import" text below on the large panel)
- **Post-install launch:** Optional checkbox on the Finished page (`runascurrentuser` flag — launches the GUI as the logged-in user, not as admin)

---

## Two-Repo Release Setup

UMI is developed in a private repo and published as snapshot releases to a public repo:

| Repo | Visibility | Purpose |
|------|-----------|---------|
| `dm7ds/Universal-Media-Import-v2-dev` | private | day-to-day commits, internal task cards, archived specs, framework notes |
| `dm7ds/Universal-Media-Import-v2` | public | clean source snapshot per release, GitHub Actions builds and releases the installer |

`scripts/publish-to-public.ps1` mirrors an allow-listed file set from the dev repo (no `.archive/`, `.tasks/`, `.claude/`, `AGENTS.md`, `CLAUDE.md`, `umi-orchestrator.md`, `ARCHITECTURE.md`, internal helper scripts, side projects). It clones the public repo into a scratch directory, wipes the tracked tree, copies in the filtered set, commits as `Release v<version>` and pushes a matching `v<version>` tag. The public-repo CI (`.github/workflows/release.yml`) reacts to the tag push, builds slim/portable/installer artifacts and creates a GitHub Release.

The version is the single source of truth — `Directory.Build.props` propagates it to both `csproj` files via MSBuild inheritance, and `build.ps1` reads the same value when invoking ISCC. Bumping is one line.

---

## Code Signing

The release pipeline is wired for [SignPath Foundation](https://signpath.org/)'s free OSS code signing programme. Workflow steps in `release.yml` are gated behind a `SIGNPATH_ENABLED` repository variable so they stay dormant until the project's application is approved. Once approved, setting four variables and one secret is enough to make every tag-driven build produce a signed installer end-to-end. The required policy lives at `docs/CODE_SIGNING_POLICY.md`.

---

## Version

Single source of truth: `Directory.Build.props` at the repo root.

```xml
<Version>2.1.1</Version>
<AssemblyVersion>$(Version).0</AssemblyVersion>
<FileVersion>$(Version).0</FileVersion>
<InformationalVersion>$(Version)</InformationalVersion>
```

Both `UMI.CLI.csproj` and `UMI.GUI.csproj` inherit these properties via MSBuild's parent-directory walk. `build.ps1` reads `<Version>` from the same file when it passes `/DMyAppVersion=` to ISCC. The GUI's header pill, About dialog and update checker all read the assembly version at runtime — bump in one place, the rest follows.

---

## Architecture

### Project Structure

```
UMI.Core       Business logic, services, interfaces (NO CLI dependencies)
UMI.Cameras    Camera handlers (config-driven)
UMI.Data       SQLite abstraction (Dapper)
UMI.CLI        CLI frontend (System.CommandLine, Spectre.Console)
UMI.GUI        WPF frontend (.NET 8, MVVM)
```

UMI.Core has **no dependency** on System.CommandLine, Spectre.Console, or Console. All business logic is decoupled from CLI and GUI.

### Frontend Interfaces

| Interface | Purpose | CLI Implementation | GUI Implementation |
|-----------|---------|--------------------|--------------------|
| `IProgressReporter` | Live progress (scan, copy, stabilize) | `SimpleProgressReporter`, `LogProgressReporter` | `WpfProgressReporter` |
| `IWizardRenderer` | CLI setup/camera wizards (multi-step dialogs) | `SpectreWizardRenderer` | N/A — GUI uses own `SetupWizardViewModel` + `SetupWizardWindow` |

### Further Patterns

- **`IProgress<T>`:** `ImportProgress`, `CopyProgress`, `ScanProgress` — ready-made progress types
- **CancellationToken:** End-to-end through ExifTool/FFprobe/Gyroflow — cancel works immediately
- **DI (`ServiceCollection`):** All services registered via interfaces
- **`ConfigWriterService`:** `SaveAsync()` for config dialogs, `LoadAsync()` triggers `ConfigMigrator` when needed
- **Async/Await:** Throughout — no UI thread blocking

### Parallelism Summary

| Operation | Parallelism |
|-----------|-------------|
| Scan (EXIF read) | 4 parallel (`Parallel.ForEachAsync`) |
| Copy | Default 1, configurable (`SemaphoreSlim`) |
| Gyroflow stabilization | CPU count − 1 workers |
| GPU Queue workers | Default 1 (`max_workers: 1`) |
| Burst Visualizer thumbnail load | 4 parallel |

---

*Last updated: 2026-05-07 | v2.1.1*
