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
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Utilities;
using UMI.Data;

namespace UMI.Core.Services;

/// <summary>
/// Service für Post-Import Verification und Standalone Workbench-Checks.
/// Zwei Modi: VerifyImportAsync (mit DB) und VerifyWorkbenchAsync (ohne DB).
/// </summary>
public class VerificationService
{
    private readonly MetadataReader _metadataReader;
    private readonly LayoutConfig _layoutConfig;
    private readonly ILogger<VerificationService>? _logger;

    public VerificationService(
        MetadataReader metadataReader,
        LayoutConfig layoutConfig,
        ILogger<VerificationService>? logger = null)
    {
        _metadataReader = metadataReader;
        _layoutConfig = layoutConfig;
        _logger = logger;
    }

    /// <summary>
    /// Modus 1: Post-Import Verify (nach Copy, DB als Referenz).
    /// Prüft ob alle kopierten Dateien korrekt im Workbench gelandet sind.
    /// </summary>
    public async Task<VerifyResult> VerifyImportAsync(
        ImportDatabase db,
        string? cameraId = null,
        CancellationToken ct = default)
    {
        var result = new VerifyResult();

        var files = string.IsNullOrEmpty(cameraId)
            ? await db.GetCopiedFilesForVerify()
            : await db.GetCopiedFilesForVerify(cameraId);

        result.TotalFiles = files.Count;
        _logger?.LogDebug("Verifiziere {Count} kopierte Dateien...", files.Count);

        foreach (var (destPath, expectedSize) in files)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(destPath))
            {
                result.Missing++;
                result.Issues.Add(new VerifyIssue
                {
                    FilePath = destPath,
                    IssueType = "Missing",
                    Severity = "Error",
                    Message = $"Datei fehlt: {destPath}"
                });
                continue;
            }

            var fileInfo = new FileInfo(destPath);

            if (fileInfo.Length != expectedSize)
            {
                result.SizeMismatch++;
                result.Issues.Add(new VerifyIssue
                {
                    FilePath = destPath,
                    IssueType = "SizeMismatch",
                    Severity = "Error",
                    Message = $"Größe abweichend: erwartet {Utilities.FormatHelper.FormatBytes(expectedSize)}, " +
                              $"ist {Utilities.FormatHelper.FormatBytes(fileInfo.Length)}"
                });
                continue;
            }

            result.Verified++;
        }

        _logger?.LogDebug(
            "Verify abgeschlossen: {Verified} OK, {Missing} fehlend, {SizeMismatch} Größenabweichungen",
            result.Verified, result.Missing, result.SizeMismatch);

        return result;
    }

    /// <summary>
    /// Modus 2: Standalone Workbench-Check (ohne DB, Workbench als Basis).
    /// Scannt Workbench und prüft Datei-Integrität und Backups.
    /// </summary>
    public async Task<VerifyResult> VerifyWorkbenchAsync(
        string workbenchPath,
        UmiConfig config,
        string? cameraId = null,
        FFprobeWrapper? ffprobe = null,
        CancellationToken ct = default)
    {
        var result = new VerifyResult();

        if (!Directory.Exists(workbenchPath))
        {
            _logger?.LogError("Workbench nicht gefunden: {Path}", workbenchPath);
            return result;
        }

        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var photoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var camera in config.Cameras.Values.Where(c => c.Enabled))
        {
            foreach (var ext in camera.FileTypes.Video)
                videoExtensions.Add(ext);
            foreach (var ext in camera.FileTypes.Photo)
                photoExtensions.Add(ext);
        }

        var allExtensions = videoExtensions.Concat(photoExtensions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = new List<FileInfo>();

        foreach (var file in new DirectoryInfo(workbenchPath).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (allExtensions.Contains(file.Extension))
            {

                if (!string.IsNullOrEmpty(cameraId) && cameraId != "ALL")
                {

                    var fileCameraId = ExtractCameraIdFromPath(file.FullName, config);

                    if (fileCameraId == null || !fileCameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                files.Add(file);
            }
        }

        result.TotalFiles = files.Count;
        _logger?.LogDebug("Verifiziere {Count} Dateien im Workbench...", files.Count);

        var issues = new ConcurrentBag<VerifyIssue>();
        int verified = 0, corrupt = 0, noBackup = 0;

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (file, token) =>
        {
            var isVideo = videoExtensions.Contains(file.Extension);

            if (config.Verification.Checks.FileSize && file.Length == 0)
            {
                Interlocked.Increment(ref corrupt);
                issues.Add(new VerifyIssue
                {
                    FilePath = file.FullName,
                    IssueType = "Corrupt",
                    Severity = "Error",
                    Message = $"Datei ist leer (0 Bytes)"
                });
                return;
            }

            if (config.Verification.Checks.MetadataBackup)
            {

                var fileCamera = ResolveCameraFromPath(file.FullName, config);
                if (fileCamera != null && fileCamera.Features.MetadataBackup)
                {
                    var metaPath = PathHelper.GetUmiPath(workbenchPath, file.FullName, FolderNameConstants.UmiSubDir.Metadata, config.MetadataBackup.Extension);
                    if (!File.Exists(metaPath))
                    {
                        Interlocked.Increment(ref noBackup);
                        issues.Add(new VerifyIssue
                        {
                            FilePath = file.FullName,
                            IssueType = "NoBackup",
                            Severity = "Warning",
                            Message = "Kein Metadata-Backup gefunden"
                        });
                    }
                }
            }

            if (isVideo && config.Verification.Checks.VideoIntegrity && ffprobe != null)
            {
                try
                {
                    var videoInfo = await ffprobe.GetVideoInfoAsync(file.FullName, token);
                    if (videoInfo != null && !videoInfo.IsValid)
                    {
                        Interlocked.Increment(ref corrupt);
                        issues.Add(new VerifyIssue
                        {
                            FilePath = file.FullName,
                            IssueType = "Corrupt",
                            Severity = "Error",
                            Message = "Video ist korrupt (FFprobe-Check fehlgeschlagen)"
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref corrupt);
                    issues.Add(new VerifyIssue
                    {
                        FilePath = file.FullName,
                        IssueType = "Corrupt",
                        Severity = "Error",
                        Message = $"Video-Check fehlgeschlagen: {ex.Message}"
                    });
                    return;
                }
            }

            if (!isVideo)
            {
                try
                {
                    _ = _metadataReader.ReadPhotoMetadata(file.FullName);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref corrupt);
                    issues.Add(new VerifyIssue
                    {
                        FilePath = file.FullName,
                        IssueType = "Corrupt",
                        Severity = "Error",
                        Message = $"Foto ist korrupt (Metadata nicht lesbar): {ex.Message}"
                    });
                    return;
                }
            }

            Interlocked.Increment(ref verified);
        });

        result.Verified = verified;
        result.Corrupt = corrupt;
        result.NoBackup = noBackup;
        result.Issues = issues.ToList();

        _logger?.LogDebug(
            "Verify abgeschlossen: {Verified} OK, {Corrupt} korrupt, {NoBackup} ohne Backup",
            result.Verified, result.Corrupt, result.NoBackup);

        return result;
    }

    /// <summary>
    /// Ermittelt die Kamera-Config aus dem Dateipfad (layout-aware).
    /// </summary>
    private CameraConfig? ResolveCameraFromPath(string filePath, UmiConfig config)
    {
        var cameraId = ExtractCameraIdFromPath(filePath, config);

        if (cameraId != null && config.Cameras.TryGetValue(cameraId, out var cameraConfig))
            return cameraConfig;

        return null;
    }

    /// <summary>
    /// Extrahiert CameraId aus Dateipfad (layout-aware).
    /// Unterstützt camera_first und type_first Layouts.
    /// </summary>
    private string? ExtractCameraIdFromPath(string filePath, UmiConfig config)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory == null)
            return null;

        var tagRoot = PathHelper.FindTagRoot(directory);

        if (tagRoot == null)
            return null;

        var file = new FileInfo(fullPath);
        var immediateParent = file.Directory?.Name;
        var parentParent = file.Directory?.Parent?.Name;

        var sortOrder = _layoutConfig.SortOrder?.ToLower() ?? SortOrder.CameraFirst;

        var workflowFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FolderNameConstants.Gyroflow, FolderNameConstants.Stabilized, FolderNameConstants.TimeLapse,
            FolderNameConstants.Export, FolderNameConstants.Metadata, FolderNameConstants.Gps
        };

        if (sortOrder == SortOrder.TypeFirst)
        {

            if (parentParent != null && workflowFolders.Contains(parentParent))
            {

                return ResolveCameraIdFromFolder(immediateParent ?? "", config);
            }
            else
            {

                return ResolveCameraIdFromFolder(immediateParent ?? "", config);
            }
        }
        else
        {

            if (immediateParent != null && workflowFolders.Contains(immediateParent))
            {

                return ResolveCameraIdFromFolder(parentParent ?? "", config);
            }
            else
            {

                return ResolveCameraIdFromFolder(parentParent ?? "", config);
            }
        }
    }

    /// <summary>
    /// Löst Ordnername zu CameraId auf via Config-Matching.
    /// </summary>
    private string? ResolveCameraIdFromFolder(string folderName, UmiConfig config)
    {
        foreach (var (cameraId, cameraConfig) in config.Cameras)
        {

            if (cameraId.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                return cameraId;

            if (!string.IsNullOrEmpty(cameraConfig.FolderName) &&
                cameraConfig.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                return cameraId;
        }

        return null;
    }
}
