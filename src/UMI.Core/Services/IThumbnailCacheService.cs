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

namespace UMI.Core.Services;

/// <summary>
/// Cache-Service für RAW-Datei-Thumbnails und Previews.
/// Speichert extrahierte JPEG-Bytes im .metadata/thumbnails/ Verzeichnis
/// relativ zum Tag-Root (yyyy-MM-dd Ordner).
/// </summary>
public interface IThumbnailCacheService
{
    /// <summary>
    /// Gibt den eingebetteten EXIF-Thumbnail einer RAW-Datei zurück.
    /// Fallback-Kette: Cache → MetadataExtractor → ExifTool.
    /// Gibt null zurück wenn kein Thumbnail verfügbar oder das Format nicht unterstützt wird.
    /// </summary>
    Task<byte[]?> GetThumbnailAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Gibt ein eingebettetes Preview-Bild einer RAW-Datei zurück (größer als Thumbnail).
    /// Fallback-Kette: Cache → ExifTool PreviewImage → ExifTool JpgFromRaw (Nikon NEF).
    /// Gibt null zurück wenn kein Preview verfügbar.
    /// </summary>
    Task<byte[]?> GetPreviewAsync(string filePath, int maxHeight = 1080, CancellationToken ct = default);

    /// <summary>
    /// Wärmt den Cache für alle unterstützten Dateien in einem Ordner vor.
    /// Progress-Reports enthalten die Anzahl der bisher verarbeiteten Dateien.
    /// </summary>
    Task WarmCacheAsync(string folderPath, IProgress<int>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Speichert extern generierte Thumbnail-Bytes (z.B. lazy-encoded aus WPF BitmapSource)
    /// in den Disk-Cache. Überschreibt vorhandenen Cache.
    /// </summary>
    Task SaveThumbnailAsync(string filePath, byte[] bytes, CancellationToken ct = default);

    /// <summary>
    /// Speichert extern generierte Preview-Bytes in den Disk-Cache.
    /// </summary>
    Task SavePreviewAsync(string filePath, byte[] bytes, CancellationToken ct = default);

    /// <summary>
    /// Prüft ob ein gecachtes Preview für die Datei existiert (nur Disk-Check, kein ExifTool).
    /// </summary>
    bool HasCachedPreview(string filePath);

    /// <summary>
    /// Invalidiert (löscht) den Thumbnail-Cache für den gegebenen Ordnerpfad.
    /// </summary>
    void InvalidateCache(string folderPath);
}
