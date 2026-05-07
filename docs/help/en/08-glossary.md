## Glossary

A quick reference for terms you may encounter when working with UMI.

### Burst / Burst Detection

A burst is a series of photos taken in rapid succession by holding down the shutter button. UMI automatically detects bursts and groups them into a dedicated sub-folder so they stay together and don't clutter your main import folder. HDR bracket sequences are also detected this way.

### EIS (Electronic Image Stabilization)

EIS stands for Electronic Image Stabilization — a technique where the camera crops and shifts the image digitally to compensate for camera shake. UMI can detect whether EIS was active during recording and sort videos into separate folders accordingly. See also: [Electronic Image Stabilization on Wikipedia](https://en.wikipedia.org/wiki/Image_stabilization#Electronic_image_stabilization_(EIS)).

### EXIF (Exchangeable Image File Format)

EXIF is a standard for storing metadata inside photo and video files. This includes the capture date and time, camera model, lens settings, GPS coordinates, and more. UMI reads EXIF data to organize your files and can also write updated metadata back to them. Learn more: [EXIF on Wikipedia](https://en.wikipedia.org/wiki/Exif).

### ExifTool

ExifTool is a command-line tool for reading and writing EXIF metadata in virtually any media file format. UMI uses ExifTool in the background for GPS injection, metadata backup, and metadata restore. You don't need to use it directly — UMI handles everything automatically. More info: [exiftool.org](https://exiftool.org/).

### FFprobe

FFprobe is part of the [FFmpeg](https://ffmpeg.org/) suite and analyzes video files to report codec, resolution, bitrate, and duration. UMI uses FFprobe for detailed video analysis during import. It is optional — basic workflows work without it. More info: [FFprobe documentation](https://ffmpeg.org/ffprobe.html).

### GPS (Global Positioning System)

GPS is a satellite-based navigation system that provides location coordinates. UMI can inject GPS coordinates from an external GPX track file into video files, adding location data that your camera may not have recorded. Learn more: [GPS on Wikipedia](https://en.wikipedia.org/wiki/Global_Positioning_System).

### GPX (GPS Exchange Format)

GPX is an open file format for storing GPS tracks, waypoints, and routes. Each point contains latitude, longitude, and a timestamp. UMI matches these timestamps against your video capture times to inject accurate GPS data into the footage. Learn more: [GPX on Wikipedia](https://en.wikipedia.org/wiki/GPS_Exchange_Format).

### Gyroflow

Gyroflow is an open-source video stabilization application that uses gyroscope sensor data from your camera or action cam to produce smooth footage. UMI can automatically run Gyroflow stabilization on your videos as part of the import post-processing pipeline. More info: [gyroflow.xyz](https://gyroflow.xyz/).

### HDR (High Dynamic Range)

HDR photography combines multiple exposures of the same scene taken at different brightness levels to capture a wider tonal range. Cameras often shoot these as rapid bracket sequences — UMI detects them via Burst Detection and keeps them grouped together. Learn more: [HDR on Wikipedia](https://en.wikipedia.org/wiki/High_dynamic_range).

### Metadata Backup

Before UMI modifies any file (for example during GPS injection), it saves a copy of the original EXIF data into the `.umi/metadata/` subfolder inside your Workbench. This lets you restore the original metadata at any time without losing the changes you made to the actual media.

### MTP (Media Transfer Protocol)

MTP is a protocol that allows cameras and Android devices to transfer files over USB. Unlike a regular USB drive, MTP devices do not appear as a drive letter on your PC. UMI supports MTP connections for Android phones and cameras — connect the device via USB, then register it under **Settings** → **Devices** → **MTP Device**. Learn more: [MTP on Wikipedia](https://en.wikipedia.org/wiki/Media_Transfer_Protocol).

### VSN (Volume Serial Number)

The Volume Serial Number is a unique identifier assigned to an SD card or storage volume by the operating system. UMI uses the VSN to recognize SD cards reliably — even if you rename the card or use it in a different slot — and automatically link it to the correct camera profile.

### AppMode (Easy / Standard / Advanced)

UMI has three operating modes selectable via the dropdown in the header. **Easy** shows only the import view — ideal for users who just want to copy files. **Standard** adds the Process Tab with stabilization and GPS tools. **Advanced** unlocks everything: Burst profiles, Device Management, and all advanced settings. The mode can be changed at any time.

### Burst Profile

A Burst Profile defines the detection rules for one type of photo series (e.g. HDR brackets or sports sequences). Each profile consists of match conditions (which EXIF fields must match) and grouping settings (maximum time gap between photos). Profiles are managed under **Settings** → **Burst** in Advanced mode.

### Fingerprint (SD Card)

UMI identifies SD cards by a combination of their Volume Serial Number and disk serial / size. This "fingerprint" allows UMI to recognize the same card reliably across different card readers, drive letters, and even after Quick Format. Once registered, the card is automatically linked to its camera profile on every insert.

### Sidecar

A sidecar file is a small metadata file stored alongside your media files. UMI uses `.umi-review.json` for review tags and ratings, and `.umi-sequences.json` for detected series groupings. Sidecar files are stored under `.umi/review/` and `.umi/sequences/` in the Workbench.

### Workbench

The Workbench is the root folder where UMI imports and organizes all your media. You set it up during the Setup Wizard and all imported files land in sub-folders here, sorted by camera and date. Think of it as your personal media library.
