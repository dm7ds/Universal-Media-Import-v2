# Changelog

All notable user-visible changes are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
loosely tracks [Semantic Versioning](https://semver.org/).

## [2.2.2] — 2026-08-06

### Fixed

- **🔴 Wichtig: „Löschen" im Sequenz-Reviewer konnte ganze Serien löschen.**
  Der Löschen-Knopf entfernte alles, was der aktive Filter gerade zeigte — ohne
  Müll-Filter also **sämtliche Fotos der Serie**, endgültig und ohne Rückfrage.
  Ausgelöst wurde das leicht: Wer einmal auf „Müll" filterte, schaltete damit
  den Löschen-Knopf scharf, und beim Zurückwechseln auf „Alle" blieb er es.
  Betroffene erkennen es an Serien, die in der Übersicht noch gezählt werden,
  aber leer sind.

### Added

- **Abfrage vor dem Aussortieren — mit drei Möglichkeiten.** Der Reviewer fragt
  jetzt immer nach und nennt die genaue Anzahl:
  - **In den Müll-Ordner verschieben** (Vorauswahl): landet in `_Trash` im
    selben Ordner, **unter Beibehaltung der Ordnerstruktur** — man sieht also,
    woher jedes Foto stammt, und kann es einfach zurückschieben. Gleichnamige
    Dateien werden nicht überschrieben.
  - **In den Windows-Papierkorb**: über den Explorer wiederherstellbar.
  - **Endgültig löschen**: mit deutlichem Warnhinweis.

  Aussortiert werden ausschließlich Fotos mit Müll-Markierung. Der Müll-Ordner
  wird bei künftigen Durchläufen übersprungen, aussortierte Fotos tauchen also
  nicht wieder auf. Fehler werden gemeldet und protokolliert statt verschluckt.

## [2.2.1.1] — 2026-07-30

Patch-Release. Enthält funktional dasselbe Programm wie 2.2.1 — dieses Release
existiert, weil der Installer-Build von 2.2.1 nicht durchlief.

### Fixed

- **Installer-Build der Version 2.2.1 schlug fehl (I-009).** Der automatische
  Release-Build konnte ExifTool nicht mehr herunterladen: die bisherigen
  Download-Adressen von exiftool.org existieren nicht mehr, das Projekt liefert
  seine Windows-Pakete inzwischen über SourceForge aus. Für 2.2.1 wurde deshalb
  kein Installer veröffentlicht. Der Build bezieht ExifTool jetzt über einen
  „latest"-Link ohne feste Versionsnummer — damit kann dieselbe Ursache nicht
  wiederkehren (sie hatte den Release zuvor bereits zweimal getroffen).

- **Update-Hinweis für Patch-Versionen.** Bei vierstelligen Versionsnummern
  (wie dieser) verglich die Update-Prüfung nur die ersten drei Stellen und hätte
  dauerhaft ein Update auf die bereits installierte Version angeboten.

## [2.2.1] — 2026-07-28

### Fixed

- **Reviewer zeigte „Keine Bilder gefunden" trotz voller Ordner (I-005 — CRITICAL).**
  Stand der Workbench-Pfad versehentlich *innerhalb* des internen `.umi`-Ordners
  (z.B. `E:\umi\.umi\...`), filterte jeder Foto-Scan seine eigenen Dateien wieder
  weg — der Ordner wirkte dauerhaft leer, obwohl tausende Bilder darin lagen.
  UMI weist einen solchen Pfad jetzt an allen Stellen ab, an denen er gesetzt
  werden kann (Einstellungen, Ersteinrichtung-Assistent, CLI-Assistent), und
  meldet eine bereits betroffene Konfiguration beim Start im Log. Der
  Reviewer nennt in seinem Hinweis außerdem den tatsächlich gescannten Ordner,
  damit „falscher Ordner" überhaupt erkennbar ist.

- **Support-Bundle enthielt nie den Log des laufenden Tages (I-008).**
  Ausgerechnet der Log mit dem gemeldeten Vorfall fehlte in jedem Bundle, weil
  die Datei vom laufenden Programm offen gehalten wird. Sie wird jetzt korrekt
  mitkopiert.

### Changed

- **Sequenz-Erkennung meldet, wenn sie nichts findet (I-006).** Lief die
  Burst-Erkennung ins Leere, geschah das bisher wortlos — im Log stand nur
  „0 Sequenzen", ohne Grund. Jeder mögliche Ausstieg wird jetzt benannt
  (keine Aufnahmedaten, Datum unlesbar, zu wenige Fotos, Funktion deaktiviert).
  Hinweis: Ein gemeldeter Fall, in dem beim Import keine Serien erkannt wurden,
  ist damit noch **nicht** behoben — die Ursache ist offen; diese Änderung macht
  sie beim nächsten Auftreten sichtbar.

## [2.2.0] — 2026-06-03

### Added

- **Kamera-Erkennung über die Body-Seriennummer (TASK-220–222).** UMI liest jetzt
  die eindeutige Gehäuse-Seriennummer aus dem EXIF (Canon Makernote bzw.
  Standard-EXIF `BodySerialNumber`) und kann Bilder darüber der richtigen Kamera
  zuordnen — auch wenn zwei Kameras **dasselbe Modell** sind (z.B. zwei Canon
  EOS R5m2). Verifiziert eineindeutig: jede Kamera hat eine stabile, eigene
  Seriennummer; die Objektiv-Seriennummer wird bewusst NICHT verwendet (kann
  `0000000000` sein).

- **Zuordnungs-Kaskade beim "Organisieren".** Die CameraId-Bestimmung läuft jetzt
  als Kaskade: **Seriennummer → Kamera-Modell → Ordnerpfad → `_unsorted`**.
  Dadurch landen flach abgelegte Bilder (ohne Kamera-Unterordner) nicht mehr
  pauschal unter `_unsorted`, sondern werden per EXIF-Modell der passenden
  registrierten Kamera zugeordnet (nur bei eindeutigem Treffer — kein Raten bei
  mehreren gleichen Modellen).

- **learn-on-first-use.** Wird beim Import eine SD-Karte einer noch nicht per
  Seriennummer bekannten Kamera zugeordnet, merkt sich UMI die Body-Seriennummer
  und das exakte Modell automatisch in der Kamera-Konfiguration (nur leere Felder
  werden gefüllt, bestehende nie überschrieben). Ab dann werden alle weiteren
  Importe/Sortierungen dieser Kamera eindeutig zugeordnet. Nutzt den bestehenden
  "Karte erkannt"-Dialog — keine neue Bedienung.

- **Neues Config-Feld `camera_model`** pro Kamera für robustes Modell-Matching.
  Schema-kompatibel: bestehende Konfigurationen ohne das Feld bleiben gültig.

### Fixed

- _(enthält kumulativ alle Fixes aus 2.1.5–2.1.9 — siehe unten.)_

## [2.1.9] — 2026-06-03

### Fixed

- **Sequenz-Reviewer zeigte leeren Screen bei 0 erkannten Serien (TASK-219 — MAJOR UX / I-004).**
  Wenn kein Burst-Profil Serien in einem Ordner erkannte (z.B. 9751 Einzelbilder einer R5m2),
  zeigte der Default-Filter "Alle Sequenzen" einen leeren Screen — obwohl tausende Bilder
  vorhanden waren. Die ungruppierten Bilder lagen hinter "Nicht zugeordnet", das der User
  nicht kannte. Fix: Bei 0 erkannten Serien + vorhandenen Bildern wechselt der Default-Filter
  automatisch auf "Nicht zugeordnet" und zeigt einen Informations-Banner:
  "Keine Fotoserien erkannt — {N} Bilder werden ungruppiert angezeigt."
  Bei leerem Ordner (0 Bilder) erscheint stattdessen: "Keine Bilder in diesem Ordner gefunden."
  Normalbetrieb (≥1 Serie erkannt) bleibt unverändert. Kein Banner, kein Fallback.
  Wechselt der User manuell den Filter, verschwindet der Banner sofort.

## [2.1.8] — 2026-06-02

### Fixed

- **Burst-Erkennung schlug still fehl bei Alt-Configs (TASK-218 FIX-1 — CRITICAL).**
  Beim Import-Scan lud der Scan-Pfad Burst-Profile nur wenn `active_profiles` nicht leer war.
  Kameras die vor TASK-215 konfiguriert wurden (`Enabled=true, active_profiles=[]`) bekamen
  für jedes Foto `ShootingMode="Single"` in die DB — keine Sequenz wurde erkannt, kein Fehler,
  keine Warnung. Fix: Scan-Pfad delegiert jetzt immer an `LoadBurstConfig()` (DRY, derselbe
  Fallback wie der Sequenz-Erkennungs-Pfad: wenn `active_profiles=[]`, werden alle verfügbaren
  Profile von Disk geladen).

- **`ParseCaptureTime` ohne InvariantCulture (TASK-218 FIX-2 — MAJOR).**
  `SequenceGroupingService.ParseCaptureTime` nutzte `DateTime.TryParse` ohne
  `CultureInfo.InvariantCulture` und `DateTimeStyles.RoundtripKind`. Heute durch
  ISO-`"o"`-Format maskiert — kippt bei Format-Änderung (Falle #11 + #15).

- **Cancellation wurde im Statistik-Lauf verschluckt (TASK-218 FIX-3 — MAJOR).**
  `StatisticsActionViewModel` hatte zwei `catch { }` Blöcke um `await`-Aufrufe mit
  `CancellationToken` ohne vorgelagertes `catch (OperationCanceledException) { throw; }`.
  User-Abbruch während Statistik-Scan wurde ignoriert — der Lauf arbeitete alle
  Dateien mit "N/A"-Defaults durch.

- **SQLite IN-Clause Limit bei Langzeit-Timelapsen (TASK-218 FIX-4 — MINOR).**
  `ImportDatabase.AssignSequenceToFiles` expandierte `WHERE id IN @Ids` zu einem
  Parameter pro Element. Bei Sequenzen > 32 766 Fotos (Langzeit-Timelapse) folgten
  `SqliteException`s. Fix: `.Chunk(900)` — alle Chunks in einer Transaktion.

- **`IsInTimelapseFolder` NML-Verstoß + falscher Substring-Match (TASK-218 FIX-5 — MINOR).**
  Hardcoded `"Timelapse"` (andere Schreibweise als `FolderNameConstants.TimeLapse = "TimeLapse"`)
  und nackter `Contains` über den vollen Pfad — Source-Pfade wie `D:\TimelapseRig\clip.mp4`
  wurden fälschlich in den TimeLapse-Ordner einsortiert. Fix: `FolderNameConstants.TimeLapse`
  + Slash-umschlossener Segment-Match analog zur existierenden Konvention in
  `FolderNameConstants.cs:287`.

- **Leere GUI-catch-Blöcke erschweren Fehlerdiagnose (TASK-218 FIX-6 — WARN).**
  Mehrere best-effort `catch { }` in `ImportViewModel` (DriveInfo, VolumeInfoReader,
  FingerprintService) hatten kein Logging. `_logger?.LogDebug(ex, ...)` ergänzt.
  Verhalten unverändert. ViewModels ohne Logger (`BurstTabViewModel`,
  `StabilizeActionViewModel`, `StatisticsActionViewModel`) nicht angepasst — im Bericht
  dokumentiert.

## [2.1.7] — 2026-06-02

### Fixed

- **Thumbnail-Dateien wurden nach Sort in heutiges-Datum-Ordner einsortiert (TASK-217).**
  `FolderSortService` scannte alle Dateien im Workbench inkl.
  `{workbench}/.umi/thumbnails/*.thumb.jpg` und `*.preview.jpg`. Da diese Dateien
  kein EXIF enthalten, fiel das Datum auf `File.GetLastWriteTime` (= heute) zurück
  → Thumbnails landeten in `{workbench}/2026-06-02/_unsorted/Photo/`.

  Fix: Neue SSOT-Methode `FolderNameConstants.IsInternalPath(string path)` erkennt
  Pfade die ein `.umi`- oder `.metadata`-Segment enthalten und schließt sie aus.
  `FolderSortService.SortCore`, `BurstVisualizerService.LoadFolderAsync` (der dort
  bereits inline duplizierte Filter), `ExifFieldAnalyzerService.AnalyzeFolderAsync`
  und `VerificationService.VerifyWorkbenchAsync` nutzen jetzt alle diese einzige
  Methode (F-217-01: gleicher Bug-Komplex, gleiches Release).

### Changed

- **"Organisieren" (Nach dem Import): Fotoserien-Erkennung jetzt standardmäßig
  aktiv.** Der "Fotoserien erkennen"-Toggle auf der Organisieren-Karte ist
  default ON — wer nach dem Import organisiert, will die Serien-Gruppierung in
  der Regel haben. Abschaltbar pro Lauf.

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
