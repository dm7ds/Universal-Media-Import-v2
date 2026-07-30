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

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using MetadataExtractor.Formats.QuickTime;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;

namespace UMI.Core.Utilities;

/// <summary>
/// Liest Metadaten direkt aus Dateien (ohne ExifTool).
/// </summary>
public class MetadataReader
{
    private readonly ILogger<MetadataReader>? _logger;

    public MetadataReader(ILogger<MetadataReader>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Liest vollständige Metadaten (EXIF + Canon Makernotes + QuickTime).
    /// Für Burst-Detection, Sequenz-Erkennung und Import.
    ///
    /// Robust against unreadable files: when MetadataExtractor cannot parse the
    /// file (corrupted, unsupported format, locked, IO error), returns a minimal
    /// PhotoMetadata derived from filesystem information only. The previous
    /// version let the underlying exception escape, which terminated the entire
    /// Parallel.ForEachAsync scan loop on the first bad file in a batch.
    /// </summary>
    /// <summary>
    /// virtual erlaubt Test-Subklassen die gezielt PhotoMetadata mit gesetztem CameraSerial
    /// zurückgeben, ohne echte EXIF-Dateien zu brauchen (Integration-Tests Welle 2).
    /// Alle produktiven Consumer (FolderSortService, GpsService, …) nutzen die Basis-Impl.
    /// </summary>
    public virtual PhotoMetadata ReadPhotoMetadata(string filePath)
    {
        try
        {
            return ReadPhotoMetadata(filePath, ImageMetadataReader.ReadMetadata(filePath));
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex,
                "ReadMetadata fehlgeschlagen für {File} — falle auf Filesystem-Daten zurück", filePath);
            return BuildFilesystemFallback(filePath);
        }
    }

    /// <summary>
    /// Returns a PhotoMetadata that contains only what the filesystem itself can give us
    /// (capture-date approximated by LastWriteTime, no EXIF / Makernote / GPS data).
    /// Used as the fallback when MetadataExtractor refuses the file.
    /// </summary>
    private static PhotoMetadata BuildFilesystemFallback(string filePath)
    {
        FileInfo? fi = null;
        try { fi = new FileInfo(filePath); } catch { /* fall through with fi == null */ }

        return new PhotoMetadata
        {
            FilePath   = filePath,
            FileName   = fi?.Name ?? Path.GetFileName(filePath) ?? string.Empty,
            CreateDate = fi is { Exists: true } ? fi.LastWriteTime : null,
            FileSize   = fi is { Exists: true } ? fi.Length : 0,
        };
    }

    public PhotoMetadata ReadPhotoMetadata(string filePath, IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            var exifSub = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            double? exposureTime = null;
            if (exifSub != null && exifSub.TryGetRational(ExifDirectoryBase.TagExposureTime, out var rational))
            {
                exposureTime = rational.ToDouble();
            }

            DateTime? createDate = null;
            if (exifSub?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dto) == true)
            {
                createDate = dto;
            }

            // Typsicherer Lookup — CanonMakernoteDirectory für alle Canon-Makernote-Felder
            // (ContinuousDrive, ExposureMode, Body-Serial). Eine Variable, ein Lookup.
            var canonDir = directories.OfType<CanonMakernoteDirectory>().FirstOrDefault();

            int? continuousDrive = null;
            if (canonDir != null)
            {
                var driveTag = canonDir.Tags
                    .FirstOrDefault(t => t.Name == "Continuous Drive Mode");
                if (driveTag != null)
                {
                    if (canonDir.TryGetInt32(driveTag.Type, out var driveValue))
                    {
                        continuousDrive = driveValue;
                    }
                    else
                    {
                        continuousDrive = ParseContinuousDrive(driveTag.Description);
                    }
                }
            }

            int? exposureMode = null;
            string? exposureModeDesc = null;
            if (canonDir != null)
            {
                var modeTag = canonDir.Tags
                    .FirstOrDefault(t => t.Name == "Exposure Mode");
                if (modeTag != null)
                {
                    if (canonDir.TryGetInt32(modeTag.Type, out var modeValue))
                    {
                        exposureMode = modeValue;
                    }
                    exposureModeDesc = modeTag.Description;
                }
            }

            // Body-Serial: Canon Makernote (TagCanonSerialNumber = 0x000C) zuerst,
            // dann Standard-EXIF BodySerialNumber (0xA431) als modellübergreifender Fallback.
            // NIEMALS Lens-Serial (TagLensSerialNumber 0xA435) — kann '0000000000' sein.
            // Fallback-Reihenfolge: Canon SerialNumber → EXIF BodySerialNumber → null.
            string? cameraSerial = null;
            if (canonDir != null)
            {
                cameraSerial = canonDir.GetString(CanonMakernoteDirectory.TagCanonSerialNumber)?.Trim();
                if (string.IsNullOrEmpty(cameraSerial))
                    cameraSerial = null;
            }
            // EXIF BodySerialNumber als Fallback (greift auch für Nicht-Canon-Kameras).
            if (cameraSerial == null && exifSub != null)
            {
                cameraSerial = exifSub.GetString(ExifDirectoryBase.TagBodySerialNumber)?.Trim();
                if (string.IsNullOrEmpty(cameraSerial))
                    cameraSerial = null;
            }

            var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var cameraModel = exifIfd0?.GetString(ExifDirectoryBase.TagModel)?.Trim();
            var lensModel = exifSub?.GetString(ExifDirectoryBase.TagLensModel)?.Trim();

            var gpsDir = directories.OfType<GpsDirectory>().FirstOrDefault();
            double? gpsLat = null;
            double? gpsLon = null;
            if (gpsDir != null)
            {
                if (gpsDir.TryGetDouble(GpsDirectory.TagLatitude, out var lat))
                    gpsLat = lat;
                if (gpsDir.TryGetDouble(GpsDirectory.TagLongitude, out var lon))
                    gpsLon = lon;
            }

            var isVideo = IsVideoExtension(fileInfo.Extension);
            TimeSpan? duration = null;
            if (isVideo)
            {
                var qtHeader = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (qtHeader != null)
                {

                    if (qtHeader.TryGetInt64(QuickTimeMovieHeaderDirectory.TagDuration, out var durationMs))
                    {
                        duration = TimeSpan.FromMilliseconds(durationMs);
                    }

                    if (createDate == null &&
                        qtHeader.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var qtDate))
                    {
                        createDate = DateTime.SpecifyKind(qtDate, DateTimeKind.Utc).ToLocalTime();
                    }
                }
            }

            var exifValues = new Dictionary<string, double>();

            TryAddExifValue(exifValues, exifSub, ExifDirectoryBase.TagExposureTime, "ExposureTime");
            TryAddExifValue(exifValues, exifSub, ExifDirectoryBase.TagIsoEquivalent, "ISO");
            TryAddExifValue(exifValues, exifSub, ExifDirectoryBase.TagFocalLength, "FocalLength");
            TryAddExifValue(exifValues, exifSub, ExifDirectoryBase.TagFNumber, "Aperture");
            TryAddExifValue(exifValues, exifSub, ExifDirectoryBase.TagShutterSpeed, "ShutterSpeed");
            TryAddExifValue(exifValues, exifSub, ExifDirectoryBase.TagExposureBias, "ExposureCompensation");

            TryAddCanonExifValue(exifValues, canonDir, "Continuous Drive Mode", "ContinuousDrive");
            TryAddCanonExifValue(exifValues, canonDir, "Exposure Mode", "ExposureMode");

            return new PhotoMetadata
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                CreateDate = createDate,
                ExposureTime = exposureTime,
                ContinuousDrive = continuousDrive,
                ExposureMode = exposureMode,
                ExposureModeDescription = exposureModeDesc,
                FileSize = fileInfo.Length,
                CameraModel = cameraModel,
                LensModel = lensModel,
                CameraSerial = cameraSerial,
                IsVideo = isVideo,
                Duration = duration,
                GpsLatitude = gpsLat,
                GpsLongitude = gpsLon,
                ExifValues = exifValues,
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Lesen von Metadaten: {File}",
                Path.GetFileName(filePath));

            var fi = new FileInfo(filePath);
            return new PhotoMetadata
            {
                FilePath = filePath,
                FileName = fi.Name,
                CreateDate = fi.LastWriteTime,
                FileSize = fi.Length,
                IsVideo = IsVideoExtension(fi.Extension),
                ExifValues = new Dictionary<string, double>(),
            };
        }
    }

    /// <summary>
    /// Liest Metadaten von mehreren Dateien parallel.
    /// </summary>
    public async Task<List<PhotoMetadata>> ReadPhotoMetadataBatchAsync(
        IEnumerable<string> filePaths,
        int maxParallelism = 4,
        IProgress<(int current, int total, string fileName)>? progress = null,
        CancellationToken ct = default)
    {
        var paths = filePaths.ToList();
        var results = new ConcurrentBag<PhotoMetadata>();
        var processed = 0;

        await Parallel.ForEachAsync(paths,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism,
                CancellationToken = ct
            },
            (path, token) =>
            {
                var metadata = ReadPhotoMetadata(path);
                results.Add(metadata);

                var count = Interlocked.Increment(ref processed);
                progress?.Report((count, paths.Count, metadata.FileName));

                return ValueTask.CompletedTask;
            });

        return results.OrderBy(r => r.CreateDate ?? DateTime.MaxValue).ToList();
    }

    /// <summary>
    /// Extrahiert das Aufnahmedatum aus einer Datei.
    /// Versucht: CreateDate, DateTimeOriginal, ModifyDate aus EXIF/QuickTime.
    /// </summary>
    public DateTime? GetCreateDate(string filePath)
    {

        var metadata = ReadPhotoMetadata(filePath);
        return metadata.CreateDate;
    }

    /// <summary>
    /// Extrahiert das Datum aus Dateiname (DJI-Format) oder Metadaten.
    /// Fallback: Filesystem-Timestamp.
    /// </summary>
    public DateTime ExtractDateFromFile(FileInfo file)
    {

        var textExtensions = new[] { ".srt", ".gpx", ".txt", ".log", ".xml" };
        if (textExtensions.Contains(file.Extension.ToLowerInvariant()))
        {
            var textDates = new[] { file.LastWriteTime, file.CreationTime };
            var textDate = textDates.Min();
            _logger?.LogDebug("Datum aus Filesystem (Text-Datei): {Date} für {File}", textDate, file.Name);
            return textDate;
        }

        var fileName = Path.GetFileNameWithoutExtension(file.Name);
        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"DJI_(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})");

        if (match.Success)
        {
            try
            {
                int year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                int day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                int hour = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
                int minute = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
                int second = int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture);

                _logger?.LogDebug("Datum aus DJI-Dateiname: {Date} für {File}",
                    new DateTime(year, month, day, hour, minute, second), file.Name);
                return new DateTime(year, month, day, hour, minute, second);
            }
            catch
            {

            }
        }

        var metadataDate = GetCreateDate(file.FullName);
        if (metadataDate.HasValue)
        {
            return metadataDate.Value;
        }

        var dates = new[] { file.LastWriteTime, file.CreationTime };
        var fallbackDate = dates.Min();

        _logger?.LogDebug("Datum aus Filesystem (Fallback): {Date} für {File}", fallbackDate, file.Name);
        return fallbackDate;
    }

    /// <summary>
    /// Parst ContinuousDrive aus Canon Makernote Description.
    /// </summary>
    private static int ParseContinuousDrive(string? description)
    {
        if (string.IsNullOrEmpty(description)) return 0;

        if (description.Contains("Single", StringComparison.OrdinalIgnoreCase))
            return 0;

        var match = Regex.Match(description, @"\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
            return value;

        if (description.Contains("Continuous", StringComparison.OrdinalIgnoreCase))
            return 1;

        return 0;
    }

    /// <summary>
    /// Prüft ob Dateiendung ein Video ist.
    /// </summary>
    private static bool IsVideoExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".mov" or ".avi" or ".mkv" => true,
            _ => false
        };
    }

    /// <summary>
    /// Versucht einen numerischen EXIF-Wert aus ExifSubIfdDirectory zu lesen und zum Dictionary hinzuzufügen.
    /// Unterstützt Rational, Int32 und Double Typen.
    /// </summary>
    private static void TryAddExifValue(
        Dictionary<string, double> dict,
        ExifSubIfdDirectory? exifDir,
        int tagId,
        string fieldName)
    {
        if (exifDir == null) return;

        if (exifDir.TryGetRational(tagId, out var rational))
        {
            dict[fieldName] = rational.ToDouble();
            return;
        }

        if (exifDir.TryGetInt32(tagId, out var intValue))
        {
            dict[fieldName] = intValue;
            return;
        }

        if (exifDir.TryGetDouble(tagId, out var doubleValue))
        {
            dict[fieldName] = doubleValue;
        }
    }

    /// <summary>
    /// Versucht einen numerischen Wert aus Canon Makernote (oder anderem Directory) zu lesen.
    /// Sucht Tag nach Name und extrahiert Int32-Wert.
    /// </summary>
    private static void TryAddCanonExifValue(
        Dictionary<string, double> dict,
        MetadataExtractor.Directory? canonDir,
        string tagName,
        string fieldName)
    {
        if (canonDir == null) return;

        var tag = canonDir.Tags.FirstOrDefault(t => t.Name == tagName);
        if (tag == null) return;

        if (canonDir.TryGetInt32(tag.Type, out var value))
        {
            dict[fieldName] = value;
        }
    }
}
