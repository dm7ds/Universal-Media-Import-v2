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

using Microsoft.Extensions.Logging;
using UMI.Core;

namespace UMI.Cameras;

/// <summary>
/// Universeller Kamera-Handler ohne hardcodierte Kamera-Namen.
/// Alle kamera-spezifischen Features werden über config.json aktiviert.
/// Post-Import-Verarbeitung erfolgt nun via PreProcessingOrchestrator (Pipeline V2.1).
/// </summary>
public class UniversalCameraHandler : ICameraHandler
{
    private readonly ILogger<UniversalCameraHandler>? _logger;

    private string _cameraId = string.Empty;
    private string _displayName = string.Empty;
    private string _manufacturer = string.Empty;
    private string _cameraType = "Unknown";
    private string[] _videoFormats = Array.Empty<string>();
    private string[] _photoFormats = Array.Empty<string>();

    public string CameraId => _cameraId;
    public string DisplayName => _displayName;
    public string Manufacturer => _manufacturer;
    public string CameraType => _cameraType;

    public UniversalCameraHandler(ILogger<UniversalCameraHandler>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialisiert den Handler für eine spezifische Kamera aus der Config.
    /// </summary>
    public void Initialize(string cameraId, CameraConfig config)
    {
        _cameraId = cameraId;
        _displayName = config.Name;
        _manufacturer = config.Manufacturer ?? "Unknown";
        _cameraType = config.CameraType;
        _videoFormats = config.FileTypes.Video;
        _photoFormats = config.FileTypes.Photo;
    }

    public Task<bool> SupportsFileAsync(FileInfo file)
    {
        var ext = file.Extension.ToLowerInvariant();
        var supported = _videoFormats.Contains(ext) || _photoFormats.Contains(ext);
        return Task.FromResult(supported);
    }

    public Task<ValidationResult> ValidateConfigAsync(CameraConfig config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(config.Paths.SdSource))
        {
            warnings.Add("SD-Source nicht konfiguriert");
        }
        else if (!Directory.Exists(config.Paths.SdSource))
        {
            warnings.Add($"SD-Source existiert nicht: {config.Paths.SdSource}");
        }

        if (config.FileTypes.Video.Length == 0 && config.FileTypes.Photo.Length == 0)
        {
            errors.Add("Keine Dateitypen konfiguriert");
        }

        return Task.FromResult(new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        });
    }
}
