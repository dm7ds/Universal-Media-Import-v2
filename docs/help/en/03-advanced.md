## Advanced Guide — Drone, Action Cam & GPS

For videographers with action cams or drones who want to use [Gyroflow](glossary:Gyroflow) stabilization and [GPS](glossary:GPS) injection.

### App Modes

The header has a dropdown with three modes:

| Mode | What you see |
|------|-------------|
| **Easy** | Import only — no post-processing |
| **Standard** | Import + Process Tab (stabilization, GPS) |
| **Advanced** | Everything — including Profiles, [Photo Series Detection](chapter:burst), [Device Management](chapter:devices) |

For this guide: select **Standard** or **Advanced**.

### Feature Bubbles

Each camera has features you can turn on and off via **Feature Bubbles** on the camera tile. Colored = active, dark = disabled.

For a typical drone or action cam:

| Feature | What it does | Recommendation |
|---------|-------------|----------------|
| **GPS** | UMI builds GPX tracks and injects GPS coordinates into the video | Enable if you use an external GPS tracker |
| **Gyroflow** | UMI stabilizes [EIS](glossary:EIS)-Off footage via Gyroflow | Enable for action cams without good internal stabilization |
| **Burst** | Enables burst / photo series detection for this camera | Enable for cameras that shoot photo sequences |
| **EIS** | Detects whether the camera's internal stabilization was on or off | Enable for DJI action cams |
| **Metadata** | UMI backs up original [EXIF](glossary:EXIF) before processing modifies metadata | Enable (recommended for everyone) |
| **Lens** | Applies lens correction (stub — not yet active) | Experimental |
| **PostProc** | Activates the post-processing pipeline (DaVinci Resolve export flow) | Enable if you grade in DaVinci |
| **Rename** | Renames videos with a timestamp prefix | Optional |
| **GoPro** | Special GoPro renaming (chapter format) | GoPro only |

To edit: click on the camera tile, change features, click **Save**.

### Gyroflow Stabilization

Cameras with **Gyroflow** enabled need a lens preset:

1. Expand the camera tile (click it)
2. **Gyroflow Preset** field → Browse → select a `.gyroflow` preset file
3. Save

UMI uses this preset automatically during stabilization.

### GPS Injection

UMI injects GPS data from [GPX](glossary:GPX) files into your videos. You need a GPS tracker (phone app, Garmin, etc.).

1. **Settings** → **Tools** → **GPS Track Folder** → select the folder containing your GPX files
2. Enable the **GPS** feature on the camera tile

UMI automatically matches videos to the right GPX track by timestamp.

### Process Tab

After importing, switch to the **Process Tab**. It has three groups:

#### Video Tools

Action cards for video post-processing, worked through top to bottom:

##### Stabilize Videos

Gyroflow stabilization for videos without internal stabilization (EIS-Off).

- Enable **Detect EIS** → UMI automatically finds videos without EIS and moves them to the Gyroflow folder
- Click **Run** → videos are stabilized via the GPU queue
- Progress per video (frame count + ETA)
- Result: stabilized videos in `Video/Stabilized/`

##### GPS Inject

Builds optimized GPX tracks and injects GPS coordinates.

- **Inject into video** → On: write GPS directly into the video. Off: only prepare GPX files.
- Only processes cameras where you enabled **GPS** in UMI
- Results are expandable: per video whether GPS was injected, GPX was built, or skipped

##### Restore Metadata

Restores EXIF metadata from `.umi/metadata/` backups.

- Useful when Gyroflow or other tools have corrupted metadata
- **Force overwrite** → overwrites metadata even if it looks intact
- Result: "Metadata restored" / "Not needed" / "No backup available"

##### Finalize Exports

When you have graded videos in DaVinci Resolve and the exports are in `Video/postprocess/exported/`:

- Injects GPS data into the exports
- Restores metadata
- Moves finished videos to `Video/`
- Cleans up the postprocess folder

#### Photo Tools

##### Sequence Reviewer

Opens the [Sequence Reviewer](chapter:reviewer) for reviewing and rating detected photo series.

#### After Import

##### Organize

Sorts files by date and detects [photo series](chapter:burst). (**Detect Photo Series** is not yet available in the GUI — coming soon.)

##### Generate Thumbnails

Generates preview thumbnails for RAW files in the Workbench. Thumbnails are cached under `.umi/thumbnails/` and used by the [Sequence Reviewer](chapter:reviewer).

##### Workbench Statistics

Overview of all videos in the [Workbench](glossary:Workbench):

| Column | Shows |
|--------|-------|
| Date | Recording date |
| Source | Camera ID |
| File | Filename |
| Status | Pipeline status (Imported → Stabilized → GPS injected → Graded → Ready) |
| EIS | ON / OFF / N/A |
| GPS | Yes / Built / No |
| Backup | OK / Missing / N/A |
| Integrity | OK / Size=0 |

Yellow warning for: status mismatch (history vs. folder), missing backups, integrity issues.

### Pipeline Overview

The lifecycle of a video looks like this:

```
SD card → Import (EIS detection, Metadata Backup)
  → EIS Off?  → Gyroflow/ → Stabilize → Stabilized/
  → EIS On?   → Video/ (ready to use)
  → GPS?      → Build GPX → Inject GPS
  → DaVinci?  → postprocess/ → Grade → exported/ → Finalize → Video/
```
