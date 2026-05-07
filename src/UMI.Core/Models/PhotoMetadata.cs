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

namespace UMI.Core.Models;

/// <summary>
/// Vollständige Metadaten eines Fotos/Videos, gelesen via MetadataExtractor.
/// Enthält alle Felder für Burst-Detection, Sequenz-Erkennung und Import.
/// </summary>
public record PhotoMetadata
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTime? CreateDate { get; init; }
    public double? ExposureTime { get; init; }
    public int? ContinuousDrive { get; init; }
    public int? ExposureMode { get; init; }
    public string? ExposureModeDescription { get; init; }
    public long FileSize { get; init; }
    public string? CameraModel { get; init; }
    public string? LensModel { get; init; }
    public bool IsVideo { get; init; }

    public TimeSpan? Duration { get; init; }
    public double? GpsLatitude { get; init; }
    public double? GpsLongitude { get; init; }

    /// <summary>
    /// Alle numerischen EXIF-Werte als Key-Value Paare.
    /// Keys sind EXIF-Feldnamen (z.B. "ExposureTime", "ISO", "FocalLength", "Aperture").
    /// Wird von MetadataReader befüllt und von Burst-Detection gelesen.
    /// Ermöglicht flexible EXIF-basierte Bedingungen in Burst-Profilen ohne Code-Änderungen.
    /// </summary>
    public Dictionary<string, double> ExifValues { get; init; } = new();
}
