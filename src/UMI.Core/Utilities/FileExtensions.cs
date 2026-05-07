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

namespace UMI.Core.Utilities;

/// <summary>
/// Zentrale Konstanten für unterstützte Dateitypen.
/// Single Source of Truth für File-Extensions.
/// </summary>
public static class FileExtensions
{
    /// <summary>
    /// Unterstützte Foto-Formate für EXIF-Analyse und Burst-Detection.
    /// Verwendet von: SdFingerprintService, ExifFieldAnalyzerService, BurstVisualizerService.
    /// </summary>
    public static readonly string[] Photos = { ".jpg", ".jpeg", ".cr3", ".dng", ".arw", ".raf", ".nef", ".cr2" };

    /// <summary>
    /// Unterstützte Video-Formate.
    /// </summary>
    public static readonly string[] Videos = { ".mp4", ".mov", ".avi", ".mkv" };

    /// <summary>
    /// Prüft ob eine Datei ein Foto ist (Case-Insensitive).
    /// </summary>
    public static bool IsPhoto(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return Array.Exists(Photos, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Prüft ob eine Datei ein Video ist (Case-Insensitive).
    /// </summary>
    public static bool IsVideo(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return Array.Exists(Videos, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }
}
