# UMI — GUI User Guide

---

## 1. Quick Start (Easy Mode)

For anyone who just wants to copy files from an SD card to their PC. No technical knowledge required.

### First Launch

When you start UMI for the first time, the Setup Wizard opens automatically.

1. **Welcome** — Select **Easy** as your mode. This keeps the interface minimal.
2. **Workbench** — Choose the folder where your media should be imported (e.g. `D:\Media\Import`).
3. **Detect Camera Source** — Insert your SD card. UMI detects it automatically and displays it.
4. **Name your camera** — Give your camera a name (e.g. "My Action Cam") and select the matching camera type.
5. **Register SD Cards** — The detected card is assigned to your camera. Optional — you can do this later.
6. **Add More Cameras?** — Got more cameras? Repeat the process. Otherwise, continue.
7. **Tools** — UMI usually finds ExifTool on its own. Just click through.
8. **Summary** — Review the summary and click **Finish**.

### Your First Import

After setup you land in the **Import Tab**, where your camera appears as a tile.

**Option A — Automatic (recommended):**

1. Click **Smart Watch** in the top-left.
2. Insert an SD card.
3. UMI detects the card, starts the import, and shows progress on the camera tile.
4. Card out, next card in — UMI keeps going.
5. Done? Click **Stop Watch**.

**Option B — One-time:**

1. Insert the SD card.
2. Click **Quick Import**.
3. UMI copies all detected media and shows progress.

That's it. Your photos and videos are now sorted by date and camera in your Workbench folder.

### Folder Structure

After import, your Workbench folder looks like this:

```
D:\Media\Import\
  2026-03-12\
    MyActionCam\
      Video\
        DJI_20260312_143022.mp4
        DJI_20260312_143155.mp4
      Photo\
        DJI_20260312_143022.jpg
```

---

## 2. Advanced Guide — Drone, Action Cam & GPS

For videographers with action cams or drones who want to use Gyroflow stabilization and GPS injection.

### App Modes

The header has a dropdown with three modes:

| Mode | What you see |
|------|-------------|
| **Easy** | Import only — no post-processing |
| **Standard** | Import + Process Tab (stabilization, GPS) |
| **Advanced** | Everything — including Profiles, Burst Detection, Device Management |

For this guide: select **Standard** or **Advanced**.

### Feature Bubbles

Each camera has features you can toggle on and off via **Feature Bubbles** on the camera tile. Colored = active, dark = disabled.

For a typical drone or action cam:

| Feature | What it does | Recommendation |
|---------|-------------|----------------|
| **GPS** | UMI builds GPX tracks and injects GPS coordinates into the video | Enable if you use an external GPS tracker |
| **Gyroflow** | UMI stabilizes EIS-Off footage via Gyroflow | Enable for action cams without good internal stabilization |
| **EIS** | Detects whether the camera's internal stabilization was on or off | Enable for DJI action cams |
| **Metadata** | UMI backs up original EXIF before processing modifies metadata | Enable (recommended for everyone) |
| **PostProc** | Activates the post-processing pipeline (DaVinci Resolve export flow) | Enable if you grade in DaVinci |
| **Rename** | Renames videos with a timestamp prefix | Optional |
| **GoPro** | Special GoPro renaming (chapter format) | GoPro only |

To edit: click on the camera tile, change features, click **Save**.

### Gyroflow Stabilization

Cameras with the **Gyroflow** toggle enabled need a lens preset:

1. Expand the camera tile (click it)
2. **Gyroflow Preset** field → Browse → select a `.gyroflow` preset file
3. Save

UMI uses this preset automatically during stabilization.

### GPS Injection

UMI injects GPS data from GPX files into your videos. You need a GPS tracker (phone app, Garmin, etc.).

1. **Settings** → **Tools** → **GPS Track Folder** → select the folder containing your GPX files
2. Enable the **GPS** feature on the camera tile

UMI automatically matches videos to the right GPX track by timestamp.

### Process Tab

After importing, switch to the **Process Tab**. It is organized into three sub-tabs:

#### Video Sub-Tab

Contains actions for the video post-processing pipeline:

##### Stabilize Videos

Gyroflow stabilization for videos without internal stabilization (EIS-Off).

- Enable the **Detect EIS** toggle → UMI automatically finds videos without EIS and moves them to the Gyroflow folder
- Click **Run** → videos are stabilized via the GPU queue
- Progress per video (frame count + ETA)
- Result: stabilized videos in `Video/Stabilized/`

##### GPS Inject

Builds optimized GPX tracks and injects GPS coordinates.

- Toggle **Inject into video** → On: write GPS directly into the video. Off: only prepare GPX files.
- Only processes cameras where you enabled the **GPS** toggle in UMI
- Results are expandable: per video whether GPS was injected, GPX was built, or skipped

##### Restore Metadata

Restores EXIF metadata from `.umi/metadata/` backups.

- Useful when Gyroflow or other tools have corrupted metadata
- Toggle **Force overwrite** → overwrites metadata even if it looks intact
- Result: "Metadata restored" / "Not needed" / "No backup available"

##### Finalize Exports

When you have graded videos in DaVinci Resolve and the exports are in `Video/postprocess/exported/`:

- Injects GPS data into the exports
- Restores metadata
- Moves finished videos to `Video/`
- Cleans up the postprocess folder

#### Photo Sub-Tab

Contains actions for photo review and burst management:

##### Sequence Reviewer

Opens the Sequence Reviewer for reviewing, rating, and tagging burst sequences.

- Click **Run** to open the Sequence Reviewer window
- UMI automatically loads all photos from your Workbench folder and applies the burst profiles

See [Section 5](#5-sequence-reviewer) for full documentation.

#### Tools Sub-Tab

Contains utility actions:

##### Sort by Date

Sorts files into folders by EXIF date. (Not yet available in the GUI — coming soon.)

##### Workbench Statistics

Overview of all videos in the Workbench:

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

##### Generate Thumbnails

Pre-generates thumbnail cache for all photos in the Workbench. This speeds up the first load of the Sequence Reviewer.

- Click **Run** → UMI scans all photo folders and generates missing thumbnails
- Optional date filter in the Process Tab header to limit which folders are processed

### Pipeline Overview

The lifecycle of a video looks like this:

```
SD card → Import (EIS detection, Metadata Backup)
  → EIS Off?  → Gyroflow/ → Stabilize → Stabilized/
  → EIS On?   → Video/ (ready to use)
  → GPS?      → Build GPX → Inject GPS
  → DaVinci?  → postprocess/ → Grade → exported/ → Finalize → Video/
```

---

## 3. Burst Detection — For Photographers

Burst Detection identifies burst sequences in your photos and groups them into subfolders. Ideal for:

- Exposure brackets (HDR)
- Action sequences (sports, wildlife)
- Astro stacking series
- Timelapses

### How It Works

UMI reads the EXIF data of each photo and groups them by two criteria:

1. **Match Conditions** — Which photos belong together? Defined by EXIF field rules.
2. **Grouping** — When does a series break? Defined by the maximum time gap.

**Example:** "All photos with ISO 100 and aperture f/2.8 that are less than 3 seconds apart belong to one series."

### Profiles

Profiles define the detection rules. Find them under **Settings** → **Burst** (Advanced mode only).

Each profile consists of:

#### Match Conditions

One or more conditions a photo must meet:

| Field | Operator | Value | Meaning |
|-------|----------|-------|---------|
| ExposureTime | = | 1/125 | Only photos with exactly this exposure time |
| FNumber | >= | 2.8 | Aperture of at least f/2.8 |
| ISO | < | 400 | ISO below 400 |
| Model | Contains | "R5" | Camera model contains "R5" |

Available operators: `=`, `!=`, `>`, `<`, `>=`, `<=`, `Contains`, `StartsWith`, `EndsWith`, `Matches` (Regex)

Multiple conditions are combined with **AND** (all must match) or **OR** (at least one must match).

#### Grouping Config

| Parameter | Meaning |
|-----------|---------|
| **Max Gap Seconds** | Maximum time gap between two photos in a series. If exceeded, a new series begins. |
| **Adaptive Threshold** | Enables adaptive threshold calculation. UMI analyzes time gaps and adjusts the threshold automatically. The Max Gap Seconds value acts as a minimum floor. |

**Typical values:**
- Exposure brackets (HDR): Max Gap 2–5s
- Sports/Action: Max Gap 1–2s
- Astro stacking: Max Gap 30–60s
- Timelapse: Max Gap 10–30s

#### Priority

Profiles are evaluated in order (drag and drop to reorder). The first matching profile wins. Put specific profiles higher up, general ones lower.

### Creating Profiles

**Manually:**

1. **Settings** → **Burst** → **Add Profile**
2. Enter a name and description
3. Expand the **Rules Editor** → add rules
4. Configure **Grouping** (Max Gap, Adaptive)
5. **Save**

**Automatically (Auto-Preset Generator):**

1. **Settings** → **Burst** → load a folder with sample photos
2. Select photos from a known series (click + Shift/Ctrl)
3. Click **Generate Preset**
4. UMI analyzes the EXIF data of the selected photos and generates matching rules
5. Review the preview → **Accept & Save**

**Testing a profile:**

Each profile has a built-in Visualizer:

1. Expand the profile → **Load Folder**
2. UMI scans the photos and shows the result:
   - Color-coded groups = detected series
   - Gray = unassigned photos (Orphans)
3. Adjust rules until the result looks right

In the **Burst Studio** (the large visualizer below the profile cards):

- Thumbnail grid with color-coded sequence assignment
- Size slider (64–256 px)
- Profile dropdown for quick switching
- Selection Mode for the Auto-Preset Generator

### Example Profiles

**HDR Brackets (Canon R5):**
```
Conditions: Model Contains "R5" AND DriveMode = "Continuous"
Grouping: Max Gap 3s, Adaptive ON
```

**Astro Stacking (general):**
```
Conditions: ExposureTime >= 10 AND ISO >= 1600
Grouping: Max Gap 45s, Adaptive ON
```

**Sport Action:**
```
Conditions: ShutterSpeed <= 1/500
Grouping: Max Gap 1.5s, Adaptive OFF
```

---

## 4. Device Management

Manage all storage sources UMI knows about under **Settings** → **Devices**.

### SD Cards

SD cards are identified by their Volume Serial Number (VSN) — a unique identifier that persists even after renaming or formatting (Quick Format only).

**Registering a card:**

1. **Settings** → **Devices** → **Add Device**
2. Tab **SD Card** → insert the card
3. UMI detects the card automatically and displays it
4. Assign a camera → **Register**

**Card assignment:**

| Assignment | Import behavior |
|-----------|----------------|
| Camera assigned (Fixed) | Import starts automatically without prompting |
| No camera (Floating) | UMI asks which camera to use at each import — with a suggestion from history |

To change: simply select a different camera in the dropdown and save. To set as Floating: leave the camera field empty.

**Card history:**

Each SD card has a history icon (clock symbol). Clicking it shows:

- Which cameras have used this card
- How often (import count + percentage)
- When last used

Useful when you swap cards between cameras and want to know where a card has been.

**Card re-recognition:**

UMI recognizes cards even after reformatting, as long as the disk serial and card size match (5% tolerance). The previous assignment is applied automatically.

### MTP Devices

USB cameras and Android devices connected via MTP (Media Transfer Protocol).

1. **Add Device** → Tab **MTP Device**
2. Connect the device via USB
3. UMI displays detected MTP devices
4. Assign a camera → save

MTP devices are always assigned to a fixed camera (no Floating).

### Fixed Paths

Local folders or network drives used as import sources — for example, a dashcam folder on a NAS.

1. **Add Device** → Tab **Fixed Path**
2. Enter or browse to the folder path
3. Assign a camera → save

UMI monitors the folder in Watch Mode and imports new files automatically.

**Online status:**

In the Devices tab, a green dot indicates which devices are currently connected. Gray dots = offline or not inserted. Status is updated live.

**Order:**

Use drag and drop to change the order of devices within each group. The order determines display position — it has no effect on import logic.

---

## 5. Sequence Reviewer

The Sequence Reviewer lets you review, rate and tag burst sequences visually — one sequence at a time, one photo at a time.

### Opening the Reviewer

Open the Sequence Reviewer from the **Process Tab** → **Photo** (sub-tab) → **Sequence Reviewer** (action card) → **Run**. UMI automatically loads all photos from your Workbench folder and applies the burst profiles.

### Navigation

| Key | Action |
|-----|--------|
| **←** / **→** | Previous / next photo in current sequence |
| **↑** / **↓** | Previous / next sequence (respects filter) |
| **Mouse wheel** | Scroll through photos |

The **Filmstrip** at the bottom shows all photos in the current sequence. Click a thumbnail to jump directly to that photo. You can also drag-scroll the filmstrip.

### Tagging and Rating

| Key | Action |
|-----|--------|
| **Space** | Toggle Favorite |
| **X** | Toggle Trash |
| **1–5** | Set star rating (1–5 stars). Press the same key again to clear the rating. |

Tags and ratings are saved immediately and persist across sessions.

### Filtering

The header bar has filter buttons:

| Filter | Shows |
|--------|-------|
| **All** | All photos in the sequence |
| **Favorites** | Only photos tagged as Favorite |
| **Trash** | Only photos tagged as Trash |

When a filter is active and no photos match, a message is displayed.

### Sequence Overview

Press **G** to toggle the Sequence Overview — a grid of all sequences with their first photo as thumbnail.

Each card shows:
- Sequence name and photo count
- Capture date (from first photo)
- Status badges (stars, favorites, trash count)
- Profile color strip at the top edge

**Sequence filter bar:**

| Filter | Shows |
|--------|-------|
| **All** | All sequences |
| **Rated** | Sequences with at least one rated/tagged photo |
| **Unrated** | Sequences with no ratings or tags |

Click a card to jump into that sequence. Navigation with ↑/↓ respects the active filter.

### Profiles and Colors

The profile dropdown in the header lets you switch between burst profiles. Special entries:

| Entry | Behavior |
|-------|----------|
| **All Sequences** | Merges sequences from all profiles. Each card shows which profile matched. |
| **Unassigned** | Shows photos not matched by any profile. |

Each profile has a color (configurable under **Settings** → **Burst** → edit the profile). The color appears on the sequence cards, the profile name in the header, and in the overview.

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **← →** | Navigate photos |
| **↑ ↓** | Navigate sequences |
| **Space** | Toggle Favorite |
| **X** | Toggle Trash |
| **1–5** | Set rating (press same key again to clear) |
| **G** | Toggle Overview |
| **F1** | Open Help |
| **Escape** | Close Reviewer |

---

## 6. Settings

Open the Settings by clicking **Settings** in the main navigation. The Settings tab has five sub-tabs:

### Cameras

Manage all cameras UMI knows about. Each camera has a name, type, and set of features.

- **Add Camera** — register a new camera manually
- **Restart Setup Wizard** — re-run the setup flow to add cameras, configure tools, or change settings

### Profiles (Advanced mode only)

Import and export profiles for camera feature sets. Only visible in Advanced mode.

### Devices

Manage SD cards, MTP devices, and fixed folder paths. See [Section 4](#4-device-management) for details.

### Tools

Configure external tools and paths:

- **ExifTool path** — path to `exiftool.exe`
- **Gyroflow path** — path to the Gyroflow CLI executable
- **GPS Track Folder** — folder where UMI looks for GPX files
- **Language** — switch between English and German (restart required)

### Burst (Advanced mode only)

Manage burst detection profiles. Only visible in Advanced mode. See [Section 3](#3-burst-detection--for-photographers) for details.

---

## 7. Help System

UMI has a built-in help system accessible at any time:

- **F1** — opens context-sensitive help for the currently active tab or window
- **Help button** — the `?` button in the toolbar opens the help window for the current context
- The **Help window** shows Markdown documentation with a sidebar for navigation between chapters and a search box to find topics quickly
- Each tab also has a **Learn more** link that jumps directly to the relevant section in the help
