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
using UMI.Core.Models;
using UMI.Core.Utilities;
using UMI.Data;
using UMI.Data.Models;

namespace UMI.Core.Services;

/// <summary>
/// Zweiphasiger Import: Scan (EXIF→DB→Sequenzen) → Copy (parallel, direkt ans Ziel).
/// Ersetzt den sequentiellen Import in UniversalCameraHandler.
/// </summary>
public class ImportPipelineService
{
    private readonly MetadataReader _metadataReader;
    private readonly Configuration.LayoutConfig _layoutConfig;
    private readonly BurstProfileLoader? _burstProfileLoader;
    private readonly BurstMatchingEngine _burstMatchingEngine;
    private readonly SequenceGroupingService _sequenceGrouping;
    private readonly LayoutResolver _layoutResolver;
    private readonly EisSortingService _eisSorting;
    private readonly GoProRenameService _goProRenameService;
    private readonly IImportHistoryService? _historyService;
    private readonly IPreProcessingOrchestrator? _preOrchestrator;
    private readonly IPostProcessingOrchestrator? _postOrchestrator;
    private readonly ILogger<ImportPipelineService>? _logger;

    /// <summary>Interne Repräsentation einer entdeckten Quelldatei mit berechnetem Zielnamen.</summary>
    private record DiscoveredFile(FileInfo File, string RelativePath, string EffectiveFileName);

    /// <summary>
    /// Wendet GoProRename und/oder RenameVideos auf einen Dateinamen an.
    /// Einzige Stelle im System die diese Logik implementiert — SD- und MTP-Pfad
    /// rufen diese Methode auf statt die Logik zu duplizieren (DRY).
    /// </summary>
    /// <param name="originalName">Ursprünglicher Dateiname (z.B. "GX010042.MP4").</param>
    /// <param name="isVideo">True wenn die Datei ein Video ist.</param>
    /// <param name="context">Import-Kontext mit Feature-Flags und Capture-Datum.</param>
    /// <param name="captureDate">Aufnahmedatum für Timestamp-Prefix (null = DateTime.Now als Fallback).</param>
    public string ResolveFileName(
        string originalName,
        bool isVideo,
        ImportContext context,
        DateTime? captureDate = null)
    {
        var name = originalName;

        if (context.GoProRename && _goProRenameService.IsGoProFile(name))
        {
            var renamed = _goProRenameService.GetRenamedFileName(name);
            _logger?.LogDebug("GoPro-Rename: {Original} → {Renamed}", originalName, renamed);
            name = renamed;
        }

        if (isVideo && context.RenameVideos && !FileNameHelper.HasTimestampInName(name))
        {
            var ts = captureDate ?? DateTime.Now;
            name = $"{FileNameHelper.BuildTimestampPrefix(ts)}_{name}";
            _logger?.LogDebug("Video-Rename: {Original} → {Renamed}", originalName, name);
        }

        return name;
    }

    public ImportPipelineService(
        MetadataReader metadataReader,
        Configuration.LayoutConfig layoutConfig,
        BurstMatchingEngine burstMatchingEngine,
        SequenceGroupingService sequenceGrouping,
        LayoutResolver layoutResolver,
        EisSortingService eisSorting,
        GoProRenameService goProRenameService,
        BurstProfileLoader? burstProfileLoader = null,
        IImportHistoryService? historyService = null,
        IPreProcessingOrchestrator? preOrchestrator = null,
        IPostProcessingOrchestrator? postOrchestrator = null,
        ILogger<ImportPipelineService>? logger = null)
    {
        _metadataReader = metadataReader;
        _layoutConfig = layoutConfig;
        _burstMatchingEngine = burstMatchingEngine;
        _sequenceGrouping = sequenceGrouping;
        _layoutResolver = layoutResolver;
        _eisSorting = eisSorting;
        _burstProfileLoader = burstProfileLoader;
        _goProRenameService = goProRenameService;
        _historyService = historyService;
        _preOrchestrator = preOrchestrator;
        _postOrchestrator = postOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Phase 1: Scannt Source, liest EXIF, erkennt Sequenzen, berechnet finale Zielpfade.
    /// Nach diesem Aufruf weiß die DB wohin jede Datei kopiert werden muss.
    /// </summary>
    public async Task<ScanResult> ScanSourceAsync(
        ImportContext context,
        ImportDatabase db,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var config = context.Config;
        var cameraId = context.CameraId;
        var folderName = config.FolderName ?? cameraId;

        var discovered = DiscoverSourceFiles(context);
        if (discovered.Count == 0)
        {
            _logger?.LogWarning("Keine Dateien gefunden in: {Path}", context.SourcePath);
            return new ScanResult();
        }
        _logger?.LogDebug("Gefunden: {Count} Dateien auf Source", discovered.Count);

        if (config.BurstDetectionConfig is { Enabled: true } burstConfig
            && burstConfig.ActiveProfiles.Count > 0
            && _burstProfileLoader != null)
        {
            burstConfig.LoadedProfiles = _burstProfileLoader.LoadProfiles(burstConfig.ActiveProfiles);
            _logger?.LogDebug("Burst-Profile geladen: {Profiles}",
                string.Join(", ", burstConfig.LoadedProfiles.Select(p => p.Name)));
        }

        var scanned = 0;
        var skippedByDateFilter = 0;
        var importFiles = new ConcurrentBag<ImportedFile>();
        var subDirMap = new ConcurrentDictionary<string, string>();

        var perFileErrors = 0;
        var skippedScanFiles = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Parallel.ForEachAsync(discovered,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            (df, token) =>
            {
                try
                {
                    var metadata = _metadataReader.ReadPhotoMetadata(df.File.FullName);

                    var shootingMode = _burstMatchingEngine.MatchBurstProfile(metadata, config.BurstDetectionConfig);

                    var captureDate = metadata.CreateDate ?? df.File.LastWriteTime;

                    if (context.HasDateFilter)
                    {
                        if (context.DateFrom.HasValue && captureDate < context.DateFrom.Value)
                        {
                            Interlocked.Increment(ref skippedByDateFilter);
                            return ValueTask.CompletedTask;
                        }
                        if (context.DateTo.HasValue && captureDate > context.DateTo.Value)
                        {
                            Interlocked.Increment(ref skippedByDateFilter);
                            return ValueTask.CompletedTask;
                        }
                    }

                    var isVideo = IsVideoFile(df.File, config);
                    var mediaType = isVideo ? "video" : "photo";

                    var effectiveName = ResolveFileName(df.EffectiveFileName, isVideo, context, captureDate);

                    string subDir;
                    if (isVideo)
                    {
                        subDir = IsInTimelapseFolder(df.File) ? FolderNameConstants.TimeLapse : FolderNameConstants.Video;
                    }
                    else
                    {
                        subDir = FolderNameConstants.Photo;
                    }

                    subDirMap[effectiveName] = subDir;

                    var importFile = new ImportedFile
                    {
                        SourcePath = df.File.FullName,
                        DestPath = "",
                        Filename = effectiveName,
                        CameraId = cameraId,
                        MediaType = mediaType,
                        CaptureDate = captureDate.ToString(DateFormatConstants.FolderFormat),
                        CaptureTime = captureDate.ToString("o"),
                        FileSize = df.File.Length,
                        IsVideo = isVideo ? 1 : 0,
                        CreatedAt = DateTime.UtcNow.ToString("o"),
                        UpdatedAt = DateTime.UtcNow.ToString("o"),
                        CameraModel = metadata.CameraModel,
                        ShootingMode = shootingMode,
                        ExposureTime = metadata.ExposureTime,
                        ContinuousDrive = metadata.ContinuousDrive,
                        ExposureMode = metadata.ExposureMode,
                        DurationMs = metadata.Duration?.Milliseconds,
                    };

                    importFiles.Add(importFile);

                    var count = Interlocked.Increment(ref scanned);
                    progress?.Report(new ScanProgress
                    {
                        Current = count,
                        Total = discovered.Count,
                        CurrentFile = effectiveName,
                        Operation = "Scanning"
                    });
                }
                catch (Exception ex)
                {
                    // Defense-in-depth: anything in the per-file body throwing (a
                    // non-Exif IO problem on the file, a regex disaster in
                    // ResolveFileName, etc.) used to terminate the entire batch.
                    // Log and skip — the rest of the 12k files keep flowing.
                    Interlocked.Increment(ref perFileErrors);
                    skippedScanFiles.Add($"{df.RelativePath} ({ex.GetType().Name}: {ex.Message})");
                    _logger?.LogWarning(ex, "Skipping file due to scan error: {Path}", df.File.FullName);
                }
                return ValueTask.CompletedTask;
            });

        if (perFileErrors > 0)
            _logger?.LogInformation("[{Camera}] Scan: {Errors} Datei(en) wegen Lese-Fehler übersprungen",
                cameraId, perFileErrors);

        var fileList = importFiles.ToList();
        var dayGroups = fileList.GroupBy(f => f.CaptureDate).ToList();

        var daysWithMediaFolders = 0;
        var allConflicts = new List<LayoutConflict>();

        foreach (var dayGroup in dayGroups)
        {
            var dateStr = dayGroup.Key;
            var dayFiles = dayGroup.ToList();

            var hasVideo = dayFiles.Any(f => f.MediaType == "video");
            var hasPhoto = dayFiles.Any(f => f.MediaType == "photo");

            var useMediaFolders = _layoutResolver.ResolveMediaFolders(
                _layoutConfig.MediaFolders,
                hasVideo,
                hasPhoto);

            if (useMediaFolders) daysWithMediaFolders++;

            _logger?.LogDebug(
                "[{CameraId}] Layout [{Date}]: MediaFolders={MediaFolders} (Video:{V}, Photo:{P})",
                cameraId, dateStr, useMediaFolders, hasVideo, hasPhoto);

            var dayConflicts = _layoutResolver.DetectConflicts(
                context.WorkbenchPath, dateStr, cameraId, useMediaFolders);

            if (dayConflicts.Any())
            {
                allConflicts.AddRange(dayConflicts);
                _logger?.LogWarning(
                    "[{CameraId}] Layout-Konflikt [{Date}]: {Count} Dateien liegen flach, aber media_folders würden Unterordner erwarten",
                    cameraId, dateStr, dayConflicts.First().ExistingFiles.Count);
            }

            foreach (var file in dayFiles)
            {
                var subDir = subDirMap.GetValueOrDefault(file.Filename, file.MediaType == "video" ? FolderNameConstants.Video : FolderNameConstants.Photo);
                file.DestPath = _layoutResolver.CalculateDestPath(
                    context.WorkbenchPath,
                    file.CaptureDate,
                    folderName,
                    file.Filename,
                    file.MediaType,
                    subDir,
                    useMediaFolders);
            }

            if (!_layoutConfig.CameraFolders)
            {
                var duplicateNames = dayFiles
                    .GroupBy(f => f.Filename)
                    .Where(g => g.Select(f => f.CameraId).Distinct().Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateNames.Any())
                {
                    _logger?.LogWarning(
                        "Namenskollisionen ohne Kamera-Ordner [{Date}]: {Files}. Suffix _CameraId wird angehängt.",
                        dateStr, string.Join(", ", duplicateNames));

                    foreach (var file in dayFiles.Where(f => duplicateNames.Contains(f.Filename)))
                    {
                        var ext = Path.GetExtension(file.Filename);
                        var name = Path.GetFileNameWithoutExtension(file.Filename);
                        var newFilename = $"{name}_{file.CameraId}{ext}";

                        var subDir = subDirMap.GetValueOrDefault(file.Filename, file.MediaType == "video" ? FolderNameConstants.Video : FolderNameConstants.Photo);
                        file.DestPath = _layoutResolver.CalculateDestPath(
                            context.WorkbenchPath,
                            file.CaptureDate,
                            folderName,
                            newFilename,
                            file.MediaType,
                            subDir,
                            useMediaFolders);

                        file.Filename = newFilename;
                    }
                }
            }
        }

        _logger?.LogDebug(
            "[{CameraId}] Layout: CameraFolders={CamFolders}, MediaFolders={Setting} " +
            "({WithFolders}/{TotalDays} Tage mit Media-Ordnern)",
            cameraId, _layoutConfig.CameraFolders, _layoutConfig.MediaFolders,
            daysWithMediaFolders, dayGroups.Count());

        fileList = fileList.OrderBy(f => f.CaptureTime).ThenBy(f => f.Filename).ToList();
        await db.InsertImportBatchAsync(fileList);
        _logger?.LogDebug("{Count} Dateien in Import-DB geschrieben", fileList.Count);

        var sequences = new List<DetectedSequence>();
        if (config.Features.BurstDetection)
        {
            sequences = await DetectSequencesAsync(db, cameraId, config);
        }

        if (_eisSorting.ShouldSortByEis(context))
        {
            await _eisSorting.ApplyEisSortingAsync(db, context, ct);
        }

        var stats = await db.GetStatsByCameraId(cameraId);

        var result = new ScanResult
        {
            TotalFiles = stats.Photos + stats.Videos,
            Photos = stats.Photos,
            Videos = stats.Videos,
            TotalBytes = stats.TotalBytes,
            Sequences = sequences,
            Stats = stats,
            LayoutConflicts = allConflicts,
            SkippedByDateFilter = skippedByDateFilter,
            SkippedScanFiles = skippedScanFiles.ToList()
        };

        if (!context.IsAdHocFolder)
        {
            result.PendingHistoryEntries = discovered
                .Select(d => (d.RelativePath, d.File.Length))
                .ToList();
        }

        _logger?.LogDebug(
            "Scan abgeschlossen: {Photos} Fotos, {Videos} Videos, {Seq} Sequenzen, {Size}",
            stats.Photos, stats.Videos, sequences.Count, Utilities.FormatHelper.FormatBytes(stats.TotalSize));

        foreach (var seq in sequences)
        {
            _logger?.LogDebug("  Sequenz: {Folder} ({Count} Fotos, Modus: {Mode})",
                seq.FolderName, seq.PhotoCount, seq.Mode);
        }

        return result;
    }

    /// <summary>
    /// Schreibt die Import-History nach erfolgreichem Copy (nur bei FixedPath, nicht DryRun).
    /// Muss nach CopyFilesAsync aufgerufen werden.
    /// </summary>
    public async Task AppendHistoryIfNeededAsync(
        ImportContext context,
        ScanResult scanResult,
        CancellationToken ct = default)
    {
        if (_historyService == null) return;
        if (context.DryRun) return;
        if (context.IsAdHocFolder) return;
        if (scanResult.PendingHistoryEntries.Count == 0) return;

        await _historyService.AppendToHistoryAsync(
            context.CameraId, scanResult.PendingHistoryEntries, ct);

        _logger?.LogDebug("[{Camera}] Import-History aktualisiert: {Count} Einträge",
            context.CameraId, scanResult.PendingHistoryEntries.Count);
    }

    /// <summary>
    /// Erkennt Sequenzen aus DB-Daten. Identische Logik wie BurstDetectionService,
    /// aber arbeitet auf ImportedFile statt FileInfo + ExifTool.
    /// </summary>
    private async Task<List<DetectedSequence>> DetectSequencesAsync(
        ImportDatabase db, string cameraId, CameraConfig config)
    {
        var allSequences = new List<DetectedSequence>();
        var dates = await db.GetDistinctDates(cameraId);

        var burstConfig = LoadBurstConfig(config);

        foreach (var date in dates)
        {
            var photos = await db.GetPhotosByDateAndCamera(cameraId, date);
            if (photos.Count < burstConfig.FallbackMinCount) continue;

            var groups = _sequenceGrouping.GroupPhotosByTimeGaps(photos, burstConfig);

            var hasSequences = groups.Any(g => g.IsSequence);

            foreach (var group in groups.Where(g => g.IsSequence))
            {
                var seq = new DetectedSequence
                {
                    CameraId = cameraId,
                    CaptureDate = group.FirstPhotoTime.ToString(DateFormatConstants.FolderFormat),
                    Mode = group.Mode,
                    FolderName = $"{group.Mode}_{group.FirstPhotoTime:HHmmss}",
                    PhotoCount = group.FileIds.Count,
                    FirstPhotoTime = group.FirstPhotoTime.ToString("o"),
                    ThresholdUsed = group.ThresholdUsed,
                    CreatedAt = DateTime.UtcNow.ToString("o")
                };

                var seqId = await db.InsertSequenceAsync(seq);

                await db.AssignSequenceToFiles(seqId, seq.FolderName, group.FileIds);

                allSequences.Add(seq);

                _logger?.LogDebug(
                    "Sequenz {Folder}: {Count} Fotos, Threshold={Threshold}s",
                    seq.FolderName, seq.PhotoCount, seq.ThresholdUsed);
            }

            if (hasSequences)
            {
                await db.AssignSingleShots(cameraId, date);
                _logger?.LogDebug("Single_Shots für {Date} zugewiesen", date);
            }
        }

        return allSequences;
    }

    /// <summary>
    /// Entdeckt Quelldateien. Für FixedPath: History-Filter + FlattenSource-Präfix.
    /// </summary>
    private List<DiscoveredFile> DiscoverSourceFiles(ImportContext context)
    {
        var config = context.Config;
        var dirInfo = new DirectoryInfo(context.SourcePath);

        if (!dirInfo.Exists)
        {
            _logger?.LogWarning("Quellverzeichnis existiert nicht: {Path}", context.SourcePath);
            return new List<DiscoveredFile>();
        }

        var allExtensions = config.FileTypes.Video
            .Concat(config.FileTypes.Photo)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        HashSet<string>? history = null;
        if (!context.IsAdHocFolder
            && !context.FullImport
            && _historyService != null)
        {
            history = _historyService.LoadHistory(context.CameraId);
            _logger?.LogDebug("[{Camera}] Import-History geladen: {Count} Einträge",
                context.CameraId, history.Count);
        }

        var discovered = new List<DiscoveredFile>();
        var skipped = 0;

        foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (!allExtensions.Contains(file.Extension.ToLowerInvariant()))
                continue;

            var relativePath = Path.GetRelativePath(context.SourcePath, file.FullName)
                .Replace('\\', '/');

            var effectiveName = config.SourceType == SourceType.FixedPath && config.FlattenSource
                ? relativePath.Replace('/', '_')
                : file.Name;

            if (history != null && _historyService!.IsImported(history, relativePath, file.Length))
            {
                skipped++;
                continue;
            }

            discovered.Add(new DiscoveredFile(file, relativePath, effectiveName));
        }

        if (skipped > 0)
        {
            _logger?.LogDebug("[{Camera}] History-Filter: {Skipped} bereits importiert, {New} neu",
                context.CameraId, skipped, discovered.Count);
        }

        return discovered.OrderBy(d => d.File.LastWriteTime).ToList();
    }

    private bool IsVideoFile(FileInfo file, CameraConfig config)
    {
        return config.FileTypes.Video
            .Any(ext => file.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Liest EXIF CreateDate und verschiebt die Datei in den korrekten Datums-Ordner,
    /// falls das MTP-Datum abweicht. Gibt den neuen lokalen Pfad zurück,
    /// oder null wenn keine Korrektur nötig war (oder ein Fehler auftrat).
    ///
    /// Portiert aus ImportCommand.TryCorrectDateFolderAsync (TASK-117).
    /// CLI-Referenzen werden in TASK-118 auf diese Methode umgestellt.
    /// </summary>
    /// <param name="localPath">Aktueller lokaler Pfad der heruntergeladenen Datei.</param>
    /// <param name="currentDateFolder">Datum-Ordner aus MTP-Metadaten (yyyy-MM-dd).</param>
    /// <param name="folderName">Kamera-Ordnername (CameraConfig.FolderName ?? CameraId).</param>
    /// <param name="workbenchPath">Wurzel der Workbench.</param>
    /// <param name="typeFolder">"Video" oder "Photo".</param>
    /// <param name="exifTool">ExifTool-Wrapper für das Lesen der Metadaten.</param>
    /// <param name="logger">Logger (optional).</param>
    /// <param name="ct">Abbruch-Token.</param>
    public static async Task<string?> TryCorrectDateFolderAsync(
        string localPath,
        string currentDateFolder,
        string folderName,
        string workbenchPath,
        string typeFolder,
        Utilities.IExifToolWrapper exifTool,
        ILogger? logger,
        CancellationToken ct)
    {
        try
        {
            var metadata = await exifTool.ReadMetadataAsync(
                localPath,
                new[] { "CreateDate", "MediaCreateDate", "TrackCreateDate" },
                ct);

            if (metadata is null) return null;

            string? createDateStr = null;
            foreach (var key in new[] { "CreateDate", "MediaCreateDate", "TrackCreateDate" })
            {
                if (metadata.TryGetValue(key, out var val) && val is string s && !string.IsNullOrEmpty(s))
                {
                    createDateStr = s;
                    break;
                }
            }

            if (createDateStr is null) return null;

            if (!DateTime.TryParseExact(
                    createDateStr.Split('+')[0].Split('Z')[0].Trim(),
                    new[] { "yyyy:MM:dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy:MM:dd" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var exifDate))
                return null;

            var exifDateFolder = exifDate.ToString(DateFormatConstants.FolderFormat,
                System.Globalization.CultureInfo.InvariantCulture);

            if (exifDateFolder == currentDateFolder) return null;

            var fileName = Path.GetFileName(localPath);
            var newDir = Path.Combine(workbenchPath, exifDateFolder, folderName, typeFolder);
            Directory.CreateDirectory(newDir);
            var newPath = Path.Combine(newDir, fileName);

            if (!File.Exists(newPath))
            {
                File.Move(localPath, newPath);
                logger?.LogInformation(
                    "MTP Datums-Korrektur: {File} → {OldDate} → {NewDate}",
                    fileName, currentDateFolder, exifDateFolder);
                return newPath;
            }
            else
            {
                logger?.LogWarning(
                    "MTP Datums-Korrektur: Ziel existiert bereits: {Path}", newPath);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "EXIF-Scan für Datums-Korrektur fehlgeschlagen: {File}",
                Path.GetFileName(localPath));
            return null;
        }
    }

    private bool IsInTimelapseFolder(FileInfo file)
    {
        return file.FullName.Contains("Timelapse", StringComparison.OrdinalIgnoreCase);
    }

    private BurstDetectionConfig LoadBurstConfig(CameraConfig config)
    {
        var burstConfig = config.BurstDetectionConfig ?? new BurstDetectionConfig();

        if (_burstProfileLoader != null
            && burstConfig.ActiveProfiles.Count > 0
            && (burstConfig.LoadedProfiles == null || burstConfig.LoadedProfiles.Count == 0))
        {
            burstConfig.LoadedProfiles = _burstProfileLoader.LoadProfiles(burstConfig.ActiveProfiles);
            _logger?.LogDebug("Burst-Profile geladen: {Profiles}",
                string.Join(", ", burstConfig.LoadedProfiles.Select(p => p.Name)));
        }

        return burstConfig;
    }

    /// <summary>
    /// Verarbeitet bereits heruntergeladene Dateien (z.B. MTP Direct-Download).
    /// Führt Pre-Processing und Post-Processing aus. Der Copy-Schritt wird übersprungen
    /// (Dateien sind bereits am Ziel). Sequence Detection entfällt — MTP ist video-only.
    /// Gemeinsame Logik mit ScanSourceAsync: Pre/Post-Orchestratoren werden geteilt.
    /// </summary>
    public async Task ProcessDownloadedFilesAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        IProgress<ImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (files.Count == 0) return;

        _logger?.LogInformation(
            "[{Camera}] Post-Download Pipeline: {Count} Datei(en) bereit für Post-Processing",
            context.CameraId, files.Count);

        _logger?.LogDebug("[{Camera}] Sequence Detection übersprungen (MTP: nur Videos)", context.CameraId);

        if (_preOrchestrator is not null)
        {
            _logger?.LogDebug("[{Camera}] MTP Pre-Processing ({Count} Dateien)", context.CameraId, files.Count);
            await _preOrchestrator.RunAsync(files, context, ct);
        }

        if (_postOrchestrator is not null)
        {
            _logger?.LogDebug("[{Camera}] MTP Post-Processing ({Count} Dateien)", context.CameraId, files.Count);
            await _postOrchestrator.RunAsync(files, context, ct: ct);
        }
    }
}
