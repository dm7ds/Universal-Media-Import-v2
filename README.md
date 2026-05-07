# Universal Media Import (UMI)

[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![Latest Release](https://img.shields.io/github/v/release/dm7ds/Universal-Media-Import-v2)](https://github.com/dm7ds/Universal-Media-Import-v2/releases/latest)

Automated media import tool for photographers and videographers. Insert an SD card — UMI detects it, sorts the footage into a clean workbench structure, and optionally injects GPS, stabilizes with Gyroflow, and backs up EXIF metadata. Works with any camera that connects via SD card or USB/MTP: action cams, drones, DSLRs, mirrorless. The included camera profiles are examples and are meant to be adapted to your own hardware.

> **Detailed feature reference:** [FEATURES.md](FEATURES.md)

---

## Features

- **Auto-import daemon** — insert card, import starts automatically (`watch`)
- **Quick backup mode** — fast copy without pipeline overhead, ideal for events (`quick`)
- **Gyroflow stabilization** — persistent GPU queue, crash-safe, survives restarts
- **GPS injection** — matches GPX tracks to videos by timestamp, writes coordinates into metadata
- **Burst detection** — groups RAW sequences (sport, astro, timelapse) into named subfolders
- **EIS detection** — separates videos with electronic stabilization from those ready for Gyroflow
- **Metadata backup / restore** — EXIF backup to `.meta.json`, restore after DaVinci Resolve export
- **Thumbnail generation** — thumbnail cache for RAW files after import
- **Sequence Reviewer** — full-screen tool to rate, tag and export burst/astro/timelapse series
- **MTP / USB import** — direct import from cameras connected via USB (Windows WPD API)
- **Fixed-path sources** — dashcams, NAS drives, any folder watched continuously
- **WPF GUI** — full desktop interface (`umi-gui.exe`) with setup wizard and camera tile management
- **In-app updates** — checks GitHub for new releases on startup, downloads in the background, one-click install
- **Tri-state logging** — Off (default) / Info / Debug, configurable in Settings → Tools
- **i18n** — English and German UI, language auto-applied from the installer's language pick

---

## Installation

### Installer (recommended)

Get the latest installer from [the releases page](https://github.com/dm7ds/Universal-Media-Import-v2/releases/latest) — file name is `UMI_Setup_<version>.exe`.

The installer:
- Checks for .NET 8 Runtime and downloads it if needed
- Installs UMI to `C:\Program Files\UMI\`
- Adds `umi` to PATH (available in any terminal)
- Bundles ExifTool — no separate installation required
- Asks at uninstall time whether to keep or wipe your camera/SD-card configuration (default: keep)

**Requires:** [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (~55 MB, checked by installer)

### Portable

Grab `umi-portable.zip` from the releases page — single self-contained EXE, no installation.

- No .NET required — runtime is embedded
- Ideal for USB sticks, on-location use, or when you cannot install software
- ExifTool must be placed in `tools/exiftool/` next to the EXE (or configured in `config.json`)

**Trade-off:** Larger file (~50 MB compressed), first launch slightly slower (one-time extraction)

---

## Quick Start

### Initial setup

Run the setup wizard to create your `config.json`:

```
umi setup
```

The wizard asks for the workbench path, auto-detects ExifTool and Gyroflow, and optionally configures your first camera. Alternatively, copy `config/defaults/config.default.json` to `config.json` and edit the paths manually.

### Add a camera

To add a camera interactively:

```
umi setup camera
```

### Automatic import (watch mode)

```
umi watch
```

Runs in the background and monitors all drives. Insert any card — UMI detects it and imports automatically. Insert multiple cards at once: they are processed one after another. Stop with `Ctrl+C`.

### One-shot import

```
umi import
umi import --source GoPro11
```

### Quick backup (event mode)

```
umi quick --target "E:\Event\Day1"
```

Copies everything to a target folder without GPS, burst detection, or other pipeline steps. Before 08:00 the previous day's date is proposed as target (night shoots belong to the day before).

---

## CLI Reference

### Global options

These apply to every command:

| Option | Default | Description |
|---|---|---|
| `--config <path>` | `config.json` | Path to config.json |
| `--dry-run` | false | Simulate without file changes |
| `-v, --verbose` | false | Enable debug logging |
| `-q, --quiet` | false | Show errors only |
| `--profile <name>` | — | Config profile from `config/presets/profiles/` |

---

### `setup` — Initial configuration

Starts the interactive first-run wizard: workbench path, tool detection (ExifTool, Gyroflow), and optional camera setup.

```
umi setup
umi setup camera
```

| Subcommand | Description |
|---|---|
| *(none)* | Full first-run wizard |
| `camera` | Add a new camera (interactive wizard) |

---

### `watch` — Automatic import daemon

Monitors all drives continuously. Starts import automatically when an SD card is inserted. Also monitors MTP devices and fixed-path sources.

```
umi watch
umi watch --camera GoPro11
umi watch --stabilize
umi watch --rename-videos
umi watch --gopro-rename
```

| Option | Description |
|---|---|
| `--camera <id>` | Treat every card as this camera (overrides card assignment) |
| `--stabilize` | Run Gyroflow stabilization after each import |
| `--rename-videos` | Add timestamp prefix: `DJI_001.mp4` → `20260220_143022_DJI_001.mp4` |
| `--gopro-rename` | Sortable GoPro names: `GH010042.MP4` → `GoPro_0042_c01.MP4` |

**Card logic:**

| Card type | Behavior |
|---|---|
| Fixed (registered) | Import without prompt |
| Floating (registered) | Prompt with suggestion from usage history |
| Unknown | Prompt with EXIF pre-selection, option to register |
| `--camera` set | Overrides all of the above |

---

### `quick` — Fast backup

Copies everything to a target folder without the full import pipeline. No GPS, no burst detection, no post-processing overhead.

```
umi quick
umi quick --target "E:\Event\Day1"
umi quick --camera GoPro11
umi quick --gopro-rename
umi quick --stabilize
umi quick --no-metadata
```

| Option | Description |
|---|---|
| `--target <path>` | Target folder (skips prompt) |
| `--camera <id>` | Treat all cards as this camera |
| `--gopro-rename` | Sortable GoPro names |
| `--rename-videos` | Timestamp prefix on videos |
| `--stabilize` | Gyroflow stabilization after copy |
| `--no-metadata` | Disable metadata backup |

**Smart date:** Before 08:00, yesterday's date is suggested as the target folder name.

---

### `import` — Manual one-shot import

Single import run instead of a persistent daemon. Useful for targeted imports or ad-hoc folder imports.

```
umi import
umi import --source GoPro11
umi import --source MyDSLR,GoPro11
umi import --type Action,Drone
umi import --source GoPro11 --stabilize
umi import --source GoPro11 --stabilize --mode all
umi import --folder "D:\Vacation\GoPro_Footage"
umi import --folder "D:\Footage" --keep-structure
umi import --source DashCam --full
umi import --source DashCam --reset-history
umi import --source GoPro11 --rename-videos
umi import --source GP1 --gopro-rename
```

| Option | Default | Description |
|---|---|---|
| `--source <id>` | `ALL` | Camera ID(s) comma-separated, or `ALL` |
| `--type <type>` | — | Camera type(s): `Action`, `Drone`, `Mirrorless`, etc. |
| `--stabilize` | false | Gyroflow stabilization after import |
| `--mode <mode>` | `automatic` | `automatic` (skip EIS videos) or `all` (all videos) |
| `--force` | false | Also stabilize videos that have EIS |
| `--no-eis-sort` | false | Disable EIS-based sorting (all videos go to `Video/`) |
| `--full` | false | Ignore import history (for fixed-path sources) |
| `--reset-history` | false | Clear import history, then import normally |
| `--folder <path>` | — | Ad-hoc import from any folder without camera config |
| `--keep-structure` | false | Keep subfolder structure (only with `--folder`) |
| `--rename-videos` | false | Timestamp prefix: `DJI_001.mp4` → `20260220_143022_DJI_001.mp4` |
| `--gopro-rename` | false | Sortable names: `GH010042.MP4` → `GoPro_0042_c01.MP4` |

---

### `gps` — GPS track management

Requires GPS tracks in GPX format (from GPS trackers, navigation devices, or phone apps). UMI matches tracks to videos automatically by timestamp and duration. GPS processing runs automatically during import when `gps_injection: true` is set on the camera. This command is for manual runs or retroactive corrections.

```
umi gps create --date 2026-02-20 --source GoPro11
umi gps inject --date 2026-02-20 --source GoPro11
umi gps verify --date 2026-02-20 --source GoPro11
umi gps inject --source GoPro11,DroneX --force
```

| Subcommand | Description | Options |
|---|---|---|
| `create` | Create GPX files from tracker data / SRT sidecars | `--date`, `--source`, `--force` |
| `inject` | Write GPS coordinates into video metadata | `--date`, `--source`, `--force` |
| `verify` | Tabular overview: GPS status per video | `--date`, `--source` |

---

### `process` — Post-processing

Gyroflow stabilization and sorting for already-imported media. Gyroflow runs via a persistent GPU queue (SQLite-backed) — tasks survive app restarts and are automatically resumed.

```
umi process --source GoPro11 --stabilize
umi process --source GoPro11 --stabilize --mode automatic
umi process --source GoPro11 --stabilize --date 2026-02-20
umi process --path "E:\Event\Day1" --sort full
umi process --path "E:\Event\Day1" --sort date
```

| Option | Default | Description |
|---|---|---|
| `--source <id>` | `ALL` | Camera ID(s) |
| `--stabilize` | false | Gyroflow stabilization via GPU queue |
| `--mode <mode>` | `manual` | `manual` (from `Gyroflow/`) or `automatic` (with EIS detection) |
| `--force` | false | Also stabilize EIS videos |
| `--date <date>` | — | Only process specific date (yyyy-MM-dd) |
| `--path <folder>` | — | Alternative folder instead of workbench |
| `--sort <mode>` | — | `full` (yyyy-MM-dd/Camera/Type/) or `date` (date level only) |

**GPU Queue:** Tasks are managed in a persistent SQLite queue. On crash or restart, running tasks are automatically re-queued and retried on next start.

---

### `verify` — Integrity check

```
umi verify
umi verify --source GoPro11
umi verify --post-import
```

| Option | Description |
|---|---|
| `--source <id>` | Check specific camera only |
| `--post-import` | Check against import DB (existence + size) |

---

### `restore` — Restore metadata

Restores EXIF data from `.meta.json` backups (e.g., after a DaVinci Resolve export overwrites metadata).

```
umi restore
umi restore --source GoPro11 --force
```

| Option | Description |
|---|---|
| `--source <id>` | Restore specific camera only |
| `--force` | Restore even when metadata appears intact |

---

### `archive` — Archive workbench

Moves completed media from the workbench into the project structure.

```
umi archive --project "Iceland_2025"
umi archive --project "Iceland_2025" --include-delivery
```

| Option | Description |
|---|---|
| `--project <name>` | Target project name (required) |
| `--include-delivery` | Also archive delivery exports |

---

### `profiles` — Config profiles

```
umi profiles list
umi profiles show "Action"
umi profiles delete "Action"
```

---

### `exif-scan` — Analyze EXIF fields

Scans photos and shows available EXIF fields. Useful for building burst detection profiles.

```
umi exif-scan --path F:\DCIM
umi exif-scan --path F:\DCIM --category Shooting
umi exif-scan --path F:\DCIM --min-coverage 80
umi exif-scan --path F:\DCIM --format json
```

| Option | Description |
|---|---|
| `--path <path>` | Source path to scan (required) |
| `--format table\|json` | Output format |
| `--category <name>` | Filter to specific EXIF category |
| `--min-coverage <pct>` | Only show fields present in at least N% of files |

---

### `test-camera` — Test camera detection

Detects the camera model from a video file using EXIF/metadata.

```
umi test-camera --video "E:\Workbench\GoPro11\Video\GoPro_0001_c01.mp4"
umi test-camera --video "video.mp4" -v
```

| Option | Description |
|---|---|
| `--video <path>` | Video file to analyze (required) |

---

### `update` — Check for and install updates

Checks the public GitHub release page for a newer version. Optionally downloads
the new installer and launches it via UAC.

```
umi update                # equivalent to --check
umi update --check
umi update --apply
```

| Option | Description |
|---|---|
| `--check` | Show installed version vs. latest available, exit |
| `--apply` | Check, download `UMI_Setup_<version>.exe` to `%TEMP%` and start the installer |

The GUI runs the same check at startup (when *Check for updates on startup* is
enabled in **Settings → Tools**, the default), pre-fetches the installer in the
background and surfaces a one-click **Install** button on the in-app banner.

---

## Configuration

UMI is configured via `config.json`. On first run, `umi setup` creates this file interactively. The default template is at `config/defaults/config.default.json`.

### Minimal example

```json
{
  "cameras": {
    "GoPro11": {
      "name": "GoPro Hero 11 Black",
      "camera_type": "Action",
      "enabled": true,
      "features": {
        "gps_injection": true,
        "gyroflow": true,
        "eis_detection": true,
        "metadata_backup": true,
        "burst_detection": false,
        "lens_correction": false,
        "post_process": false,
        "rename_videos": false,
        "gopro_rename": false,
        "generate_thumbnails": false
      },
      "file_types": {
        "video": [".mp4", ".mov"],
        "photo": [".jpg", ".dng"]
      },
      "paths": {
        "sd_source": "D:\\SDCards\\GoPro11"
      },
      "custom_settings": {
        "gyroflow": {
          "preset": "C:\\Tools\\Gyroflow\\Presets\\ActionCam_Default.gyroflow",
          "gpu_device": "nvidia"
        }
      }
    }
  },
  "global_paths": {
    "workbench": "E:\\Workbench",
    "projects": "E:\\Projects",
    "gpx_source": "C:\\GPS\\GPX",
    "log_directory": "./logs",
    "tools": {
      "exiftool": "tools/exiftool/exiftool.exe",
      "gyroflow": "C:\\Tools\\Gyroflow\\Gyroflow.exe",
      "ffprobe": null
    }
  }
}
```

### Config sections

| Section | Description |
|---|---|
| `cameras` | Camera definitions (ID → CameraConfig) |
| `global_paths` | Workbench, projects, GPX source, log directory, tool paths |
| `metadata_backup` | EXIF backup fields and restore fields |
| `gps_processing` | GPS track optimization, time buffer, validation |
| `gyroflow` | Parallel jobs, core auto-detection, queue strategy, timeout |
| `verification` | Post-import integrity checks |
| `duplicate_handling` | Duplicate detection method and action (skip/rename) |
| `lens_correction` | Lens profile assignments per camera |
| `archiving` | Project structure, auto-cleanup, include-delivery default |
| `logging` | Log level, console/file output |
| `workflow` | make_clean, create_backup, dry_run, ignore_folders |
| `options` | Default CLI flags (gps, stabilize, dry_run, force, no_eis_sort) |
| `layout` | Workbench folder structure (camera_folders, media_folders, sort_order) |
| `app_settings` | GUI language (`"en"` / `"de"`), app mode (`"simple"` / `"advanced"` / `"dau"`) |
| `sd_cards` | Registered SD card registry (auto-populated during import) |
| `mtp_devices` | Registered MTP device registry (auto-populated) |
| `gpu_queue` | GPU queue workers, auto-start, purge schedule |

### `gyroflow` section

```json
"gyroflow": {
  "parallel_enabled": true,
  "parallel_jobs": 3,
  "auto_detect_cores": true,
  "queue_strategy": "largest_first",
  "timeout_minutes": 30
}
```

When `auto_detect_cores` is true, `parallel_jobs` is overridden by `ProcessorCount - 1`. `queue_strategy` controls processing order: `"largest_first"` processes the biggest files first.

### `gpu_queue` section

```json
"gpu_queue": {
  "enabled": true,
  "max_workers": 1,
  "auto_start": true,
  "purge_completed_after_hours": 24
}
```

Default `max_workers: 1` prevents GPU deadlocks when multiple stabilization tasks run in parallel.

### Camera `source_type` values

| Value | Description |
|---|---|
| `sd_card` | SD card via volume serial number matching (default) |
| `mtp` | USB-connected camera via Windows WPD API |
| `fixed_path` | Fixed folder (dashcam, NAS, any always-available path) |

**Fixed-path camera example:**

```json
"DashCam": {
  "name": "Blackvue DR970X",
  "source_type": "fixed_path",
  "source_path": "D:\\DashCam\\Videos",
  "flatten_source": true,
  "camera_type": "Action",
  "enabled": true,
  "file_types": { "video": [".mp4"] }
}
```

---

## Camera features

Each camera has a `features` block with the following keys:

| Key | Description |
|---|---|
| `gps_injection` | Match GPX tracks to videos and inject GPS into metadata |
| `gyroflow` | Gyroflow stabilization (copies to `Gyroflow/`, output to `Stabilized/`) |
| `burst_detection` | Group RAW sequences into named subfolders (sport, astro, timelapse) |
| `metadata_backup` | Back up EXIF fields to `.meta.json` sidecar files |
| `eis_detection` | Detect electronic stabilization and sort accordingly |
| `lens_correction` | Apply lens correction profile (stub — sidecar only) |
| `post_process` | Post-processing pipeline: `Gyroflow/` → `Stabilized/` → DaVinci Resolve |
| `rename_videos` | Add timestamp prefix to video filenames |
| `gopro_rename` | Rename GoPro chaptered files to sortable format |
| `generate_thumbnails` | Generate thumbnail cache for RAW files after import |

---

## Workbench folder structure

After import the workbench looks like this:

```
Workbench/
├── .umi/                          UMI internal data (never edit manually)
│   ├── metadata/                  EXIF backups (.meta.json per file)
│   ├── history/                   Import history (.history.json per file)
│   ├── gps/                       Optimized GPS tracks (_optimized.gpx)
│   ├── thumbnails/                Thumbnail cache (.thumb.jpg, .preview.jpg)
│   ├── review/                    Review sidecars (.umi-review.json per folder)
│   └── sequences/                 Sequence sidecars (.umi-sequences.json per folder)
│
├── 2026-02-20/
│   ├── GoPro11/
│   │   ├── Video/                 Finished videos (EIS-on or already stabilized)
│   │   ├── Gyroflow/              Videos waiting for stabilization
│   │   ├── Stabilized/            Stabilized output from Gyroflow
│   │   └── Photo/
│   │
│   ├── MyDSLR/
│   │   ├── Video/
│   │   └── Photo/
│   │       ├── Sport_154111/      Burst sequence (sport)
│   │       ├── Astro_214902/      Burst sequence (astro)
│   │       └── Single_Shots/      Individual frames
│   │
│   └── DroneX/
│       ├── Video/
│       └── Photo/
│
└── 2026-02-21/
    └── ...
```

### PostProcess pipeline (optional)

When both `gyroflow` and `post_process` features are enabled, videos follow this extended path:

```
2026-02-20/GoPro11/
  Gyroflow/                        Raw — waiting for stabilization
  Stabilized/                      Gyroflow output
  Video/postprocess/               Waiting for DaVinci Resolve
  Video/postprocess/exported/      DVR exports waiting for GPS inject
  Video/                           Final: stabilized + graded + GPS
```

**Workflow:**
1. `umi import --stabilize` — copies to `Gyroflow/`, prepares GPX files
2. `umi process --stabilize` — Gyroflow output lands in `Stabilized/`, then moves to `Video/postprocess/`
3. Color grade in DaVinci Resolve, export to `Video/postprocess/exported/` (same filename)
4. `umi gps inject --date 2026-02-20 --source GoPro11` — injects GPS, moves to `Video/`, cleans up

---

## GUI — `umi-gui.exe`

UMI includes a WPF desktop interface for Windows. Launch `umi-gui.exe` from the install directory.

**Tabs:**

| Tab | Content |
|---|---|
| **Import** | Camera selection, date range filter, import trigger, live progress |
| **Process** | Gyroflow stabilization, sequence reviewer, GPS injection, restore metadata, sort by date, statistics, thumbnails |
| **Settings** | Sub-tabs Cameras / Profiles / Devices / Tools / Burst — camera tiles, tool paths, language, log level, profiles |

**Key features:**
- **Smart Watch** — insert card → import starts automatically (equivalent to `umi watch`)
- **Quick Import** — fast event backup without pipeline (equivalent to `umi quick`)
- **Camera tiles** — click to expand inline, feature bubbles as toggle (colored = active)
- **Setup Wizard** — first-run wizard starts automatically when no `config.json` exists; accessible via Settings → "Restart Setup Wizard"
- **App modes** — Easy / Standard / Advanced (controls visible options)
- **Language** — English and German (Settings → Tools); the choice you make in the installer is auto-applied on first run
- **Logging** — Off (default), Info or Debug, switchable in Settings → Tools (restart required)
- **Updates** — banner appears when a new GitHub release is available, the installer is fetched in the background and one click runs it

---

## Build from source

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), optional [Inno Setup 6](https://jrsoftware.org/isdown.php) for the installer

```powershell
git clone https://github.com/dm7ds/Universal-Media-Import-v2.git
cd Universal-Media-Import-v2

pwsh -File build.ps1 -Publish slim          # CLI: framework-dependent single-file EXE (~5 MB)
pwsh -File build.ps1 -Publish portable      # CLI: self-contained EXE (~65 MB)
pwsh -File build.ps1 -Publish slim -Installer  # CLI + Windows installer (Inno Setup)
pwsh -File build.ps1 -Gui                   # GUI only (umi-gui.exe)
pwsh -File build.ps1 -Publish slim -Gui     # CLI + GUI combined
```

Output lands in `publish/`. The build script runs all unit tests before publishing and fails on any warning (`/warnaserror`).

**Source tree:**

```
src/
├── UMI.CLI/           CLI entry point (System.CommandLine)
│   ├── Commands/      Command implementations
│   └── Helpers/       ConsoleHelper, SpectreWizardRenderer
├── UMI.Core/          Business logic
│   ├── Configuration/ Config classes (UmiConfig, CameraConfig, ...)
│   ├── Constants/     FeatureKeys, FolderNameConstants
│   ├── Services/      Import, GPS, Gyroflow, PostProcessing, ...
│   ├── Features/      GPS, Metadata, SRT, BurstDetection
│   └── Utilities/     PathHelper, Mp4Parser, MetadataReader
├── UMI.Cameras/       Universal camera handler + factory
├── UMI.Data/          SQLite persistence (Dapper, GpuTaskQueue)
└── UMI.GUI/           WPF desktop interface

tests/
└── UMI.Core.Tests/    Unit tests
```

Further documentation: [FEATURES.md](FEATURES.md) (feature reference) and [docs/help/](docs/help/) (in-app help, EN + DE).

---

## License

GPL-3.0-or-later — see [LICENSE](LICENSE) for details.

Third-party components: [THIRD_PARTY_LICENSES.txt](THIRD_PARTY_LICENSES.txt)
