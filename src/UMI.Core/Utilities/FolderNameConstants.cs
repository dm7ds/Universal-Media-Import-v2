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

using System.IO;

namespace UMI.Core.Utilities;

/// <summary>
/// Zentrale Konstanten fuer Workflow-Ordnernamen im Medienpfad-Layout.
/// Einzige Quelle der Wahrheit fuer alle Ordnernamen — kein Magic Literal.
/// </summary>
public static class FolderNameConstants
{
    /// <summary>Ordner fuer EIS-Off Videos die auf Gyroflow-Stabilisierung warten.</summary>
    public const string Gyroflow = "Gyroflow";

    /// <summary>Ordner fuer bereits stabilisierte Videos.</summary>
    public const string Stabilized = "Stabilized";

    /// <summary>Ordner fuer Zeitraffer-Sequenzen.</summary>
    public const string TimeLapse = "TimeLapse";

    /// <summary>Ordner fuer exportierte / fertig verarbeitete Medien.</summary>
    public const string Export = "Export";

    /// <summary>Ordner fuer Metadaten-Backups.</summary>
    public const string Metadata = "Metadata";

    /// <summary>Ordner fuer GPS-Tracks (GPX-Dateien).</summary>
    public const string Gps = "GPS";

    /// <summary>Ordner fuer Videos die auf Post-Processing warten (postprocess/).</summary>
    public const string PostProcess = "postprocess";

    /// <summary>Ordner fuer Video-Medien.</summary>
    public const string Video = "Video";

    /// <summary>Ordner fuer Photo-Medien.</summary>
    public const string Photo = "Photo";

    /// <summary>Unterordner fuer DaVinci-Exports innerhalb von postprocess/.</summary>
    public const string Exported = "exported";

    /// <summary>Unterordner fuer Einzelaufnahmen die keiner Burst-Sequenz angehören.</summary>
    public const string SingleShots = "Single_Shots";

    /// <summary>Internes UMI-Verzeichnis im Workbench-Root.</summary>
    public const string UmiDir = ".umi";

    /// <summary>UMI Lock-Datei.</summary>
    public const string UmiLock = ".umi.lock";

    /// <summary>Zentraler Metadaten-Ordner (neues Layout, Punkt-Prefix).</summary>
    public const string MetadataDir = ".metadata";

    /// <summary>Unterordner-Name für EXIF-Backups unter .umi/.</summary>
    public const string UmiMetadata = "metadata";

    /// <summary>Unterordner-Name für Prozess-History unter .umi/.</summary>
    public const string UmiHistory = "history";

    /// <summary>Unterordner-Name für optimierte GPS-Tracks unter .umi/.</summary>
    public const string UmiGps = "gps";

    /// <summary>Unterordner-Name für Thumbnail-Cache unter .umi/.</summary>
    public const string UmiThumbnails = "thumbnails";

    /// <summary>Unterordner-Name für Review-Sidecars unter .umi/.</summary>
    public const string UmiReview = "review";

    /// <summary>Unterordner-Name für Sequence-Sidecars unter .umi/.</summary>
    public const string UmiSequences = "sequences";

    /// <summary>Dateiname der Review-Sidecar-Datei.</summary>
    public const string ReviewSidecarFile = ".umi-review.json";

    /// <summary>Dateiname der Sequence-Sidecar-Datei.</summary>
    public const string SequenceSidecarFile = ".umi-sequences.json";

    /// <summary>Extension für Thumbnail-Cache-Dateien.</summary>
    public const string ThumbnailSuffix = ".thumb.jpg";

    /// <summary>Extension für Preview-Cache-Dateien.</summary>
    public const string PreviewSuffix = ".preview.jpg";

    /// <summary>Extension für Poster-Cache-Dateien (Legacy-Migration).</summary>
    public const string PosterSuffix = ".poster.jpg";

    /// <summary>Extension für optimierte GPX-Tracks.</summary>
    public const string OptimizedGpxSuffix = "_optimized.gpx";

    /// <summary>Extension für EXIF-Metadaten-Backup-Dateien.</summary>
    public const string MetaJsonSuffix = ".meta.json";

    /// <summary>Extension für Prozess-History-Dateien.</summary>
    public const string HistoryJsonSuffix = ".history.json";

    /// <summary>
    /// Zentrales Unterverzeichnis unter {Workbench}/.umi/ für alle UMI-Metadaten.
    /// Ersetze .metadata/ und verstreute Sidecars durch diesen einheitlichen Ablageort.
    /// </summary>
    public enum UmiSubDir
    {
        /// <summary>.umi/metadata/ — EXIF-Backups (.meta.json)</summary>
        Metadata,

        /// <summary>.umi/history/ — Prozess-History (.history.json)</summary>
        History,

        /// <summary>.umi/gps/ — Optimierte GPS-Tracks (_optimized.gpx)</summary>
        Gps,

        /// <summary>.umi/thumbnails/ — Thumbnail-Cache (.thumb.jpg, .preview.jpg)</summary>
        Thumbnails,

        /// <summary>.umi/review/ — Review-Sidecars (.umi-review.json)</summary>
        Review,

        /// <summary>.umi/sequences/ — Sequence-Sidecars (.umi-sequences.json)</summary>
        Sequences,
    }

    /// <summary>
    /// Gibt den Ordnernamen für das angegebene <see cref="UmiSubDir"/> zurück.
    /// Einzige Quelle der Wahrheit für alle SubDir-Namen — kein Magic Literal.
    /// </summary>
    /// <param name="subDir">Das Unterverzeichnis.</param>
    /// <returns>Name des Unterverzeichnisses (z.B. "metadata", "history").</returns>
    public static string GetSubDirName(UmiSubDir subDir) => subDir switch
    {
        UmiSubDir.Metadata   => UmiMetadata,
        UmiSubDir.History    => UmiHistory,
        UmiSubDir.Gps        => UmiGps,
        UmiSubDir.Thumbnails => UmiThumbnails,
        UmiSubDir.Review     => UmiReview,
        UmiSubDir.Sequences  => UmiSequences,
        _                    => throw new ArgumentOutOfRangeException(nameof(subDir), subDir, null)
    };

    /// <summary>
    /// Extrahiert die Uhrzeit (HH:mm) aus DJI-Dateinamen wie DJI_20260305165134_0001_D.MP4.
    /// Gibt null zurück wenn das Pattern nicht passt.
    /// </summary>
    public static string? ExtractTimeFromFilename(string filename)
    {

        var name = Path.GetFileNameWithoutExtension(filename);
        if (name.Length >= 16 && name.StartsWith("DJI_", StringComparison.OrdinalIgnoreCase))
        {
            var hour = name.Substring(12, 2);
            var min = name.Substring(14, 2);
            if (int.TryParse(hour, out var h) && int.TryParse(min, out var m) && h < 24 && m < 60)
                return $"{h:D2}:{m:D2}";
        }
        return null;
    }

    /// <summary>
    /// Extrahiert Datum (yyyy-MM-dd) und CameraId aus einem Workbench-Pfad.
    /// Pfadstruktur: Workbench/yyyy-MM-dd/CameraId/...
    /// </summary>
    public static (string? Date, string? CameraId) ExtractDateAndCamera(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/');

        for (var i = 0; i < segments.Length - 1; i++)
        {

            if (segments[i].Length == 10 &&
                char.IsDigit(segments[i][0]) &&
                segments[i][4] == '-' && segments[i][7] == '-')
            {
                var date = segments[i];
                var cameraId = i + 1 < segments.Length ? segments[i + 1] : null;
                return (date, cameraId);
            }
        }
        return (null, null);
    }

    /// <summary>
    /// Baut ein lesbares Prefix wie "CameraId · 2026-03-05 16:51 · " aus
    /// einem Workbench-Pfad und DJI-Dateinamen. Für Progress-Anzeigen.
    /// </summary>
    public static string FormatProgressPrefix(string inputPath, string fileName)
    {
        var (date, cam) = ExtractDateAndCamera(inputPath);
        var time = ExtractTimeFromFilename(fileName);
        var dateStr = (date, time) switch
        {
            (not null, not null) => $"{date} {time}",
            (not null, null) => date,
            _ => (string?)null
        };
        return (cam, dateStr) switch
        {
            (not null, not null) => $"{cam} \u00b7 {dateStr} \u00b7 ",
            (not null, null) => $"{cam} \u00b7 ",
            (null, not null) => $"{dateStr} \u00b7 ",
            _ => ""
        };
    }

    /// <summary>
    /// Exclusion flags for <see cref="IsVideoCandidate"/>.
    /// Combine with | to exclude multiple folders.
    /// </summary>
    [Flags]
    public enum VideoExclusion
    {
        None       = 0,
        Metadata   = 1 << 0,
        Gps        = 1 << 1,
        TimeLapse  = 1 << 2,
        Export     = 1 << 3,
        Exported   = 1 << 4,
        Stabilized = 1 << 5,
        Gyroflow   = 1 << 6,

        /// <summary>Metadata + GPS + TimeLapse + Export + Exported (shared base set).</summary>
        CommonExclusions = Metadata | Gps | TimeLapse | Export | Exported,
    }

    /// <summary>
    /// Returns true when the file is a .mp4 or .mov that is NOT inside any
    /// of the specified exclusion folders.
    /// SSOT for all video candidate checks across ProcessActions.
    /// </summary>
    public static bool IsVideoCandidate(string filePath, VideoExclusion exclusions)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is not (".mp4" or ".mov"))
            return false;

        var normalized = filePath.Replace('\\', '/');

        if (exclusions.HasFlag(VideoExclusion.Metadata) &&
            normalized.Contains($"/{Metadata}/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (exclusions.HasFlag(VideoExclusion.Gps) &&
            normalized.Contains($"/{Gps}/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (exclusions.HasFlag(VideoExclusion.TimeLapse) &&
            normalized.Contains($"/{TimeLapse}/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (exclusions.HasFlag(VideoExclusion.Export) &&
            normalized.Contains($"/{Export}/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (exclusions.HasFlag(VideoExclusion.Exported) &&
            normalized.Contains($"/{PostProcess}/exported/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (exclusions.HasFlag(VideoExclusion.Stabilized) &&
            normalized.Contains($"/{Stabilized}/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (exclusions.HasFlag(VideoExclusion.Gyroflow) &&
            normalized.Contains($"/{Gyroflow}/", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Berechnet den Output-Pfad fuer ein stabilisiertes Video.
    /// Ersetzt .../Gyroflow/... mit .../Stabilized/... im Pfad.
    /// Beispiel: date/Camera/Gyroflow/file.mp4 → date/Camera/Stabilized/file.mp4
    /// </summary>
    public static string CalculateStabilizedOutputPath(string inputPath)
    {
        var normalized = inputPath.Replace('\\', '/');
        var gyroflowSegment = "/" + Gyroflow + "/";
        var stabilizedSegment = "/" + Stabilized + "/";

        var idx = normalized.IndexOf(gyroflowSegment, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var result = normalized[..idx] + stabilizedSegment + normalized[(idx + gyroflowSegment.Length)..];
            return Path.GetFullPath(result);
        }

        var dir = Path.GetDirectoryName(inputPath)!;
        var fileName = Path.GetFileName(inputPath);
        return Path.GetFullPath(Path.Combine(dir, Stabilized, fileName));
    }
}
