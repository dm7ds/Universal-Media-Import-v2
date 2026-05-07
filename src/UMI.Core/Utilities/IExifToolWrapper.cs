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
/// Interface für ExifTool-Wrapper - ermöglicht Mocking in Tests.
/// </summary>
public interface IExifToolWrapper
{
    /// <summary>True when a valid ExifTool path is configured and the executable exists.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Liest Metadaten aus einer Datei (JSON-Format).
    /// </summary>
    Task<Dictionary<string, object?>> ReadMetadataAsync(string filePath, string[]? fields = null, CancellationToken ct = default);

    /// <summary>
    /// Schreibt Metadaten in eine Datei.
    /// </summary>
    Task<bool> WriteMetadataAsync(string filePath, Dictionary<string, object?> metadata, bool overwriteOriginal = true, CancellationToken ct = default);

    /// <summary>
    /// Injiziert GPS-Daten aus GPX-Datei in Video.
    /// </summary>
    Task<bool> InjectGpsFromGpxAsync(string videoPath, string gpxPath, bool overwriteOriginal = true, CancellationToken ct = default);

    /// <summary>
    /// Kopiert alle Metadaten von einer Quelldatei in eine Zieldatei via ExifTool -TagsFromFile.
    /// Wird nach Gyroflow/Racerender genutzt, um Metadaten des Originals in die neue Datei zu übertragen.
    /// </summary>
    Task<bool> CopyTagsFromFileAsync(string sourcePath, string destPath, bool overwriteOriginal = true, CancellationToken ct = default);

    /// <summary>
    /// Liest einen binären Tag aus einer Datei (z.B. -b -ThumbnailImage für eingebettete Thumbnails).
    /// Gibt rohe Bytes zurück — NICHT text-basiert, da Binärdaten durch UTF-8-Encoding korrumpiert würden.
    /// Gibt null zurück wenn ExifTool nicht verfügbar, der Tag leer ist oder ein Fehler auftritt.
    /// </summary>
    Task<byte[]?> ReadBinaryTagAsync(string filePath, string tagName, CancellationToken ct = default);
}
