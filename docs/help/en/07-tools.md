## Tools & Options

Configure external tools and app settings under **Settings** → **Tools**.

### External Tools

#### ExifTool (Required)

UMI uses [ExifTool](glossary:ExifTool) to read and write [EXIF](glossary:EXIF) metadata. It is required for [GPS](glossary:GPS) injection, [Metadata Backup](glossary:Metadata Backup) and restore.

- UMI bundles ExifTool and uses it automatically
- **Browse** — select a custom ExifTool executable
- **Default** — reset to the bundled version
- Status: green check = found, red X = missing

#### Gyroflow (Optional)

Required only if you use [Gyroflow](glossary:Gyroflow) stabilization (see [Advanced Guide](chapter:advanced)).

- **Browse** — select the Gyroflow CLI executable
- **Install** — downloads the latest version from GitHub
- Not needed if you don't stabilize videos

#### FFprobe (Optional)

Used for detailed video analysis (codec, resolution, bitrate).

- **Browse** — select the [FFprobe](glossary:FFprobe) executable
- **Install** — downloads FFmpeg essentials (includes FFprobe)
- Not needed for basic import/export workflows

### Options

#### GPS Track Folder

Folder containing your [GPX](glossary:GPX) files from GPS trackers (phone apps, Garmin, etc.).

- **Browse** — select the folder
- UMI scans this folder when running [GPS injection](chapter:advanced)

#### Language

Switch between English and German. Requires an app restart to take effect.

#### Debug Logging

Enables verbose log output for troubleshooting. Only needed when diagnosing issues.

#### Restart Setup Wizard

Reopens the [Setup Wizard](chapter:setup) from the beginning. Useful if you want to reconfigure your workbench path, cameras, or tools without manually editing config files.

### Generate Thumbnails

Thumbnail generation for RAW files is available under **Process** → **After Import** → **Generate Thumbnails**. It is not part of the Tools settings panel — see the [Advanced Guide](chapter:advanced) for details.
