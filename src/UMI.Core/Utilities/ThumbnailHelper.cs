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

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace UMI.Core.Utilities;

/// <summary>
/// Statische Hilfsklasse für EXIF-Thumbnail-Extraktion.
/// Bietet Strategie 1+2 (MetadataExtractor) und Strategie 3 (ExifTool-Fallback).
/// Wird von ThumbnailCacheService genutzt — Logik NICHT in den Service duplizieren!
/// </summary>
public static class ThumbnailHelper
{
    /// <summary>
    /// Extrahiert EXIF-Thumbnail-Bytes via MetadataExtractor-Directories.
    /// Strategie 1: AdjustedThumbnailOffset + TagThumbnailLength → direktes Byte-Lesen aus FileStream,
    ///   JPEG-Magic-Bytes 0xFF 0xD8 werden validiert.
    /// Strategie 2: GetObject(TagThumbnailOffset) als byte[] (RAW-Fallback).
    /// Gibt null zurück wenn kein Thumbnail gefunden oder ein Fehler auftritt.
    /// </summary>
    public static byte[]? ExtractThumbnailBytes(
        IReadOnlyList<MetadataExtractor.Directory> directories,
        string filePath)
    {
        try
        {
            var thumbDir = directories.OfType<ExifThumbnailDirectory>().FirstOrDefault();
            if (thumbDir == null)
                return null;

            // Strategie 1: AdjustedThumbnailOffset + TagThumbnailLength
            var adjustedOffset = thumbDir.AdjustedThumbnailOffset;
            if (adjustedOffset.HasValue
                && thumbDir.TryGetInt32(ExifThumbnailDirectory.TagThumbnailLength, out var length)
                && length > 0)
            {
                using var stream = File.OpenRead(filePath);
                stream.Seek(adjustedOffset.Value, SeekOrigin.Begin);
                var buffer = new byte[length];
                var bytesRead = stream.Read(buffer, 0, length);

                if (bytesRead == length && buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xD8)
                    return buffer;
            }

            // Strategie 2: TagThumbnailOffset als byte[]
            var rawBytes = thumbDir.GetObject(ExifThumbnailDirectory.TagThumbnailOffset) as byte[];
            if (rawBytes != null && rawBytes.Length >= 2 && rawBytes[0] == 0xFF && rawBytes[1] == 0xD8)
                return rawBytes;
        }
        catch
        {
            // Fehler → Fallback auf ExifTool
        }

        return null;
    }

    /// <summary>
    /// Extrahiert Thumbnail via ExifTool als Fallback (z.B. CR3).
    /// Nutzt IExifToolWrapper.ReadBinaryTagAsync mit Tag "ThumbnailImage".
    /// LANGSAM (~50–100ms pro Datei) — nur wenn MetadataExtractor versagt!
    /// </summary>
    public static async Task<byte[]?> ExtractThumbnailViaExifToolAsync(
        IExifToolWrapper exifTool,
        string filePath,
        CancellationToken ct = default)
    {
        if (!exifTool.IsAvailable)
            return null;

        try
        {
            var bytes = await exifTool.ReadBinaryTagAsync(filePath, "ThumbnailImage", ct).ConfigureAwait(false);

            if (bytes != null && bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                return bytes;

            return null;
        }
        catch
        {
            return null;
        }
    }
}
