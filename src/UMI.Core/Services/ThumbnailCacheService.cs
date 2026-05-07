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
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Utilities;
using SysDirectory = System.IO.Directory;

namespace UMI.Core.Services;

/// <summary>
/// Cache-Service für RAW-Datei-Thumbnails und Previews.
/// Cache-Pfad: {tagRoot}/.metadata/thumbnails/{relativePath}/{basename}.thumb.jpg
/// Invalidierung via LastWriteTimeUtc-Vergleich (Cache muss neuer sein als Original).
/// </summary>
public sealed class ThumbnailCacheService : IThumbnailCacheService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr3", ".cr2", ".arw", ".nef", ".dng", ".orf", ".rw2", ".raf"
    };

    private readonly IExifToolWrapper _exifTool;
    private readonly GlobalPaths _globalPaths;
    private readonly ILogger<ThumbnailCacheService>? _logger;

    public ThumbnailCacheService(
        IExifToolWrapper exifTool,
        GlobalPaths globalPaths,
        ILogger<ThumbnailCacheService>? logger = null)
    {
        _exifTool = exifTool;
        _globalPaths = globalPaths;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetThumbnailAsync(string filePath, CancellationToken ct = default)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
            return null;

        var cachePath = GetCachePath(filePath, FolderNameConstants.ThumbnailSuffix);

        // Cache-Hit prüfen — validate JPEG magic (old cache may contain HEIF from CR3 HDR PQ)
        var cached = TryReadCache(cachePath, filePath);
        if (cached is { Length: >= 2 } && cached[0] == 0xFF && cached[1] == 0xD8)
            return cached;

        byte[]? bytes = null;

        // CR3 Fast Path — direkte ISOBMFF-Extraktion (<5ms, kein ExifTool!)
        if (Path.GetExtension(filePath).Equals(".cr3", StringComparison.OrdinalIgnoreCase))
        {
            bytes = Cr3JpegExtractor.ExtractThumbnail(filePath);
            if (bytes != null)
            {
                await WriteCacheAsync(cachePath, bytes, ct).ConfigureAwait(false);
                return bytes;
            }

            // CR3 THMB fehlgeschlagen — MetadataExtractor hat keine ExifThumbnailDirectory für CR3,
            // nur ExifTool als letzter Fallback
            bytes = await ThumbnailHelper.ExtractThumbnailViaExifToolAsync(_exifTool, filePath, ct)
                .ConfigureAwait(false);
            if (bytes != null)
                await WriteCacheAsync(cachePath, bytes, ct).ConfigureAwait(false);
            return bytes;
        }

        // Nicht-CR3: MetadataExtractor (Strategie 1+2)
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            bytes = ThumbnailHelper.ExtractThumbnailBytes(directories, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("MetadataExtractor Fehler bei {File}: {Error}", filePath, ex.Message);
        }

        // Nicht-CR3: ExifTool-Fallback (Strategie 3)
        if (bytes == null)
        {
            bytes = await ThumbnailHelper.ExtractThumbnailViaExifToolAsync(_exifTool, filePath, ct)
                .ConfigureAwait(false);
        }

        if (bytes != null)
            await WriteCacheAsync(cachePath, bytes, ct).ConfigureAwait(false);

        return bytes;
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetPreviewAsync(string filePath, int maxHeight = 1080, CancellationToken ct = default)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
            return null;

        var cachePath = GetCachePath(filePath, FolderNameConstants.PreviewSuffix);

        // Cache-Hit prüfen — validate JPEG magic (old cache may contain HEIF from CR3 HDR PQ)
        var cached = TryReadCache(cachePath, filePath);
        if (cached is { Length: >= 2 } && cached[0] == 0xFF && cached[1] == 0xD8)
            return cached;

        byte[]? bytes = null;

        // CR3 Fast Path — direkte ISOBMFF-Extraktion (<5ms, kein ExifTool!)
        if (Path.GetExtension(filePath).Equals(".cr3", StringComparison.OrdinalIgnoreCase))
        {
            bytes = Cr3JpegExtractor.ExtractPreview(filePath);
            if (bytes is { Length: >= 2 } && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                await WriteCacheAsync(cachePath, bytes, ct).ConfigureAwait(false);
                return bytes;
            }

            _logger?.LogDebug("CR3 JPEG-Preview-Extraktion fehlgeschlagen für {File}", filePath);

            // HEIF-Fallback (HDR PQ CR3) — extrahiere HEIF aus PRVW, dekodiere via LibHeifSharp
            // HeifJpegConverter nutzt System.Drawing.Common (Windows-only, wie der gesamte UMI-Stack)
#pragma warning disable CA1416
            var heifData = Cr3JpegExtractor.ExtractPreviewHeif(filePath);
            if (heifData is { Length: > 0 })
            {
                var jpegBytes = HeifJpegConverter.ConvertToJpeg(heifData);
                if (jpegBytes is { Length: >= 2 } && jpegBytes[0] == 0xFF && jpegBytes[1] == 0xD8)
                {
                    await WriteCacheAsync(cachePath, jpegBytes, ct).ConfigureAwait(false);
                    return jpegBytes;
                }
            }

            _logger?.LogDebug("CR3 HEIF-Preview-Extraktion fehlgeschlagen für {File}", filePath);
#pragma warning restore CA1416

            return null;
        }

        // Nicht-CR3: ExifTool -b -PreviewImage
        if (_exifTool.IsAvailable)
        {
            try
            {
                bytes = await _exifTool.ReadBinaryTagAsync(filePath, "PreviewImage", ct).ConfigureAwait(false);

                // ExifTool -b -JpgFromRaw (Nikon NEF Fallback)
                if (bytes == null || bytes.Length == 0)
                    bytes = await _exifTool.ReadBinaryTagAsync(filePath, "JpgFromRaw", ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("ExifTool Preview-Fehler bei {File}: {Error}", filePath, ex.Message);
            }
        }

        // Only cache valid JPEG — HEIF/ISOBMFF data from CR3 HDR PQ hangs WPF BitmapImage
        if (bytes != null && bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            await WriteCacheAsync(cachePath, bytes, ct).ConfigureAwait(false);

        return bytes is { Length: >= 2 } && bytes[0] == 0xFF && bytes[1] == 0xD8 ? bytes : null;
    }

    /// <inheritdoc/>
    public async Task WarmCacheAsync(string folderPath, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var files = SysDirectory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

        var count = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            await GetThumbnailAsync(file, ct).ConfigureAwait(false);
            progress?.Report(++count);
        }
    }

    /// <inheritdoc/>
    public async Task SaveThumbnailAsync(string filePath, byte[] bytes, CancellationToken ct = default)
    {
        var cachePath = GetCachePath(filePath, FolderNameConstants.ThumbnailSuffix);
        await WriteCacheAsync(cachePath, bytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SavePreviewAsync(string filePath, byte[] bytes, CancellationToken ct = default)
    {
        var cachePath = GetCachePath(filePath, FolderNameConstants.PreviewSuffix);
        await WriteCacheAsync(cachePath, bytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool HasCachedPreview(string filePath)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
            return false;
        var cachePath = GetCachePath(filePath, FolderNameConstants.PreviewSuffix);
        if (!File.Exists(cachePath))
            return false;

        // Validate JPEG magic — old HEIF cache entries (from CR3 HDR PQ tests) are invalid
        // and would cause WPF BitmapImage to hang or throw
        try
        {
            using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> magic = stackalloc byte[2];
            return fs.Read(magic) == 2 && magic[0] == 0xFF && magic[1] == 0xD8;
        }
        catch (Exception ex) { _logger?.LogDebug(ex, "JPEG-Magic-Check fehlgeschlagen: {Path}", cachePath); return false; }
    }

    /// <inheritdoc/>
    public void InvalidateCache(string folderPath)
    {
        var cacheRoot = Path.Combine(
            _globalPaths.Workbench,
            FolderNameConstants.UmiDir,
            FolderNameConstants.UmiThumbnails);

        if (SysDirectory.Exists(cacheRoot))
        {
            try
            {
                SysDirectory.Delete(cacheRoot, recursive: true);
                _logger?.LogDebug("Thumbnail-Cache gelöscht: {Path}", cacheRoot);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Thumbnail-Cache konnte nicht gelöscht werden: {Path} — {Error}", cacheRoot, ex.Message);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Berechnet den Cache-Pfad für eine Mediendatei.
    /// Schema: {workbench}/.umi/thumbnails/{relativePath}/{basename}{suffix}
    /// </summary>
    private string GetCachePath(string filePath, string suffix)
    {
        return PathHelper.GetUmiPath(_globalPaths.Workbench, filePath, FolderNameConstants.UmiSubDir.Thumbnails, suffix);
    }

    /// <summary>
    /// Liest Cache-Bytes wenn vorhanden UND neuer (oder gleich alt) als Original.
    /// </summary>
    private static byte[]? TryReadCache(string cachePath, string originalPath)
    {
        try
        {
            if (!File.Exists(cachePath))
                return null;

            var cacheTime = File.GetLastWriteTimeUtc(cachePath);
            var originalTime = File.GetLastWriteTimeUtc(originalPath);

            if (cacheTime < originalTime)
                return null;

            return File.ReadAllBytes(cachePath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Schreibt Bytes in den Cache. Erstellt das Verzeichnis falls nötig.
    /// </summary>
    private async Task WriteCacheAsync(string cachePath, byte[] bytes, CancellationToken ct)
    {
        try
        {
            var cacheDir = Path.GetDirectoryName(cachePath)!;
            SysDirectory.CreateDirectory(cacheDir);
            await File.WriteAllBytesAsync(cachePath, bytes, ct).ConfigureAwait(false);
            _logger?.LogDebug("Thumbnail gecacht: {Path} ({Bytes} Bytes)", cachePath, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Thumbnail-Cache-Schreiben fehlgeschlagen: {Path} — {Error}", cachePath, ex.Message);
        }
    }
}
