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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Result of a metadata restore operation.
/// </summary>
public enum MetadataRestoreResult
{
    Restored,
    NoBackup,
    InvalidFormat,
    NoRestoreFields,
    WriteFailed,
    Error
}

/// <summary>
/// Service für Metadata Backup und Restore.
/// </summary>
public class MetadataService
{
    /// <summary>
    /// Cache for Tier-4 recursive metadata search results.
    /// Key: baseName + extension (e.g. "DJI_20260212120000_0001.meta.json"), Value: found path or null.
    /// Tier 1-3 always find new backups, so Tier-4 cache only covers legacy lookups.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string?> s_tier4Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly IExifToolWrapper _exifTool;
    private readonly MetadataBackupConfig _config;
    private readonly GlobalPaths _globalPaths;
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<MetadataService>? _logger;

    public MetadataService(
        IExifToolWrapper exifTool,
        MetadataBackupConfig config,
        GlobalPaths globalPaths,
        IProcessHistoryService processHistory,
        ILogger<MetadataService>? logger = null)
    {
        _exifTool = exifTool;
        _config = config;
        _globalPaths = globalPaths;
        _processHistory = processHistory;
        _logger = logger;
    }

    /// <summary>
    /// Erstellt Metadata-Backup für eine Datei.
    /// </summary>
    public async Task<bool> CreateBackupAsync(string filePath, string? cameraId = null, CancellationToken ct = default)
    {
        try
        {

            var metadata = await _exifTool.ReadMetadataAsync(filePath, _config.BackupFields, ct);

            if (metadata.Count == 0)
            {
                _logger?.LogWarning("Keine Metadaten gefunden: {File}", Path.GetFileName(filePath));
                return false;
            }

            var backup = new MetadataBackup
            {
                SourceCamera = cameraId,
                OriginalFilename = Path.GetFileName(filePath),
                ImportTimestamp = DateTime.UtcNow,
                Metadata = metadata
            };

            var metaPath = PathHelper.GetUmiPath(_globalPaths.Workbench, filePath, FolderNameConstants.UmiSubDir.Metadata, _config.Extension);

            var metadataDir = Path.GetDirectoryName(metaPath);
            Directory.CreateDirectory(metadataDir!);

            var json = JsonSerializer.Serialize(new[] { backup }, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(metaPath, json, ct);

            _logger?.LogDebug("Metadata-Backup erstellt: {File}", Path.GetFileName(metaPath));

            await _processHistory.WriteEntryAsync(filePath, ProcessSteps.MetadataBackedUp, ct: ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Metadata-Backup: {File}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Stellt Metadaten aus Backup wieder her.
    /// </summary>
    public async Task<MetadataRestoreResult> RestoreMetadataAsync(string filePath, bool force = false, CancellationToken ct = default)
    {
        try
        {

            var metaPath = FindMetadataFile(filePath);

            if (metaPath == null)
            {
                _logger?.LogWarning("Keine Metadata-Backup gefunden: {File}", Path.GetFileName(filePath));
                return MetadataRestoreResult.NoBackup;
            }

            _logger?.LogDebug("Metadata gefunden: {Meta}", Path.GetFileName(metaPath));

            var json = await File.ReadAllTextAsync(metaPath, ct);
            var backups = JsonSerializer.Deserialize<MetadataBackup[]>(json);

            if (backups == null || backups.Length == 0)
            {
                _logger?.LogWarning("Ungültiges Metadata-Format: {File}", metaPath);
                return MetadataRestoreResult.InvalidFormat;
            }

            var backup = backups[0];

            var restoreData = backup.Metadata
                .Where(kvp => _config.RestoreFields.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (restoreData.Count == 0)
            {
                _logger?.LogWarning("Keine Restore-Felder gefunden");
                return MetadataRestoreResult.NoRestoreFields;
            }

            var success = await _exifTool.WriteMetadataAsync(filePath, restoreData, overwriteOriginal: true, ct);

            if (success)
            {
                _logger?.LogDebug("✓ Metadata restored: {File}", Path.GetFileName(filePath));
                return MetadataRestoreResult.Restored;
            }

            return MetadataRestoreResult.WriteFailed;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Metadata-Restore: {File}", filePath);
            return MetadataRestoreResult.Error;
        }
    }

    /// <summary>
    /// Findet Metadata-Datei mit 4-Tier Strategy: Neuer .metadata/-Pfad + Legacy-Fallback.
    /// </summary>
    private string? FindMetadataFile(string filePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(filePath);

        var newMetaPath = PathHelper.GetUmiPath(_globalPaths.Workbench, filePath, FolderNameConstants.UmiSubDir.Metadata, _config.Extension);
        if (newMetaPath != null && File.Exists(newMetaPath))
        {
            _logger?.LogDebug("Metadata gefunden (.umi/metadata/): {Path}", Path.GetFileName(newMetaPath));
            return newMetaPath;
        }

        _logger?.LogDebug("Kein .umi/metadata/-Backup gefunden, suche Legacy-Pfade für: {File}", Path.GetFileName(filePath));

        var directory = Path.GetDirectoryName(filePath);
        if (directory == null)
            return null;

        var sameFolderMeta = Path.Combine(directory, $"{baseName}{_config.Extension}");
        if (File.Exists(sameFolderMeta))
        {
            _logger?.LogWarning("Legacy-Metadata gefunden (same folder): {File} - Neue Backups nutzen .metadata/",
                Path.GetFileName(sameFolderMeta));
            return sameFolderMeta;
        }

        var parent = Directory.GetParent(directory);
        if (parent != null)
        {
            var metadataDir = Path.Combine(parent.FullName, FolderNameConstants.Metadata);
            var parentMeta = Path.Combine(metadataDir, $"{baseName}{_config.Extension}");
            if (File.Exists(parentMeta))
            {
                _logger?.LogWarning("Legacy-Metadata gefunden (Metadata/): {File} - Neue Backups nutzen .metadata/",
                    Path.GetFileName(parentMeta));
                return parentMeta;
            }
        }

        var tier4Key = $"{baseName}{_config.Extension}";
        if (s_tier4Cache.TryGetValue(tier4Key, out var cachedPath))
        {
            if (cachedPath != null)
                _logger?.LogDebug("Tier-4 cache hit: {File}", Path.GetFileName(cachedPath));
            return cachedPath;
        }

        var videoRoot = FindVideoRoot(directory);
        if (videoRoot != null)
        {
            var allMeta = Directory.GetFiles(videoRoot, $"{baseName}{_config.Extension}",
                SearchOption.AllDirectories);
            if (allMeta.Length > 0)
            {
                _logger?.LogWarning("Legacy-Metadata gefunden (recursive): {File} - Neue Backups nutzen .metadata/",
                    Path.GetFileName(allMeta[0]));
                s_tier4Cache.TryAdd(tier4Key, allMeta[0]);
                return allMeta[0];
            }
        }

        s_tier4Cache.TryAdd(tier4Key, null);

        return null;
    }

    private string? FindVideoRoot(string directory)
    {

        var current = new DirectoryInfo(directory);

        while (current != null)
        {
            if (current.Name.Equals(FolderNameConstants.Video, StringComparison.OrdinalIgnoreCase))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private class MetadataBackup
    {
        public string? SourceCamera { get; set; }
        public string? OriginalFilename { get; set; }
        public DateTime ImportTimestamp { get; set; }
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }
}
