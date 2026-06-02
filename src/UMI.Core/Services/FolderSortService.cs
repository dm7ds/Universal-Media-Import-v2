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

using System.Globalization;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Utilities;
using UMI.Data.Models;

namespace UMI.Core.Services;

/// <summary>
/// Sort modes supported by <see cref="IFolderSortService"/>.
/// </summary>
public enum FolderSortMode
{
    /// <summary>Sort to <c>{workbench}/yyyy-MM-dd/{Camera}/{Type}/</c> (default UMI layout).</summary>
    Full,
    /// <summary>Sort to <c>{workbench}/yyyy-MM-dd/{relative substructure}/</c> (preserves existing tree under date folder).</summary>
    Date,
}

/// <summary>
/// Request DTO for <see cref="IFolderSortService.SortAsync"/>.
/// </summary>
public sealed record FolderSortRequest(
    string Workbench,
    FolderSortMode Mode,
    UmiConfig Config,
    bool DetectBursts = false,
    bool DryRun = false);

/// <summary>
/// Phase markers used in <see cref="FolderSortProgress.Phase"/>. SSOT for the
/// implicit protocol between <see cref="FolderSortService"/> (producer) and
/// any consumer that wants to render phase-specific UI text. Don't duplicate
/// these literals — match against these constants.
/// </summary>
public static class FolderSortPhase
{
    public const string Scanning = "Scanning";
    public const string Sorting  = "Sorting";
}

/// <summary>
/// Progress payload emitted during a sort run.
/// </summary>
public sealed record FolderSortProgress(
    int Current,
    int Total,
    string CurrentFile,
    string Phase);

/// <summary>
/// Result DTO returned by <see cref="IFolderSortService.SortAsync"/>.
/// </summary>
public sealed record FolderSortResult(
    int Moved,
    int Skipped,
    int Errors,
    int DateFolders,
    int DetectedSequences,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Sortiert Mediendateien im Workbench-Ordner nach EXIF-Datum.
/// Optional kann eine retro-aktive Burst-Detection laufen, die zusammenhängende
/// Foto-Sequenzen in <c>{Mode}_HHmmss</c>-Unterordner gruppiert.
/// SSOT für Sort-Logik — wird sowohl von CLI (<c>process --sort</c>) als auch
/// von der GUI (Process-Tab → Organisieren) verwendet.
/// </summary>
public interface IFolderSortService
{
    Task<FolderSortResult> SortAsync(
        FolderSortRequest request,
        IProgress<FolderSortProgress>? progress = null,
        CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class FolderSortService : IFolderSortService
{
    private readonly MetadataReader _metadataReader;
    private readonly BurstMatchingEngine _burstMatching;
    private readonly SequenceGroupingService _sequenceGrouping;
    private readonly BurstProfileLoader? _burstProfileLoader;
    private readonly ILogger<FolderSortService>? _logger;

    public FolderSortService(
        MetadataReader metadataReader,
        BurstMatchingEngine burstMatching,
        SequenceGroupingService sequenceGrouping,
        BurstProfileLoader? burstProfileLoader = null,
        ILogger<FolderSortService>? logger = null)
    {
        _metadataReader     = metadataReader;
        _burstMatching      = burstMatching;
        _sequenceGrouping   = sequenceGrouping;
        _burstProfileLoader = burstProfileLoader;
        _logger             = logger;
    }

    public Task<FolderSortResult> SortAsync(
        FolderSortRequest request,
        IProgress<FolderSortProgress>? progress = null,
        CancellationToken ct = default)
    {
        // The method is currently CPU-bound + sync I/O end-to-end (filesystem
        // walk, file moves). Wrapping in Task.Run keeps the GUI responsive
        // when the workbench has thousands of files. If we ever switch
        // MetadataReader to a true async path, drop the Run() and propagate
        // async/await through the body.
        return Task.Run(() => SortCore(request, progress, ct), ct);
    }

    private FolderSortResult SortCore(
        FolderSortRequest request,
        IProgress<FolderSortProgress>? progress,
        CancellationToken ct)
    {
        var workbench = Path.GetFullPath(request.Workbench);
        var config    = request.Config;

        if (!Directory.Exists(workbench))
        {
            _logger?.LogWarning("Workbench nicht gefunden: {Path}", workbench);
            return new FolderSortResult(0, 0, 0, 0, 0, new[] { $"Workbench nicht gefunden: {workbench}" });
        }

        var knownExtensions = config.Cameras.Values
            .SelectMany(c => c.FileTypes.Video.Concat(c.FileTypes.Photo))
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        if (knownExtensions.Count == 0)
        {
            knownExtensions = FileExtensions.Videos
                .Concat(FileExtensions.Photos)
                .Select(e => e.ToLowerInvariant())
                .ToHashSet();
        }

        var allFiles = Directory.EnumerateFiles(workbench, "*.*", SearchOption.AllDirectories)
            .Where(f => knownExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(Path.GetFullPath)
            .ToList();

        var warnings = new List<string>();
        if (allFiles.Count == 0)
        {
            return new FolderSortResult(0, 0, 0, 0, 0, warnings);
        }

        var photoExtensions = config.Cameras.Values
            .SelectMany(c => c.FileTypes.Photo)
            .Select(e => e.ToLowerInvariant())
            .Concat(FileExtensions.Photos.Select(e => e.ToLowerInvariant()))
            .ToHashSet();

        var plans = new List<SortPlan>(allFiles.Count);

        progress?.Report(new FolderSortProgress(0, allFiles.Count, string.Empty, FolderSortPhase.Scanning));

        for (var i = 0; i < allFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = allFiles[i];
            var fileName = Path.GetFileName(filePath);
            var fileDir  = Path.GetDirectoryName(filePath)!;
            var ext      = Path.GetExtension(filePath).ToLowerInvariant();
            var isPhoto  = photoExtensions.Contains(ext);

            DateTime? captureDate = null;
            string? shootingMode  = null;
            string cameraId       = ExtractCameraIdFromPath(filePath, workbench, config);

            if (isPhoto)
            {
                try
                {
                    var meta = _metadataReader.ReadPhotoMetadata(filePath);
                    captureDate = meta.CreateDate;

                    if (request.DetectBursts)
                    {
                        var burstCfg = config.Cameras.TryGetValue(cameraId, out var camCfg)
                            ? LoadBurstConfig(camCfg)
                            : BuildFallbackBurstConfig();
                        shootingMode = _burstMatching.MatchBurstProfile(meta, burstCfg);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"{fileName}: metadata read failed ({ex.GetType().Name})");
                }
            }

            captureDate ??= File.GetLastWriteTime(filePath);

            // QuickTime CreateDate is UTC — match the rest of the codebase and
            // convert to local before formatting (see CLAUDE.md note #15).
            var localDate = captureDate.Value.Kind == DateTimeKind.Utc
                ? captureDate.Value.ToLocalTime()
                : captureDate.Value;

            plans.Add(new SortPlan(
                FilePath:       filePath,
                FileName:       fileName,
                SourceDir:      fileDir,
                CaptureDate:    localDate,
                CameraId:       cameraId,
                IsPhoto:        isPhoto,
                ShootingMode:   shootingMode));

            progress?.Report(new FolderSortProgress(i + 1, allFiles.Count, fileName, FolderSortPhase.Scanning));
        }

        var sequenceFolderByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var detectedSequences    = 0;

        if (request.DetectBursts)
        {
            // Group photos by camera + date (using the LOCAL capture date so
            // cross-midnight UTC timestamps land in the correct calendar day),
            // then run the existing SequenceGroupingService against an
            // in-memory ImportedFile list. We use the array index as a stable
            // surrogate for the DB Id so the FileIds in the result map back to
            // our plan entries without touching ImportDatabase.
            var photoPlans = plans.Where(p => p.IsPhoto).ToList();

            var byCameraDate = photoPlans
                .GroupBy(p => (p.CameraId, Date: p.CaptureDate.ToString(DateFormatConstants.FolderFormat, CultureInfo.InvariantCulture)));

            foreach (var grp in byCameraDate)
            {
                ct.ThrowIfCancellationRequested();

                var burstCfg = config.Cameras.TryGetValue(grp.Key.CameraId, out var camCfg)
                    ? LoadBurstConfig(camCfg)
                    : BuildFallbackBurstConfig();

                if (burstCfg is null || !burstCfg.Enabled || burstCfg.LoadedProfiles is null || burstCfg.LoadedProfiles.Count == 0)
                    continue;

                var ordered = grp
                    .OrderBy(p => p.CaptureDate)
                    .ThenBy(p => p.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (ordered.Count < burstCfg.FallbackMinCount) continue;

                var importedFiles = new List<ImportedFile>(ordered.Count);
                for (var idx = 0; idx < ordered.Count; idx++)
                {
                    var p = ordered[idx];
                    importedFiles.Add(new ImportedFile
                    {
                        Id           = idx,
                        SourcePath   = p.FilePath,
                        Filename     = p.FileName,
                        CameraId     = p.CameraId,
                        CaptureDate  = p.CaptureDate.ToString(DateFormatConstants.FolderFormat, CultureInfo.InvariantCulture),
                        CaptureTime  = p.CaptureDate.ToString("o"),
                        ShootingMode = p.ShootingMode,
                        IsVideo      = 0,
                        MediaType    = "photo",
                    });
                }

                var groups = _sequenceGrouping.GroupPhotosByTimeGaps(importedFiles, burstCfg);

                foreach (var group in groups.Where(g => g.IsSequence))
                {
                    var folder = $"{group.Mode}_{group.FirstPhotoTime:HHmmss}";
                    detectedSequences++;
                    foreach (var fid in group.FileIds)
                    {
                        if (fid < 0 || fid >= importedFiles.Count) continue;
                        sequenceFolderByPath[importedFiles[(int)fid].SourcePath] = folder;
                    }
                }
            }
        }

        var movedCount   = 0;
        var skippedCount = 0;
        var errorCount   = 0;
        var dateFolders  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceDirs   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < plans.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var plan      = plans[i];
            var dateStr   = plan.CaptureDate.ToString(DateFormatConstants.FolderFormat, CultureInfo.InvariantCulture);

            string targetDir;
            if (request.Mode == FolderSortMode.Full)
            {
                var typeFolder = DetermineTypeFolder(plan.FileName, config);
                var procFolder = config.Cameras.TryGetValue(plan.CameraId, out var cam)
                    ? cam.FolderName ?? plan.CameraId
                    : plan.CameraId;

                targetDir = Path.Combine(workbench, dateStr, procFolder, typeFolder);
            }
            else
            {
                var relativeDirToWorkbench = Path.GetRelativePath(workbench, plan.SourceDir);
                targetDir = relativeDirToWorkbench == "."
                    ? Path.Combine(workbench, dateStr)
                    : Path.Combine(workbench, dateStr, relativeDirToWorkbench);
            }

            if (sequenceFolderByPath.TryGetValue(plan.FilePath, out var seqFolder))
            {
                targetDir = Path.Combine(targetDir, seqFolder);
            }

            var targetPath = Path.Combine(targetDir, plan.FileName);

            if (string.Equals(Path.GetFullPath(plan.FilePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                skippedCount++;
                progress?.Report(new FolderSortProgress(i + 1, plans.Count, plan.FileName, FolderSortPhase.Sorting));
                continue;
            }

            if (File.Exists(targetPath))
            {
                skippedCount++;
                progress?.Report(new FolderSortProgress(i + 1, plans.Count, plan.FileName, FolderSortPhase.Sorting));
                continue;
            }

            dateFolders.Add(Path.Combine(workbench, dateStr));
            sourceDirs.Add(plan.SourceDir);

            if (!request.DryRun)
            {
                try
                {
                    Directory.CreateDirectory(targetDir);
                    File.Move(plan.FilePath, targetPath);
                    movedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    warnings.Add($"{plan.FileName}: {ex.Message}");
                    _logger?.LogWarning(ex, "Move failed for {File}", plan.FilePath);
                }
            }
            else
            {
                movedCount++;
            }

            progress?.Report(new FolderSortProgress(i + 1, plans.Count, plan.FileName, FolderSortPhase.Sorting));
        }

        if (!request.DryRun)
        {
            DirectoryCleanupHelper.CleanEmptyDirectories(workbench, sourceDirs);
        }

        return new FolderSortResult(
            Moved:             movedCount,
            Skipped:           skippedCount,
            Errors:            errorCount,
            DateFolders:       dateFolders.Count,
            DetectedSequences: detectedSequences,
            Warnings:          warnings);
    }

    private BurstDetectionConfig? LoadBurstConfig(CameraConfig config)
    {
        if (config.BurstDetectionConfig is null)
            return null;

        var burst = config.BurstDetectionConfig;

        if (_burstProfileLoader is not null
            && burst.LoadedProfiles is { Count: 0 })
        {
            // Defensive: if ActiveProfiles is empty but profiles exist on disk, use all available.
            // This handles cameras saved before the auto-populate fix (TASK-215).
            var profilesToLoad = burst.ActiveProfiles.Count > 0
                ? burst.ActiveProfiles
                : _burstProfileLoader.ListAvailableProfiles(); // Fallback
            if (profilesToLoad.Count > 0)
            {
                burst.LoadedProfiles = _burstProfileLoader.LoadProfiles(profilesToLoad);
                _logger?.LogDebug(
                    "FolderSort: Burst-Profile geladen ({Count}) — ActiveProfiles war {Source}",
                    burst.LoadedProfiles.Count,
                    burst.ActiveProfiles.Count > 0 ? "konfiguriert" : "leer → Fallback auf alle verfügbaren");
            }
        }

        return burst;
    }

    /// <summary>
    /// Constructs a default <see cref="BurstDetectionConfig"/> using all available profiles.
    /// Used for files whose path segment does not map to any registered camera (e.g. <c>_unsorted</c>).
    /// Best-effort: returns null when no profiles are available so callers can skip gracefully.
    /// </summary>
    private BurstDetectionConfig? BuildFallbackBurstConfig()
    {
        if (_burstProfileLoader is null) return null;

        var available = _burstProfileLoader.ListAvailableProfiles();
        if (available.Count == 0) return null;

        var burst = new BurstDetectionConfig
        {
            Enabled        = true,
            ActiveProfiles = available,
        };
        burst.LoadedProfiles = _burstProfileLoader.LoadProfiles(available);

        _logger?.LogDebug(
            "FolderSort: Fallback-BurstConfig für _unsorted erstellt mit {Count} Profilen: {Names}",
            burst.LoadedProfiles.Count,
            string.Join(", ", burst.LoadedProfiles.Select(p => p.Name)));

        return burst;
    }

    /// <summary>
    /// Extracts the camera-id from a file's directory tree by matching path
    /// segments against <c>config.Cameras</c>. Falls back to <c>_unsorted</c>.
    /// </summary>
    internal static string ExtractCameraIdFromPath(string filePath, string workbench, UmiConfig config)
    {
        var relativePath = Path.GetRelativePath(workbench, filePath);
        var segments     = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        var cameraKeys = config.Cameras.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folderToId = config.Cameras
            .GroupBy(c => c.Value.FolderName ?? c.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

        foreach (var segment in segments)
        {
            if (cameraKeys.Contains(segment))
                return config.Cameras.Keys.First(k => k.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (folderToId.TryGetValue(segment, out var matchedId))
                return matchedId;
        }

        return "_unsorted";
    }

    /// <summary>
    /// Determines whether a file goes under the <c>Video</c> or <c>Photo</c>
    /// type-folder, using the camera FileTypes config first and a hard-coded
    /// fallback list of common video extensions when the config is silent.
    /// </summary>
    internal static string DetermineTypeFolder(string fileName, UmiConfig config)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        var isVideo = config.Cameras.Values
            .Any(c => c.FileTypes.Video.Any(v => v.Equals(ext, StringComparison.OrdinalIgnoreCase)));

        if (isVideo)
            return FolderNameConstants.Video;

        var isPhoto = config.Cameras.Values
            .Any(c => c.FileTypes.Photo.Any(p => p.Equals(ext, StringComparison.OrdinalIgnoreCase)));

        if (isPhoto)
            return FolderNameConstants.Photo;

        return ext is ".mp4" or ".mov" or ".avi" or ".mkv"
            ? FolderNameConstants.Video
            : FolderNameConstants.Photo;
    }

    /// <summary>
    /// Per-file plan we build during the scan phase. Captures everything we
    /// need for the move phase + optional burst detection so the second pass
    /// doesn't have to re-read EXIF data.
    /// </summary>
    private sealed record SortPlan(
        string FilePath,
        string FileName,
        string SourceDir,
        DateTime CaptureDate,
        string CameraId,
        bool IsPhoto,
        string? ShootingMode);
}
