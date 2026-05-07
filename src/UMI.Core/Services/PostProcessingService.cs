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

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für Post-Processing von bereits importierten Videos.
/// Business-Logik für Video-Suche, Gyroflow-Stabilisierung und GPS-Injection.
/// </summary>
public class PostProcessingService : IPostProcessingService
{
    private readonly UmiConfig _config;
    private readonly LayoutConfig _layoutConfig;
    private readonly IGyroflowService _gyroflowService;
    private readonly GpsService _gpsService;
    private readonly Mp4Parser? _mp4Parser;
    private readonly ILogger<PostProcessingService> _logger;
    private readonly ConfigPathResolver _configPaths;

    public PostProcessingService(
        UmiConfig config,
        LayoutConfig layoutConfig,
        GpsService gpsService,
        IGyroflowService gyroflowService,
        ConfigPathResolver configPaths,
        ILogger<PostProcessingService> logger,
        Mp4Parser? mp4Parser = null)
    {
        _config = config;
        _layoutConfig = layoutConfig;
        _gpsService = gpsService;
        _gyroflowService = gyroflowService;
        _configPaths = configPaths;
        _logger = logger;
        _mp4Parser = mp4Parser;
    }

    public Task<List<FileInfo>> FindVideosAsync(PostProcessingOptions options, CancellationToken ct = default)
    {
        var videos = new List<FileInfo>();

        if (!Directory.Exists(options.Workbench))
            return Task.FromResult(videos);

        var searchPath = options.Workbench;

        if (!string.IsNullOrEmpty(options.Date))
        {
            searchPath = Path.Combine(options.Workbench, options.Date);
            if (!Directory.Exists(searchPath))
            {
                _logger.LogWarning("Datum-Ordner nicht gefunden: {Date}", options.Date);
                return Task.FromResult(videos);
            }
        }

        if (options.Mode.Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            videos = FindVideosManualMode(searchPath, options.Source);
        }
        else if (options.Mode.Equals("automatic", StringComparison.OrdinalIgnoreCase))
        {
            videos = FindVideosAutomaticMode(searchPath, options.Source);
        }
        else
        {
            _logger.LogWarning("Unbekannter Modus: {Mode} (nutze 'manual' oder 'automatic')", options.Mode);
            return Task.FromResult(videos);
        }

        return Task.FromResult(videos.OrderBy(v => v.CreationTime).ToList());
    }

    private List<FileInfo> FindVideosManualMode(string searchPath, string source)
    {
        var videos = new List<FileInfo>();

        var sourceIds = source.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? new List<string>()
            : source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

        var gyroflowFolders = Directory.GetDirectories(searchPath, FolderNameConstants.Gyroflow, SearchOption.AllDirectories);

        if (gyroflowFolders.Length == 0)
        {
            _logger.LogDebug("Keine 'Gyroflow' Ordner gefunden in {SearchPath}", searchPath);
            _logger.LogDebug("Tipp: Erstelle Ordner 'Video/Gyroflow' und verschiebe Videos dort rein");
            return videos;
        }

        foreach (var gyroflowFolder in gyroflowFolders)
        {

            var parentDir = Directory.GetParent(gyroflowFolder);
            var folderName = parentDir?.Name == FolderNameConstants.Video
                ? parentDir?.Parent?.Name
                : parentDir?.Name;

            var isDateFolder = PathHelper.DateFolderPattern.IsMatch(folderName ?? "");

            var searchOption = isDateFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var folderVideos = Directory.GetFiles(gyroflowFolder, "*.mp4", searchOption)
                .Concat(Directory.GetFiles(gyroflowFolder, "*.mov", searchOption))
                .Select(v => new FileInfo(v))
                .ToList();

            if (sourceIds.Count > 0)
            {
                folderVideos = folderVideos.Where(video =>
                {

                    if (isDateFolder)
                    {
                        var videoParent = video.Directory?.Name;
                        if (videoParent == null) return false;

                        var cameraId = ResolveCameraIdFromFolder(videoParent);
                        return cameraId != null && sourceIds.Contains(cameraId, StringComparer.OrdinalIgnoreCase);
                    }

                    else
                    {
                        if (folderName == null) return false;
                        var cameraId = ResolveCameraIdFromFolder(folderName);
                        return cameraId != null && sourceIds.Contains(cameraId, StringComparer.OrdinalIgnoreCase);
                    }
                }).ToList();
            }

            videos.AddRange(folderVideos);

            if (folderVideos.Count > 0)
            {
                var displayName = $"{folderName}/{FolderNameConstants.Gyroflow}";
                _logger.LogDebug("{FolderName}: {Count} Videos", displayName, folderVideos.Count);
            }
        }

        return videos;
    }

    private List<FileInfo> FindVideosAutomaticMode(string searchPath, string source)
    {
        var videos = new List<FileInfo>();

        var sourceIds = source.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? new List<string>()
            : source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

        var allVideos = Directory.GetFiles(searchPath, "*.mp4", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(searchPath, "*.mov", SearchOption.AllDirectories))
            .Select(v => new FileInfo(v))
            .ToList();

        foreach (var video in allVideos)
        {
            var dirName = video.Directory?.Name ?? "";
            var parentDirName = video.Directory?.Parent?.Name ?? "";

            if (dirName.Equals(FolderNameConstants.Stabilized, StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals(FolderNameConstants.Gyroflow, StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals(FolderNameConstants.TimeLapse, StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals(FolderNameConstants.Export, StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals(FolderNameConstants.Metadata, StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals(FolderNameConstants.Gps, StringComparison.OrdinalIgnoreCase))
                continue;

            if (sourceIds.Count > 0)
            {

                var isParentDate = PathHelper.DateFolderPattern.IsMatch(parentDirName);

                if (!isParentDate)
                {

                    var cameraId = ResolveCameraIdFromFolder(parentDirName) ?? ResolveCameraIdFromFolder(dirName);

                    if (cameraId == null || !sourceIds.Contains(cameraId, StringComparer.OrdinalIgnoreCase))
                        continue;
                }

            }

            videos.Add(video);
        }

        _logger.LogDebug("Automatic Mode: {Count} Videos gefunden", videos.Count);
        return videos;
    }

    public async Task<BatchStabilizationResult> StabilizeBatchAsync(List<FileInfo> videos, PostProcessingOptions options, CancellationToken ct = default)
    {
        _logger.LogDebug("options.Source = '{Source}'", options.Source);
        _logger.LogDebug("Gyroflow Batch-Stabilisierung ({Count} Videos, Modus: {Mode})", videos.Count, options.Mode);

        var jobs = new List<VideoStabilizationJob>();
        var skippedEis = 0;

        foreach (var video in videos)
        {

            if (options.Mode.Equals("automatic", StringComparison.OrdinalIgnoreCase) && _mp4Parser != null)
            {
                try
                {
                    var eisResult = await _mp4Parser.DetectEisStatusAsync(video.FullName, ct);

                    _logger.LogDebug("{VideoName}: {Status} (Zero: {ZeroDensity:F1}%, Quat: {QuatDensity:F1}%)",
                        video.Name, eisResult.Status, eisResult.ZeroDensity, eisResult.QuaternionDensity);

                    if (eisResult.Status == EisStatus.StabilizationOn && !options.Force)
                    {
                        _logger.LogDebug("  Überspringe (EIS AN - nutze --force zum Erzwingen)");
                        skippedEis++;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Fehler bei EIS-Erkennung: {VideoName} - {Message}", video.Name, ex.Message);
                }
            }

            var components = ExtractPathComponents(video.FullName);

            if (components == null)
            {
                _logger.LogError("FEHLER: Konnte Pfad-Komponenten nicht extrahieren für {VideoPath}", video.FullName);
                continue;
            }

            var outputPath = string.Equals(_layoutConfig.SortOrder, SortOrder.TypeFirst, StringComparison.OrdinalIgnoreCase)
                ? CalculateStabilizedPathTypeFirst(components)
                : CalculateStabilizedPathCameraFirst(components);

            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null)
            {
                Directory.CreateDirectory(outputDir);
            }

            _logger.LogDebug("Job erstellt: {VideoName}", video.Name);
            _logger.LogDebug("  Input:  {Input}", video.FullName);
            _logger.LogDebug("  Output: {Output}", outputPath);

            if (File.Exists(outputPath))
            {
                _logger.LogDebug("Bereits stabilisiert: {VideoName}", video.Name);
                continue;
            }

            string? preset = null;
            string gpuDevice = "nvidia";

            var videoCameraId = components.CameraId;

            _logger.LogDebug("Vor TryGetValue - videoCameraId='{Id}'", videoCameraId ?? "(null)");
            if (videoCameraId != null && _config.Cameras.TryGetValue(videoCameraId, out var cameraConfig))
            {
                _logger.LogDebug("TryGetValue SUCCESS - cameraConfig gefunden für '{Id}'", videoCameraId);
                var gyroConfig = cameraConfig.PostProcessing?.Gyroflow;
                if (gyroConfig != null && !string.IsNullOrEmpty(gyroConfig.Preset))
                {
                    _logger.LogDebug("PostProcessing.Gyroflow.Preset = '{Preset}'", gyroConfig.Preset);
                    preset = FindGyroflowPreset(gyroConfig.Preset, videoCameraId);
                    _logger.LogDebug("FindGyroflowPreset() returned '{Preset}'", preset ?? "(null)");

                    if (preset == null)
                    {
                        _logger.LogError("Video {VideoName} übersprungen: Preset '{Preset}' nicht gefunden", video.Name, gyroConfig.Preset);
                        continue;
                    }
                }
            }

            jobs.Add(new VideoStabilizationJob
            {
                InputPath = video.FullName,
                OutputPath = outputPath,
                PresetPath = preset,
                GpuDevice = gpuDevice
            });
        }

        if (jobs.Count == 0)
        {
            if (skippedEis > 0)
            {
                _logger.LogDebug("Alle {Count} Videos haben EIS aktiviert", skippedEis);
                _logger.LogDebug("Tipp: Nutze --force um trotzdem zu stabilisieren");
            }
            else
            {
                _logger.LogDebug("Keine neuen Videos zu stabilisieren");
            }
            return new BatchStabilizationResult { SuccessfulVideos = 0, FailedVideos = 0 };
        }

        _logger.LogDebug("Starte: {Count} Videos (parallel aktiviert: {ParallelEnabled})",
            jobs.Count, _config.Gyroflow.ParallelEnabled);

        var result = await _gyroflowService.StabilizeBatchAsync(jobs, options.StabilizationProgress, options.RenderProgress, ct);

        _logger.LogDebug("Fertig: {Success} stabilisiert, {Failed} Fehler",
            result.SuccessfulVideos, result.FailedVideos);

        if (result.SuccessfulVideos > 0 && !options.DryRun)
        {
            result.OutputFiles = await PostStabilizeWorkflowAsync(jobs, options, ct);
        }

        return result;
    }

    public async Task<(int injected, int failed)> InjectGpsBatchAsync(List<FileInfo> videos, PostProcessingOptions options, CancellationToken ct = default)
    {
        _logger.LogDebug("GPS Injection ({Count} Videos)", videos.Count);

        if (string.IsNullOrEmpty(options.GpxSource) || !Directory.Exists(options.GpxSource))
        {
            _logger.LogWarning("GPX-Quelle nicht konfiguriert oder nicht gefunden");
            return (0, 0);
        }

        int injected = 0;
        int failed = 0;

        foreach (var video in videos)
        {
            _logger.LogDebug("Verarbeite {VideoName}...", video.Name);

            if (options.DryRun)
            {
                _logger.LogDebug("{VideoName} [DRY-RUN]", video.Name);
                continue;
            }

            var success = await _gpsService.InjectOptimizedGpsAsync(video.FullName, options.GpxSource, ct);

            if (success)
            {
                _logger.LogDebug("{VideoName} OK", video.Name);
                injected++;
            }
            else
            {
                _logger.LogWarning("{VideoName} FEHLER", video.Name);
                failed++;
            }
        }

        _logger.LogDebug("Fertig: {Injected} injiziert, {Failed} Fehler", injected, failed);
        return (injected, failed);
    }

    /// <summary>
    /// Findet Videos in Video/postprocess/exported/ Ordnern für den Finalize-Workflow.
    /// Gibt Paare zurück: (exportedFile, matchingOriginalInPostprocess).
    /// </summary>
    public List<(FileInfo Exported, FileInfo? Original)> FindExportedVideosForFinalize(
        string workbench, string? date, string source)
    {
        var results = new List<(FileInfo Exported, FileInfo? Original)>();
        var searchPath = !string.IsNullOrEmpty(date)
            ? Path.Combine(workbench, date)
            : workbench;

        if (!Directory.Exists(searchPath))
            return results;

        var exportedDirs = Directory.GetDirectories(searchPath, FolderNameConstants.Exported, SearchOption.AllDirectories)
            .Where(d => Path.GetFileName(Path.GetDirectoryName(d)!)
                .Equals(FolderNameConstants.PostProcess, StringComparison.OrdinalIgnoreCase));

        foreach (var exportedDir in exportedDirs)
        {
            var postprocessDir = Path.GetDirectoryName(exportedDir)!;

            var videoFiles = Directory.GetFiles(exportedDir, "*.mp4")
                .Concat(Directory.GetFiles(exportedDir, "*.mov"));

            foreach (var exportedPath in videoFiles)
            {
                var exported = new FileInfo(Path.GetFullPath(exportedPath));
                var fileName = exported.Name;

                var originalPath = Path.Combine(postprocessDir, fileName);
                var original = File.Exists(originalPath) ? new FileInfo(originalPath) : null;

                results.Add((exported, original));
            }
        }

        if (!source.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var sourceIds = source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            results = results.Where(r =>
            {

                var parts = r.Exported.FullName.Split(Path.DirectorySeparatorChar);
                return parts.Any(p => sourceIds.Contains(p));
            }).ToList();
        }

        return results;
    }

    /// <summary>
    /// Finalize-Workflow: GPS in exported Videos injizieren, nach Video/ verschieben, Cleanup.
    /// </summary>
    public async Task<(int finalized, int failed)> FinalizeExportedVideosAsync(
        List<(FileInfo Exported, FileInfo? Original)> pairs,
        PostProcessingOptions options,
        CancellationToken ct = default)
    {
        int finalized = 0, failed = 0;

        foreach (var (exported, original) in pairs)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation("Finalize: {FileName}", exported.Name);

            if (options.DryRun)
            {
                _logger.LogDebug("[DRY-RUN] GPS inject + move: {File}", exported.Name);
                finalized++;
                continue;
            }

            try
            {

                if (!string.IsNullOrEmpty(options.GpxSource) && Directory.Exists(options.GpxSource))
                {
                    var success = await _gpsService.InjectOptimizedGpsAsync(
                        exported.FullName, options.GpxSource, ct);

                    if (success)
                        _logger.LogDebug("GPS injiziert: {File}", exported.Name);
                    else
                    {
                        _logger.LogWarning("GPS-Injection fehlgeschlagen: {File}", exported.Name);

                        if (original != null && original.Exists)
                        {
                            var fallbackGpx = PathHelper.GetUmiPath(_config.GlobalPaths.Workbench, original.FullName, FolderNameConstants.UmiSubDir.Gps, FolderNameConstants.OptimizedGpxSuffix);
                            if (File.Exists(fallbackGpx))
                            {
                                _logger.LogDebug("Fallback: Verwende vorhandene GPX aus .metadata/: {Gpx}",
                                    Path.GetFileName(fallbackGpx));

                                var fallbackSuccess = await _gpsService.InjectOptimizedGpsAsync(
                                    exported.FullName, Path.GetDirectoryName(fallbackGpx)!, ct);

                                if (fallbackSuccess)
                                    _logger.LogDebug("GPS injiziert (Fallback): {File}", exported.Name);
                                else
                                    _logger.LogWarning("GPS-Injection auch per Fallback fehlgeschlagen: {File}", exported.Name);
                            }
                        }
                    }
                }

                var postprocessDir = Path.GetDirectoryName(exported.DirectoryName)!;
                var videoDir = Path.GetDirectoryName(postprocessDir)!;
                var targetPath = Path.Combine(videoDir, exported.Name);

                Directory.CreateDirectory(videoDir);

                if (File.Exists(targetPath))
                {
                    _logger.LogWarning("Datei existiert bereits: {Target} — überspringe", targetPath);
                    failed++;
                    continue;
                }

                File.Move(exported.FullName, targetPath);
                _logger.LogInformation("Verschoben: {File} → Video/", exported.Name);

                if (original != null && original.Exists)
                {
                    original.Delete();
                    _logger.LogDebug("Original gelöscht: postprocess/{File}", original.Name);
                }

                finalized++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Finalize: {File}", exported.Name);
                failed++;
            }
        }

        var foldersToCheck = pairs
            .SelectMany(p => new[] { p.Exported.DirectoryName, p.Original?.DirectoryName })
            .Where(d => d != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)!;

        foreach (var folder in foldersToCheck)
        {
            try
            {
                if (Directory.Exists(folder!) && !Directory.EnumerateFileSystemEntries(folder!).Any())
                {
                    Directory.Delete(folder!);
                    _logger.LogDebug("Leeren Ordner gelöscht: {Folder}", Path.GetFileName(folder!));
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Best-effort cleanup failed for folder: {Folder}", folder); }
        }

        var postprocessDirs = pairs
            .Select(p => Path.GetDirectoryName(p.Exported.DirectoryName)!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var ppDir in postprocessDirs)
        {
            try
            {
                if (Directory.Exists(ppDir) && !Directory.EnumerateFileSystemEntries(ppDir).Any())
                {
                    Directory.Delete(ppDir);
                    _logger.LogDebug("Leeren Ordner gelöscht: {Folder}", Path.GetFileName(ppDir));
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Best-effort cleanup failed for postprocess dir: {Dir}", ppDir); }
        }

        return (finalized, failed);
    }

    /// <summary>
    /// Post-Stabilize Workflow: Metadata Restore → Move → Cleanup
    /// </summary>
    public async Task<Dictionary<string, string>> PostStabilizeWorkflowAsync(List<VideoStabilizationJob> jobs, PostProcessingOptions options, CancellationToken ct = default)
    {
        _logger.LogDebug("Starte Post-Stabilize Workflow...");

        var exifToolPath = _config.GlobalPaths.Tools.ExifTool;
        if (string.IsNullOrEmpty(exifToolPath) || !File.Exists(exifToolPath))
        {
            _logger.LogWarning("ExifTool nicht gefunden - überspringe Metadata Restore");
            return new Dictionary<string, string>();
        }

        var outputMapping = new Dictionary<string, string>();
        var processedFolders = new HashSet<string>();

        foreach (var job in jobs)
        {
            var stabilizedFile = new FileInfo(job.OutputPath);
            var originalFile = new FileInfo(job.InputPath);

            stabilizedFile.Refresh();
            if (!stabilizedFile.Exists || stabilizedFile.Length < 1024)
            {
                _logger.LogError("Stabilisierte Datei fehlt oder ist zu klein ({Size} bytes): {Path}",
                    stabilizedFile.Exists ? stabilizedFile.Length : 0, stabilizedFile.FullName);
                continue;
            }

            if (originalFile.Exists)
            {
                try
                {
                    var args = $"-TagsFromFile \"{originalFile.FullName}\" -All:All -overwrite_original \"{stabilizedFile.FullName}\"";
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exifToolPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = System.Diagnostics.Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync(ct);
                        if (process.ExitCode == 0)
                        {
                            _logger.LogDebug("Metadata restored: {FileName}", stabilizedFile.Name);
                        }
                        else
                        {
                            _logger.LogWarning("ExifTool Fehler für {FileName}: Exit {ExitCode}",
                                stabilizedFile.Name, process.ExitCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler bei Metadata Restore: {FileName}", stabilizedFile.Name);
                }
            }

            var stabilizedComponents = ExtractPathComponents(stabilizedFile.FullName);

            if (stabilizedComponents != null)
            {

                var videoDir = string.Equals(_layoutConfig.SortOrder, SortOrder.TypeFirst, StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(stabilizedComponents.TagRoot, FolderNameConstants.Video, stabilizedComponents.CameraId ?? "")
                    : Path.Combine(stabilizedComponents.TagRoot, stabilizedComponents.CameraId ?? "", FolderNameConstants.Video);

                if (stabilizedComponents.CameraId == null)
                {
                    videoDir = Path.Combine(stabilizedComponents.TagRoot, FolderNameConstants.Video);
                }

                var useVideoFolder = Directory.Exists(videoDir);

                var isPostProcess = stabilizedComponents.CameraId != null
                    && _config.Cameras.TryGetValue(stabilizedComponents.CameraId, out var stabilizedCameraConfig)
                    && stabilizedCameraConfig.Features.PostProcess;

                _logger.LogDebug("PostStabilize: stabilizedFile={Path}, CameraId={CameraId}, isPostProcess={IsPostProcess}",
                    stabilizedFile.FullName, stabilizedComponents.CameraId ?? "(null)", isPostProcess);

                string targetPath;
                if (useVideoFolder)
                {

                    var targetDir = isPostProcess
                        ? Path.Combine(videoDir, FolderNameConstants.PostProcess)
                        : videoDir;

                    Directory.CreateDirectory(targetDir);
                    targetPath = Path.Combine(targetDir, stabilizedFile.Name);
                }
                else
                {

                    var baseDir = stabilizedComponents.CameraId != null && _layoutConfig.CameraFolders
                        ? Path.Combine(stabilizedComponents.TagRoot, stabilizedComponents.CameraId)
                        : stabilizedComponents.TagRoot;

                    var targetDir = isPostProcess
                        ? Path.Combine(baseDir, FolderNameConstants.PostProcess)
                        : baseDir;

                    Directory.CreateDirectory(targetDir);
                    targetPath = Path.Combine(targetDir, stabilizedFile.Name);
                }

                if (File.Exists(targetPath))
                {
                    _logger.LogWarning("Datei existiert bereits am Ziel: {FileName} - überspringe", stabilizedFile.Name);

                    outputMapping[job.InputPath] = targetPath;
                }
                else
                {
                    try
                    {
                        File.Move(stabilizedFile.FullName, targetPath);
                        outputMapping[job.InputPath] = targetPath;

                        var targetFolderDisplay = useVideoFolder
                            ? (isPostProcess ? $"{FolderNameConstants.Video}/{FolderNameConstants.PostProcess}" : FolderNameConstants.Video)
                            : (stabilizedComponents.CameraId ?? Path.GetFileName(stabilizedComponents.TagRoot));

                        _logger.LogDebug("Verschoben: Stabilized{Sep}{FileName} → {Target}{Sep}",
                            Path.DirectorySeparatorChar, stabilizedFile.Name, targetFolderDisplay, Path.DirectorySeparatorChar);

                        if (originalFile.Exists)
                        {
                            try
                            {
                                originalFile.Delete();
                                _logger.LogDebug("Original gelöscht: Gyroflow{Sep}{FileName}",
                                    Path.DirectorySeparatorChar, originalFile.Name);
                            }
                            catch (Exception delEx)
                            {
                                _logger.LogWarning(delEx, "Fehler beim Löschen des Originals: {FileName}", originalFile.Name);
                            }
                        }

                        if (stabilizedFile.Directory != null)
                            processedFolders.Add(stabilizedFile.Directory.FullName);
                        if (originalFile.Directory != null)
                            processedFolders.Add(originalFile.Directory.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fehler beim Verschieben: {FileName}", stabilizedFile.Name);
                    }
                }
            }
        }

        foreach (var folder in processedFolders)
        {
            try
            {
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder);
                    var folderName = Path.GetFileName(folder);
                    _logger.LogDebug("Aufgeräumt: {FolderName}{Sep} entfernt (leer)", folderName, Path.DirectorySeparatorChar);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fehler beim Cleanup: {Folder}", folder);
            }
        }

        var parentFolders = processedFolders
            .Select(f => Path.GetDirectoryName(f))
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        foreach (var parent in parentFolders)
        {
            try
            {
                if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    Directory.Delete(parent);
                    _logger.LogDebug("Aufgeräumt: {FolderName}{Sep} entfernt (leer)",
                        Path.GetFileName(parent), Path.DirectorySeparatorChar);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fehler beim Parent-Cleanup: {Folder}", parent);
            }
        }

        _logger.LogDebug("Post-Stabilize Workflow abgeschlossen");
        return outputMapping;
    }

    /// <summary>
    /// Sucht Gyroflow-Preset in folgender Reihenfolge:
    /// 1. App-Root\Presets (z.B. Presets\OA5_Default.gyroflow)
    /// 2. App-Root (neben umi.exe)
    /// 3. Config-Pfad (falls absolut)
    /// </summary>
    private string? FindGyroflowPreset(string presetNameOrPath, string cameraId)
    {
        var searchPaths = new List<string>();
        var appRoot = AppContext.BaseDirectory;

        if (Path.IsPathRooted(presetNameOrPath))
        {
            searchPaths.Add(presetNameOrPath);
        }

        else if (presetNameOrPath.Contains(Path.DirectorySeparatorChar) || presetNameOrPath.Contains('/'))
        {

            searchPaths.Add(Path.Combine(_configPaths.PresetsRoot, presetNameOrPath.Replace('/', Path.DirectorySeparatorChar)));

            searchPaths.Add(Path.Combine(appRoot, "Presets", presetNameOrPath.Replace('/', Path.DirectorySeparatorChar)));

            searchPaths.Add(Path.GetFullPath(presetNameOrPath));
        }

        else
        {
            var presetFileName = Path.GetFileName(presetNameOrPath);

            searchPaths.Add(Path.Combine(_configPaths.GyroflowPresetsDir, presetFileName));

            searchPaths.Add(Path.Combine(appRoot, "Presets", "gyroflow", presetFileName));

            searchPaths.Add(Path.Combine(appRoot, "Presets", presetFileName));

            searchPaths.Add(Path.Combine(appRoot, presetFileName));

            searchPaths.Add(Path.GetFullPath(presetNameOrPath));
        }

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Gyroflow-Preset für {CameraId}: {Path}", cameraId, path);
                return path;
            }
        }

        _logger.LogError("Gyroflow-Preset nicht gefunden für {CameraId}! Gesucht: {Paths}",
            cameraId, string.Join(", ", searchPaths));

        return null;
    }

    /// <summary>
    /// Löst Ordnername zu CameraId auf via Config-Matching.
    /// Prüft CameraId == folderName und FolderName == folderName.
    /// </summary>
    private string? ResolveCameraIdFromFolder(string folderName)
    {
        foreach (var (cameraId, config) in _config.Cameras)
        {

            if (cameraId.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                return cameraId;

            if (!string.IsNullOrEmpty(config.FolderName) &&
                config.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                return cameraId;
        }

        return null;
    }

    /// <summary>
    /// Extrahiert Pfad-Komponenten aus einem Video-Pfad (layout-aware).
    /// Unterstützt camera_first und type_first Layouts.
    /// </summary>
    /// <param name="videoPath">Absoluter Pfad zum Video</param>
    /// <returns>PathComponents mit TagRoot, CameraId, MediaFolder, IsWorkflowFolder</returns>
    private PathComponents? ExtractPathComponents(string videoPath)
    {
        var fullPath = Path.GetFullPath(videoPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory == null)
            return null;

        var tagRoot = PathHelper.FindTagRoot(directory);

        if (tagRoot == null)
        {
            _logger.LogWarning("Kein Tag-Root gefunden für {VideoPath}", videoPath);
            return null;
        }

        var file = new FileInfo(fullPath);
        var immediateParent = file.Directory?.Name;
        var parentParent = file.Directory?.Parent?.Name;

        var sortOrder = _layoutConfig.SortOrder ?? SortOrder.CameraFirst;
        string? cameraId = null;
        string? mediaFolder = null;
        bool isWorkflowFolder = false;

        var workflowFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FolderNameConstants.Gyroflow, FolderNameConstants.Stabilized, FolderNameConstants.TimeLapse,
            FolderNameConstants.Export, FolderNameConstants.Metadata, FolderNameConstants.Gps
        };

        if (string.Equals(sortOrder, SortOrder.TypeFirst, StringComparison.OrdinalIgnoreCase))
        {

            if (parentParent != null && workflowFolders.Contains(parentParent))
            {

                isWorkflowFolder = true;
                mediaFolder = parentParent;
                cameraId = ResolveCameraIdFromFolder(immediateParent ?? "");
            }
            else
            {

                mediaFolder = parentParent;
                cameraId = ResolveCameraIdFromFolder(immediateParent ?? "");
            }
        }
        else
        {

            if (immediateParent != null && workflowFolders.Contains(immediateParent))
            {

                isWorkflowFolder = true;
                mediaFolder = immediateParent;
                cameraId = ResolveCameraIdFromFolder(parentParent ?? "");
            }
            else
            {

                mediaFolder = immediateParent;
                cameraId = ResolveCameraIdFromFolder(parentParent ?? "");
            }

            if (cameraId == null && parentParent != null &&
                PathHelper.DateFolderPattern.IsMatch(parentParent))
            {

                mediaFolder = immediateParent;
            }
        }

        return new PathComponents
        {
            TagRoot = tagRoot,
            CameraId = cameraId,
            MediaFolder = mediaFolder,
            IsWorkflowFolder = isWorkflowFolder,
            FileName = file.Name
        };
    }

    /// <summary>
    /// Pfad-Komponenten für Layout-Aware Operationen.
    /// </summary>
    private class PathComponents
    {
        public required string TagRoot { get; init; }
        public string? CameraId { get; init; }
        public string? MediaFolder { get; init; }
        public bool IsWorkflowFolder { get; init; }
        public required string FileName { get; init; }
    }

    /// <summary>
    /// Berechnet Stabilized-Pfad für type_first Layout.
    /// date/Stabilized/Camera/file.mp4
    /// </summary>
    private string CalculateStabilizedPathTypeFirst(PathComponents components)
    {
        var segments = new List<string> { components.TagRoot, FolderNameConstants.Stabilized };

        if (_layoutConfig.CameraFolders && components.CameraId != null)
        {
            segments.Add(components.CameraId);
        }

        segments.Add(components.FileName);
        return Path.Combine(segments.ToArray());
    }

    /// <summary>
    /// Berechnet Stabilized-Pfad für camera_first Layout.
    /// date/Camera/Stabilized/file.mp4
    /// </summary>
    private string CalculateStabilizedPathCameraFirst(PathComponents components)
    {
        var segments = new List<string> { components.TagRoot };

        if (_layoutConfig.CameraFolders && components.CameraId != null)
        {
            segments.Add(components.CameraId);
        }

        segments.Add(FolderNameConstants.Stabilized);
        segments.Add(components.FileName);
        return Path.Combine(segments.ToArray());
    }
}
