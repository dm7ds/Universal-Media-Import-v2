## Photo Series Detection — For Photographers

UMI automatically detects related photo series and groups them into subfolders. This works not just for rapid-fire bursts, but for any kind of photo series. Ideal for:

- Exposure brackets ([HDR](glossary:HDR))
- Action sequences (sports, wildlife)
- Astro stacking series
- Timelapses

### Detecting Series After Import

Already imported your photos and want to run series detection afterwards? No problem — this works anytime:

1. Go to **Process** → **Photo Tools** → **[Sequence Reviewer](chapter:reviewer)**
2. UMI automatically analyzes all photos in the Workbench and applies your burst profiles
3. You see the detected series visually and can rate and tag them

To sort them into subfolders, use **Process** → **After Import** → **Organize** with **Detect Photo Series** enabled. UMI will automatically group detected series into their own folders.

> **Note:** The **Detect Photo Series** option in Organize is not yet available in the GUI. It is planned for a future release.

You need at least one detection profile for this (see below). If you don't have one yet, UMI can generate one automatically.

### How It Works

UMI reads the [EXIF](glossary:EXIF) data of each photo and groups them by two criteria:

1. **Match Conditions** — Which photos belong together? Defined by EXIF field rules.
2. **Grouping** — When does a series break? Defined by the maximum time gap.

**Example:** "All photos with ISO 100 and aperture f/2.8 that are less than 3 seconds apart belong to one series."

### Profiles

Profiles define the detection rules. Find them under **Settings** → **Burst** (Advanced mode only). See also [Tools & Options](chapter:tools) for tool configuration.

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

### Burst Studio

The Burst Studio is the large visualizer below the profile cards in **Settings** → **Burst**. It lets you test your profiles visually and generate new ones from sample photos.

#### Loading Photos

1. Click **Browse** to select a folder with photos
2. Click **Load** — UMI reads the [EXIF](glossary:EXIF) data and extracts thumbnails
3. A progress bar shows the scan status

#### Thumbnail Grid

After loading, your photos appear as thumbnails in a grid:

- **Color-coded borders** — each detected series gets a unique color matching the profile
- **Gray** — unassigned photos (no profile matched)
- **Size slider** — drag to adjust thumbnail size (64–256 px)

#### Evaluating Profiles

Select a profile from the **Profile dropdown** at the top. UMI immediately evaluates the loaded photos against the selected profile and updates the color coding. The summary shows how many series were detected and how many photos matched.

Switch profiles to compare results — the grid updates instantly.

#### Auto-Preset Generator

Don't want to write rules by hand? Let UMI generate them:

1. Click the **Selection Mode** button in the toolbar
2. Click photos that belong to the same series — a checkmark appears on selected photos
3. Click **Generate Preset** — UMI analyzes the [EXIF](glossary:EXIF) data of your selection
4. UMI finds stable fields (values that are identical or very similar across your selection) and creates matching rules
5. Review the suggested rules and grouping settings
6. Click **Accept & Save** to create a new profile, or **Discard** to start over

The generated profile appears in your profile list and can be fine-tuned manually.

#### Tips

- Load a folder with mixed content (bursts + single shots) for realistic testing
- Use the Auto-Preset Generator first, then refine the rules
- The [Sequence Reviewer](chapter:reviewer) shows results across your entire [Workbench](glossary:Workbench) — the Burst Studio is for testing with a specific folder

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
