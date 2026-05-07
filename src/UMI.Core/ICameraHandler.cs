// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This file is part of UMI - Universal Media Import.
//
//     UMI - Universal Media Import is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     UMI - Universal Media Import is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with UMI - Universal Media Import.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Services;

namespace UMI.Core;

/// <summary>
/// Interface für modulare Kamera-Handler.
/// Jede Kamera implementiert dieses Interface als Plugin.
/// </summary>
public interface ICameraHandler
{
    /// <summary>
    /// Eindeutige Kamera-ID (z.B. "GoPro11", "MyDSLR", "MyCamera").
    /// Wird in config.json als Key verwendet.
    /// </summary>
    string CameraId { get; }

    /// <summary>
    /// Anzeigename der Kamera (z.B. "GoPro Hero 11 Black").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Hersteller der Kamera (z.B. "GoPro", "Canon").
    /// </summary>
    string Manufacturer { get; }

    /// <summary>
    /// Typ der Kamera (Action, Drone, Mirrorless, etc.).
    /// Referenziert Typ-Definition aus Presets/types/*.json.
    /// </summary>
    string CameraType { get; }

    /// <summary>
    /// Prüft ob diese Datei von dieser Kamera unterstützt wird.
    /// </summary>
    Task<bool> SupportsFileAsync(FileInfo file);

    /// <summary>
    /// Validiert die Kamera-Konfiguration.
    /// </summary>
    Task<ValidationResult> ValidateConfigAsync(CameraConfig config);
}

/// <summary>
/// Kontext für den Import-Vorgang.
/// </summary>
public class ImportContext
{
    public required string CameraId { get; set; }
    public required CameraConfig Config { get; set; }
    public required string SourcePath { get; set; }
    public required string WorkbenchPath { get; set; }
    public required GlobalSettings GlobalSettings { get; set; }

    public bool InjectGps { get; set; }
    public bool Stabilize { get; set; }
    public string StabilizeMode { get; set; } = "all";
    public bool ForceStabilize { get; set; }
    public bool DryRun { get; set; }

    /// <summary>
    /// Wenn true: Keine EIS-basierte Sortierung beim Import.
    /// Alle Videos gehen nach Video/, egal ob EIS an oder aus.
    /// Default: false (EIS-Sortierung aktiv wenn Kamera eis_detection unterstützt).
    /// </summary>
    public bool NoEisSort { get; set; } = false;

    /// <summary>
    /// Wenn true: Import-History ignorieren, alle Dateien gegen Workbench prüfen (--full).
    /// Nützlich wenn Workbench leer ist und alles neu importiert werden soll.
    /// </summary>
    public bool FullImport { get; set; } = false;

    /// <summary>
    /// Wenn true: Import-History vor dem Scan löschen (--reset-history).
    /// Danach normaler Import (History-Datei wird neu aufgebaut).
    /// </summary>
    public bool ResetHistory { get; set; } = false;

    /// <summary>
    /// Ad-hoc Import aus Ordner ohne CameraConfig (--folder).
    /// Wenn gesetzt: camera_folders=false, kein History-Tracking.
    /// </summary>
    public bool IsAdHocFolder { get; set; } = false;

    /// <summary>
    /// Wenn true: Videos mit Timestamp-Prefix umbenennen (yyyyMMdd_HHmmss_OriginalName).
    /// Überschreibt CameraFeatures.RenameVideos aus der Config.
    /// Default: false (Config-Wert gilt).
    /// </summary>
    public bool RenameVideos { get; set; } = false;

    /// <summary>
    /// Wenn true: GoPro-Dateien in sortierbare Namen umbenennen (GoPro_0001_c01.MP4).
    /// Überschreibt CameraFeatures.GoProRename aus der Config.
    /// Default: false (Config-Wert gilt).
    /// </summary>
    public bool GoProRename { get; set; } = false;

    /// <summary>
    /// Wenn true: Videos benötigen externe Nachbearbeitung — werden nach Video/postprocess/ geroutet.
    /// GPS-Injection wird beim Import übersprungen (DVR verliert GPS-Daten).
    /// Überschreibt CameraFeatures.PostProcess aus der Config.
    /// Default: false (Config-Wert gilt).
    /// </summary>
    public bool PostProcess { get; set; } = false;

    /// <summary>
    /// Optional: Nur Medien importieren die AB diesem Zeitpunkt erstellt wurden.
    /// Null = kein unteres Limit.
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Optional: Nur Medien importieren die BIS zu diesem Zeitpunkt erstellt wurden.
    /// Null = kein oberes Limit.
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>True when any date filter is active.</summary>
    public bool HasDateFilter => DateFrom.HasValue || DateTo.HasValue;

    public IProgress<ImportProgress>? Progress { get; set; }
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Optional: Gyroflow per-frame render progress callback.
    /// Set by the Orchestrator when an IProgressReporter is available.
    /// </summary>
    public IProgress<GyroflowRenderProgress>? RenderProgress { get; set; }

    /// <summary>
    /// Optional: Gyroflow batch-level progress callback (video x/y).
    /// Set by the Orchestrator when an IProgressReporter is available.
    /// </summary>
    public IProgress<StabilizationProgress>? StabilizationProgress { get; set; }
}

/// <summary>
/// Ergebnis eines Import-Vorgangs.
/// </summary>
public class ImportResult
{
    public int TotalFiles { get; set; }
    public int ImportedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int ErrorFiles { get; set; }

    public int VideosImported { get; set; }
    public int PhotosImported { get; set; }

    public int VideosStabilized { get; set; }

    /// <summary>
    /// Anzahl gebauter GPX-Dateien (beim Import).
    /// GPS-Injection ins Video erfolgt erst beim Restore.
    /// </summary>
    public int VideosWithGps { get; set; }

    /// <summary>
    /// Anzahl Videos nach Gyroflow/ sortiert (EIS aus, brauchen Stabilisierung).
    /// </summary>
    public int VideosEisSorted { get; set; }

    /// <summary>
    /// Anzahl Videos nach Video/ sortiert (EIS an, bereits stabilisiert).
    /// </summary>
    public int VideosWithEis { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Fortschritt des Import-Vorgangs.
/// </summary>
public class ImportProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;

    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

/// <summary>
/// Validierungs-Ergebnis.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Woher kommen die Dateien einer Kamera?
/// Default: SdCard (Backward-Compatible).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SourceType
{
    /// <summary>SD-Karte: Auto-Erkennung via DriveWatcher</summary>
    [JsonPropertyName("sd")]
    SdCard,

    /// <summary>Fester Ordner auf Festplatte/NAS: FileSystemWatcher + History-Tracking</summary>
    [JsonPropertyName("fixed_path")]
    FixedPath,

    /// <summary>MTP-Gerät: Zugriff via Windows Portable Devices API (MediaDevices)</summary>
    [JsonPropertyName("mtp")]
    MTP
}

/// <summary>
/// Kamera-Konfiguration (aus config.json).
/// </summary>
public class CameraConfig
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("camera_type")]
    public string CameraType { get; set; } = "Unknown";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Seriennummer der Kamera (optional, für SD-Karten Auto-Erkennung).
    /// Wird beim ersten Erkennen automatisch gespeichert.
    /// </summary>
    [JsonPropertyName("serial_number")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Ordnername in Workbench (z.B. "GoPro11"). Falls nicht angegeben, wird CameraId verwendet.
    /// Erlaubt flexible Ordnerstruktur: CameraId != Ordnername.
    /// </summary>
    [JsonPropertyName("folder_name")]
    public string? FolderName { get; set; }

    [JsonPropertyName("features")]
    public CameraFeatures Features { get; set; } = new();

    [JsonPropertyName("file_types")]
    public CameraFileTypes FileTypes { get; set; } = new();

    [JsonPropertyName("paths")]
    public CameraPaths Paths { get; set; } = new();

    /// <summary>
    /// Woher kommen die Dateien? Default: SdCard (Backward-Compatible).
    /// </summary>
    [JsonPropertyName("source_type")]
    public SourceType SourceType { get; set; } = SourceType.SdCard;

    /// <summary>
    /// Fester Quell-Pfad (nur bei source_type = fixed_path).
    /// Wird rekursiv nach bekannten Media-Extensions gescannt.
    /// </summary>
    [JsonPropertyName("source_path")]
    public string? SourcePath { get; set; }

    /// <summary>
    /// Bei fixed_path: Unterordner-Struktur flach importieren (Ordnername wird Dateiname-Prefix)?
    /// true  = Tom/IMG_001.jpg → Tom_IMG_001.jpg (Default, verhindert Namenskollisionen)
    /// false = Unterordner beibehalten: Tom/IMG_001.jpg bleibt Tom/IMG_001.jpg
    /// </summary>
    [JsonPropertyName("flatten_source")]
    public bool FlattenSource { get; set; } = true;

    /// <summary>
    /// Typisierte Burst-Detection-Konfiguration (Thresholds, EXIF-Felder, etc.).
    /// </summary>
    [JsonPropertyName("burst_detection_config")]
    public Models.BurstDetectionConfig? BurstDetectionConfig { get; set; }

    /// <summary>
    /// Post-Processing Konfiguration (Gyroflow, Racerender, etc.).
    /// Kamera-spezifische Überschreibung der globalen PostProcessing-Einstellungen.
    /// </summary>
    [JsonPropertyName("post_processing")]
    public Configuration.PostProcessingConfig PostProcessing { get; set; } = new();

    /// <summary>
    /// Sortierungsreihenfolge für Drag & Drop (0 = Default, aufsteigend).
    /// Kameras ohne expliziten sort_order werden nach Name sortiert (Fallback).
    /// </summary>
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Per-camera overrides for SimpleOnCard placement.
    /// Key = canonical feature key (e.g. "gps_injection").
    /// When present, overrides the preset's simple_on_card value for this camera.
    /// Empty means: use preset defaults for all features.
    /// </summary>
    [JsonPropertyName("simple_on_card_overrides")]
    public Dictionary<string, bool> SimpleOnCardOverrides { get; set; } = new();

    /// <summary>
    /// Per-camera feature availability overrides.
    /// Key = canonical feature key (e.g. "gps_injection").
    /// When present, overrides the profile's Available flag for this camera.
    /// true = feature available on this camera, false = hidden from Import view.
    /// Empty means: use profile defaults for all features.
    /// </summary>
    [JsonPropertyName("available_overrides")]
    public Dictionary<string, bool> AvailableOverrides { get; set; } = new();

    /// <summary>
    /// Custom-Settings pro Kamera (flexibel für jede Kamera-Implementation).
    /// </summary>
    [JsonPropertyName("custom_settings")]
    public Dictionary<string, object?> CustomSettings { get; set; } = new();
}

public class CameraFeatures
{
    [JsonPropertyName("gps_injection")]
    public bool GpsInjection { get; set; }

    [JsonPropertyName("gyroflow")]
    public bool Gyroflow { get; set; }

    [JsonPropertyName("burst_detection")]
    public bool BurstDetection { get; set; }

    [JsonPropertyName("metadata_backup")]
    public bool MetadataBackup { get; set; }

    [JsonPropertyName("lens_correction")]
    public bool LensCorrection { get; set; }

    [JsonPropertyName("eis_detection")]
    public bool EisDetection { get; set; }

    /// <summary>
    /// Videos benötigen externe Nachbearbeitung (z.B. Color Grading in DaVinci Resolve).
    /// Import-Routing: Videos → Video/postprocess/ statt Video/.
    /// GPS-Injection wird beim Import übersprungen (DVR verliert GPS-Daten).
    /// </summary>
    [JsonPropertyName("post_process")]
    public bool PostProcess { get; set; }

    /// <summary>
    /// Videos beim Import mit Timestamp-Prefix versehen (yyyyMMdd_HHmmss_OriginalName).
    /// Nur wenn kein Timestamp bereits im Dateinamen erkannt wird.
    /// CLI-Flag --rename-videos überschreibt diesen Wert.
    /// Default: false.
    /// </summary>
    [JsonPropertyName("rename_videos")]
    public bool RenameVideos { get; set; }

    /// <summary>
    /// GoPro-Dateien beim Import in sortierbare Namen umbenennen (GoPro_0001_c01.MP4).
    /// Erkennt Legacy (GOPR/GP) und Modern (GH/GX) Patterns automatisch.
    /// CLI-Flag --gopro-rename überschreibt diesen Wert.
    /// Default: false.
    /// </summary>
    [JsonPropertyName("gopro_rename")]
    public bool GoProRename { get; set; }

    /// <summary>
    /// Thumbnails für RAW-Dateien nach dem Import automatisch generieren.
    /// Ruft IThumbnailCacheService.WarmCacheAsync() nach dem Copy-Schritt auf.
    /// Default: false.
    /// </summary>
    [JsonPropertyName("generate_thumbnails")]
    public bool GenerateThumbnails { get; set; }

    /// <summary>
    /// Returns the value of a feature flag by its canonical key.
    /// SSOT: the ONE place where feature-key → property-getter is defined.
    /// </summary>
    public bool GetByKey(string key) => key switch
    {
        FeatureKeys.GpsInjection   => GpsInjection,
        FeatureKeys.Gyroflow       => Gyroflow,
        FeatureKeys.BurstDetection => BurstDetection,
        FeatureKeys.MetadataBackup => MetadataBackup,
        FeatureKeys.EisDetection   => EisDetection,
        FeatureKeys.LensCorrection => LensCorrection,
        FeatureKeys.PostProcess    => PostProcess,
        FeatureKeys.RenameVideos   => RenameVideos,
        FeatureKeys.GoProRename        => GoProRename,
        FeatureKeys.GenerateThumbnails => GenerateThumbnails,
        _                              => false,
    };

    /// <summary>
    /// Sets a feature flag by its canonical key.
    /// SSOT: the ONE place where feature-key → property-setter is defined.
    /// </summary>
    public void SetByKey(string key, bool value)
    {
        switch (key)
        {
            case FeatureKeys.GpsInjection:   GpsInjection   = value; break;
            case FeatureKeys.Gyroflow:       Gyroflow       = value; break;
            case FeatureKeys.BurstDetection: BurstDetection = value; break;
            case FeatureKeys.MetadataBackup: MetadataBackup = value; break;
            case FeatureKeys.EisDetection:   EisDetection   = value; break;
            case FeatureKeys.LensCorrection: LensCorrection = value; break;
            case FeatureKeys.PostProcess:    PostProcess    = value; break;
            case FeatureKeys.RenameVideos:   RenameVideos   = value; break;
            case FeatureKeys.GoProRename:        GoProRename        = value; break;
            case FeatureKeys.GenerateThumbnails: GenerateThumbnails = value; break;
        }
    }

    /// <summary>
    /// Builds a <see cref="CameraFeatures"/> instance from a preset's feature dictionary,
    /// enabling every feature that has <c>available = true</c> AND <c>enabled_by_default = true</c>.
    /// Delegates to SetByKey — SSOT for feature-key → property mapping.
    /// </summary>
    public static CameraFeatures BuildFromPreset(Dictionary<string, FeatureDefinition>? features)
    {
        var result = new CameraFeatures();
        if (features == null) return result;

        foreach (var (key, def) in features)
        {
            if (!def.Available || !def.EnabledByDefault) continue;
            result.SetByKey(key, true);
        }

        return result;
    }
}

public class CameraFileTypes
{
    [JsonPropertyName("video")]
    public string[] Video { get; set; } = Array.Empty<string>();

    [JsonPropertyName("photo")]
    public string[] Photo { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Builds a <see cref="CameraFileTypes"/> instance from a preset's
    /// <c>default_file_types</c> section.
    /// SSOT: Extensions are defined in the .umi preset — never duplicated in code.
    /// Returns empty arrays when no preset data is available.
    /// </summary>
    public static CameraFileTypes BuildFromPreset(Models.DefaultFileTypes? defaults) =>
        new()
        {
            Video = defaults?.Video ?? Array.Empty<string>(),
            Photo = defaults?.Photo ?? Array.Empty<string>(),
        };
}

public class CameraPaths
{
    [JsonPropertyName("sd_source")]
    public string? SdSource { get; set; }

    [JsonPropertyName("custom_gpx_path")]
    public string? CustomGpxPath { get; set; }
}

/// <summary>
/// Globale Settings (aus config.json).
/// </summary>
public class GlobalSettings
{
    [JsonPropertyName("paths")]
    public required GlobalPaths Paths { get; set; }

    [JsonPropertyName("logging")]
    public LoggingSettings Logging { get; set; } = new();

    [JsonPropertyName("workflow")]
    public WorkflowSettings Workflow { get; set; } = new();
}

public class GlobalPaths
{
    [JsonPropertyName("workbench")]
    public required string Workbench { get; set; }

    [JsonPropertyName("projects")]
    public required string Projects { get; set; }

    [JsonPropertyName("gpx_source")]
    public required string GpxSource { get; set; }

    [JsonPropertyName("tools")]
    public required ToolPaths Tools { get; set; }

    [JsonPropertyName("log_directory")]
    public string LogDirectory { get; set; } = "./logs";
}

public class ToolPaths
{
    [JsonPropertyName("exiftool")]
    public required string ExifTool { get; set; }

    [JsonPropertyName("gyroflow")]
    public string? Gyroflow { get; set; }

    [JsonPropertyName("ffprobe")]
    public string? FFprobe { get; set; }
}

public class LoggingSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("level")]
    public string Level { get; set; } = LogLevels.Info;

    [JsonPropertyName("console_output")]
    public bool ConsoleOutput { get; set; } = true;

    [JsonPropertyName("file_output")]
    public bool FileOutput { get; set; } = true;
}

public class WorkflowSettings
{
    [JsonPropertyName("make_clean")]
    public bool MakeClean { get; set; }

    [JsonPropertyName("create_backup")]
    public bool CreateBackup { get; set; } = true;

    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; }

    [JsonPropertyName("ignore_folders")]
    public string[] IgnoreFolders { get; set; } = Array.Empty<string>();
}
