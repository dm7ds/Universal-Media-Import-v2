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

using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für SD-Karten Fingerprinting (Auto-Erkennung der Kamera).
/// </summary>
public interface ISdFingerprintService
{
    Task<SdFingerprint?> IdentifyCardAsync(string sdRootPath, CancellationToken ct = default);
    string? MatchCamera(SdFingerprint fingerprint, Dictionary<string, CameraConfig> cameras);
}

public class SdFingerprintService : ISdFingerprintService
{
    private readonly IExifToolWrapper _exifTool;
    private readonly ILogger<SdFingerprintService>? _logger;

    private static readonly string[] FingerprintExtensions =
        [.. FileExtensions.Photos, .. FileExtensions.Videos, ".lrf"];

    public SdFingerprintService(
        IExifToolWrapper exifTool,
        ILogger<SdFingerprintService>? logger = null)
    {
        _exifTool = exifTool;
        _logger = logger;
    }

    /// <summary>
    /// Identifiziert SD-Karte via Stufe 1 (version.txt) oder Stufe 2 (EXIF aus erstem Foto).
    /// </summary>
    public async Task<SdFingerprint?> IdentifyCardAsync(string sdRootPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(sdRootPath))
        {
            _logger?.LogWarning("SD-Pfad existiert nicht: {Path}", sdRootPath);
            return null;
        }

        var versionTxtPath = Path.Combine(sdRootPath, "MISC", "version.txt");
        if (File.Exists(versionTxtPath))
        {
            var fingerprint = await ParseVersionTxtAsync(versionTxtPath, ct);
            if (fingerprint != null)
            {
                _logger?.LogDebug("SD-Karte erkannt via version.txt: {Model} (Serial: {Serial})",
                    fingerprint.Model, fingerprint.SerialNumber);
                return fingerprint;
            }
        }

        if (!_exifTool.IsAvailable)
        {
            _logger?.LogDebug("ExifTool not configured — skipping EXIF fingerprint for: {Path}", sdRootPath);
            return null;
        }

        var mediaFiles = FindMediaFiles(sdRootPath).Take(10);
        foreach (var mediaFile in mediaFiles)
        {
            ct.ThrowIfCancellationRequested();

            var fingerprint = await ReadFingerprintFromPhotoAsync(mediaFile, ct);
            if (fingerprint != null)
            {
                _logger?.LogDebug("SD-Karte erkannt via EXIF: {Model} (Serial: {Serial})",
                    fingerprint.Model, fingerprint.SerialNumber);
                return fingerprint;
            }
        }

        _logger?.LogDebug("Keine Fingerprint-Daten in den ersten 10 Medien-Dateien gefunden: {Path}", sdRootPath);
        return null;
    }

    /// <summary>
    /// Matcht einen SD-Fingerprint gegen die konfigurierten Kameras.
    /// Returns: CameraId oder null wenn keine Übereinstimmung.
    /// Delegiert an <see cref="CameraSerialMatcher.FindCameraId"/> (SSOT für Serial-Match-Logik).
    /// </summary>
    public string? MatchCamera(SdFingerprint fingerprint, Dictionary<string, CameraConfig> cameras)
        => CameraSerialMatcher.FindCameraId(fingerprint.SerialNumber, cameras);

    /// <summary>
    /// Parst GoPro version.txt (JSON mit Keys wie "camera serial number").
    /// </summary>
    private async Task<SdFingerprint?> ParseVersionTxtAsync(string versionTxtPath, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(versionTxtPath, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var serial = GetJsonProperty(root, "camera serial number");
            var model = GetJsonProperty(root, "camera type");
            var firmware = GetJsonProperty(root, "firmware version");

            if (string.IsNullOrEmpty(serial))
            {
                _logger?.LogWarning("version.txt hat keine 'camera serial number'");
                return null;
            }

            return new SdFingerprint
            {
                SerialNumber = serial,
                Model = model,
                FirmwareVersion = firmware,
                DetectionMethod = "version.txt"
            };
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Fehler beim Parsen von version.txt: {Path}", versionTxtPath);
            return null;
        }
    }

    /// <summary>
    /// Liest SerialNumber + Model aus EXIF-Daten eines Fotos.
    /// </summary>
    private async Task<SdFingerprint?> ReadFingerprintFromPhotoAsync(string photoPath, CancellationToken ct)
    {
        try
        {
            var metadata = await _exifTool.ReadMetadataAsync(
                photoPath,
                fields: new[] { "SerialNumber", "Make", "Model", "FirmwareVersion", "InternalSerialNumber" },
                ct);

            var serial = metadata.TryGetValue("SerialNumber", out var s) ? s?.ToString()
                       : metadata.TryGetValue("InternalSerialNumber", out var i) ? i?.ToString()
                       : null;

            var make = metadata.TryGetValue("Make", out var mk) ? mk?.ToString() : null;
            var model = metadata.TryGetValue("Model", out var m) ? m?.ToString() : null;
            var firmware = metadata.TryGetValue("FirmwareVersion", out var f) ? f?.ToString() : null;

            if (string.IsNullOrEmpty(make) && string.IsNullOrEmpty(model) && string.IsNullOrEmpty(serial))
            {
                _logger?.LogDebug("Foto hat weder Make/Model noch SerialNumber: {Path}", photoPath);
                return null;
            }

            if (string.IsNullOrEmpty(serial))
            {
                _logger?.LogDebug("Foto hat Make/Model aber keine SerialNumber — Fingerprint ohne Serial: {Path}", photoPath);
            }

            return new SdFingerprint
            {
                SerialNumber = string.IsNullOrEmpty(serial) ? null : serial,
                Make = make,
                Model = model,
                FirmwareVersion = firmware,
                DetectionMethod = "exif-photo"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Lesen von EXIF aus Foto: {Path}", photoPath);
            return null;
        }
    }

    /// <summary>
    /// Findet Medien-Dateien (Fotos + Videos) auf der SD-Karte (max depth 3, alphabetisch sortiert).
    /// Schließt Video-Formate ein, da DJI Action Cams primär Videos aufnehmen und
    /// ExifTool Make/Model auch aus QuickTime-Metadaten (.mp4/.mov) lesen kann.
    /// Lazy Enumeration – Caller kann .Take(N) nutzen.
    /// </summary>
    private IEnumerable<string> FindMediaFiles(string sdRootPath)
    {
        IEnumerable<string> files = Enumerable.Empty<string>();

        try
        {
            files = Directory.EnumerateFiles(sdRootPath, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return FingerprintExtensions.Contains(ext);
                })
                .Where(f =>
                {

                    var relativePath = Path.GetRelativePath(sdRootPath, f);
                    var depth = relativePath.Split(Path.DirectorySeparatorChar).Length;
                    return depth <= 4;
                })
                .OrderBy(f => f);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Suchen nach Medien-Dateien auf SD-Karte: {Path}", sdRootPath);
        }

        return files;
    }

    /// <summary>
    /// Helper: Liest JSON-Property case-insensitive (für Keys mit Leerzeichen).
    /// </summary>
    private static string? GetJsonProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.GetString();
            }
        }

        return null;
    }
}
