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
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Implementierung des MTP-Imports. Portiert die bewährte Logik aus
/// ImportCommand.HandleMtpImportAsync und WatchCommand.RunMtpImportAsync
/// in einen wiederverwendbaren Core-Service.
///
/// Ablauf:
/// 1. Dateien auflisten (via IMtpService)
/// 2. History-Filter (bereits importierte überspringen)
/// 3. Direct-Download ins Workbench-Ziel (KEIN Staging!)
/// 4. EXIF-Datums-Korrektur (ImportPipelineService.TryCorrectDateFolderAsync)
/// 5. Post-Processing (ImportPipelineService.ProcessDownloadedFilesAsync)
/// 6. History-Update (MTP-Pfade als Key)
/// 7. LastSeen-Update (IConfigWriterService.RegisterMtpDevice)
/// </summary>
public class MtpImportService : IMtpImportService
{
    private readonly IMtpService _mtpService;
    private readonly IImportHistoryService? _historyService;
    private readonly IConfigWriterService _configWriter;
    private readonly ImportPipelineService _pipeline;
    private readonly IExifToolWrapper _exifTool;
    private readonly IMp4Parser? _mp4Parser;
    private readonly ILogger<MtpImportService>? _logger;

    public MtpImportService(
        IMtpService mtpService,
        IConfigWriterService configWriter,
        ImportPipelineService pipeline,
        IExifToolWrapper exifTool,
        IImportHistoryService? historyService = null,
        IMp4Parser? mp4Parser = null,
        ILogger<MtpImportService>? logger = null)
    {
        _mtpService = mtpService;
        _historyService = historyService;
        _configWriter = configWriter;
        _pipeline = pipeline;
        _exifTool = exifTool;
        _mp4Parser = mp4Parser;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MtpImportResult> ImportAsync(
        MtpImportRequest request,
        IProgress<MtpImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var cameraId = request.CameraId;
        var cfg = request.CameraConfig;
        var folderName = cfg.FolderName ?? cameraId;

        progress?.Report(new MtpImportProgress(0, 0, "", 0, 0, "Listing"));

        var extensions = cfg.FileTypes.Video
            .Concat(cfg.FileTypes.Photo)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        var mtpFiles = _mtpService.ListFiles(request.Device.DeviceId, extensions: extensions);

        _logger?.LogDebug("[{Camera}] MTP: {Count} Dateien auf Gerät", cameraId, mtpFiles.Count);

        if (mtpFiles.Count == 0)
        {
            _logger?.LogWarning(
                "[{Camera}] MTP: Keine Dateien auf Gerät gefunden (DeviceId={DeviceId}, Extensions={Ext})",
                cameraId, request.Device.DeviceId, string.Join(", ", extensions));
            return new MtpImportResult(0, 0, 0, 0L);
        }

        _historyService?.ReconcileHistory(cameraId, request.WorkbenchPath, folderName);

        var history = _historyService?.LoadHistory(cameraId);

        var newFiles = history is null || history.Count == 0
            ? mtpFiles.ToList()
            : mtpFiles.Where(f => !_historyService!.IsImported(history, f.FullPath, f.Length)).ToList();

        var skipped = mtpFiles.Count - newFiles.Count;
        _logger?.LogDebug("[{Camera}] MTP: {New} neu, {Skipped} bereits importiert",
            cameraId, newFiles.Count, skipped);

        if (newFiles.Count == 0)
        {
            return new MtpImportResult(0, 0, skipped, 0L);
        }

        progress?.Report(new MtpImportProgress(0, newFiles.Count, "", 0, 0, "Downloading"));

        var needsEisSorting = request.EisDetection && _mp4Parser != null;
        var context = ImportContextFactory.Create(
            cameraId, cfg, "",
            request.WorkbenchPath, request.GlobalSettings,
            injectGps: request.InjectGps,
            stabilize: request.Stabilize,
            stabilizeMode: request.StabilizeMode,
            noEisSort: !needsEisSorting,
            dryRun: request.DryRun,
            renameVideos: request.RenameVideos ? true : (bool?)null,
            goProRename: request.GoProRename ? true : (bool?)null,
            postProcess: request.PostProcess);

        context.DateFrom = request.DateFrom;
        context.DateTo   = request.DateTo;

        var hasVideos = newFiles.Any(f => IsVideoExtension(Path.GetExtension(f.Name), cfg));
        var hasPhotos = newFiles.Any(f => !IsVideoExtension(Path.GetExtension(f.Name), cfg));
        var needsTypeFolders = hasVideos && hasPhotos;

        var downloaded = new List<ImportedFileInfo>(newFiles.Count);
        var historyEntries = new List<(string RelativePath, long FileSize)>();
        var errorDetails = new List<string>();
        var totalBytes = 0L;
        var downloadErrors = 0;
        var totalMtpBytes = newFiles.Sum(f => f.Length);
        var downloadedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < newFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var file = newFiles[i];
            var isVideo = IsVideoExtension(Path.GetExtension(file.Name), cfg);

            var captureDate = file.DateModified ?? DateTime.Now;

            if (context.HasDateFilter)
            {
                if (context.DateFrom.HasValue && captureDate < context.DateFrom.Value)
                {
                    _logger?.LogDebug("[{Camera}] MTP: Datei übersprungen (vor DateFrom): {File}", cameraId, file.Name);
                    skipped++;
                    continue;
                }
                if (context.DateTo.HasValue && captureDate > context.DateTo.Value)
                {
                    _logger?.LogDebug("[{Camera}] MTP: Datei übersprungen (nach DateTo): {File}", cameraId, file.Name);
                    skipped++;
                    continue;
                }
            }

            var dateFolder = captureDate.ToString(DateFormatConstants.FolderFormat, CultureInfo.InvariantCulture);

            var typeFolder = needsTypeFolders ? (isVideo ? FolderNameConstants.Video : FolderNameConstants.Photo) : "";

            if (isVideo && request.PostProcess)
                typeFolder = typeFolder.Length > 0
                    ? Path.Combine(typeFolder, FolderNameConstants.PostProcess)
                    : FolderNameConstants.PostProcess;

            var targetDir = Path.Combine(request.WorkbenchPath, dateFolder, folderName, typeFolder);

            if (!request.DryRun)
                Directory.CreateDirectory(targetDir);

            var resolvedName = _pipeline.ResolveFileName(file.Name, isVideo, context, captureDate);
            var targetPath = Path.Combine(targetDir, resolvedName);

            if (File.Exists(targetPath))
            {
                _logger?.LogDebug("[{Camera}] MTP: Bereits vorhanden: {File}", cameraId, file.Name);
                continue;
            }

            progress?.Report(new MtpImportProgress(
                i + 1, newFiles.Count, file.Name, totalBytes, totalMtpBytes, "Downloading"));

            try
            {
                string? localPath = request.DryRun
                    ? targetPath
                    : await _mtpService.DownloadFileAsync(
                        request.Device.DeviceId, file.FullPath, targetDir, ct);

                if (localPath is not null)
                {

                    if (!request.DryRun
                        && !string.Equals(Path.GetFileName(localPath), resolvedName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var renamedPath = Path.Combine(Path.GetDirectoryName(localPath)!, resolvedName);
                        File.Move(localPath, renamedPath);
                        localPath = renamedPath;
                    }

                    if (!request.DryRun)
                    {
                        var corrected = await ImportPipelineService.TryCorrectDateFolderAsync(
                            localPath, dateFolder, folderName, request.WorkbenchPath,
                            typeFolder, _exifTool, _logger, ct);
                        if (corrected is not null)
                            localPath = corrected;
                    }

                    downloaded.Add(new ImportedFileInfo(
                        SourcePath: file.FullPath,
                        DestPath: localPath,
                        FileName: resolvedName,
                        FileSize: file.Length,
                        IsPhoto: !isVideo,
                        IsVideo: isVideo));

                    totalBytes += file.Length;
                    historyEntries.Add((file.FullPath, file.Length));
                    downloadedDirs.Add(Path.GetDirectoryName(localPath) ?? targetDir);
                }
                else if (!request.DryRun)
                {
                    downloadErrors++;
                    errorDetails.Add($"{file.Name}: Download fehlgeschlagen");
                    _logger?.LogWarning("[{Camera}] MTP-Download fehlgeschlagen: {File}", cameraId, file.Name);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                downloadErrors++;
                errorDetails.Add($"{file.Name}: {ex.Message}");
                _logger?.LogError(ex, "[{Camera}] MTP-Download-Fehler: {File}", cameraId, file.Name);
            }
        }

        _logger?.LogInformation(
            "[{Camera}] MTP: {Downloaded} heruntergeladen, {Errors} Fehler, {Bytes} Bytes",
            cameraId, downloaded.Count, downloadErrors, totalBytes);

        if (needsEisSorting && !request.DryRun && downloaded.Count > 0)
        {
            var updatedDownloaded = new List<ImportedFileInfo>(downloaded.Count);
            foreach (var fileInfo in downloaded)
            {
                if (!fileInfo.IsVideo || !File.Exists(fileInfo.DestPath))
                {
                    updatedDownloaded.Add(fileInfo);
                    continue;
                }

                ct.ThrowIfCancellationRequested();

                try
                {
                    var eisResult = await _mp4Parser!.DetectEisStatusAsync(fileInfo.DestPath, ct);

                    if (eisResult.Status == EisStatus.StabilizationOff)
                    {

                        var currentDir = Path.GetDirectoryName(fileInfo.DestPath)!;
                        var parentDir = Path.GetDirectoryName(currentDir);

                        var gyroflowDir = parentDir is not null
                            ? Path.Combine(parentDir, FolderNameConstants.Gyroflow)
                            : Path.Combine(currentDir, FolderNameConstants.Gyroflow);

                        Directory.CreateDirectory(gyroflowDir);
                        var newDest = Path.Combine(gyroflowDir, fileInfo.FileName);

                        if (!File.Exists(newDest))
                        {
                            File.Move(fileInfo.DestPath, newDest);
                            _logger?.LogDebug("[{Camera}] MTP EIS aus → Gyroflow/: {File}", cameraId, fileInfo.FileName);
                            downloadedDirs.Add(gyroflowDir);
                            updatedDownloaded.Add(fileInfo with { DestPath = newDest });
                        }
                        else
                        {
                            _logger?.LogWarning("[{Camera}] MTP EIS-Sort: Ziel existiert bereits: {Path}", cameraId, newDest);
                            updatedDownloaded.Add(fileInfo);
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("[{Camera}] MTP EIS an → Video/: {File} ({Status})", cameraId, fileInfo.FileName, eisResult.Status);
                        updatedDownloaded.Add(fileInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[{Camera}] MTP EIS-Check fehlgeschlagen: {File}", cameraId, fileInfo.FileName);
                    updatedDownloaded.Add(fileInfo);
                }
            }
            downloaded = updatedDownloaded;
        }

        if (downloaded.Count > 0)
        {
            progress?.Report(new MtpImportProgress(
                downloaded.Count, downloaded.Count, "", totalBytes, totalMtpBytes, "Processing"));

            await _pipeline.ProcessDownloadedFilesAsync(downloaded, context, null, ct);
        }

        if (!request.DryRun && downloadedDirs.Count > 0)
        {
            DirectoryCleanupHelper.CleanEmptyDirectories(request.WorkbenchPath, downloadedDirs);
        }

        if (historyEntries.Count > 0 && !request.DryRun && _historyService is not null)
        {
            await _historyService.AppendToHistoryAsync(cameraId, historyEntries, ct);
        }

        if (!request.DryRun && downloaded.Count > 0)
        {
            var deviceKey = MtpDeviceDetectionService.GetDeviceKey(request.Device);
            var existing = _configWriter.GetMtpDevice(deviceKey);
            _configWriter.RegisterMtpDevice(deviceKey,
                MtpRegistrationHelper.Create(cameraId, request.Device.FriendlyName, existing: existing));
            await _configWriter.SaveAsync(ct);
        }

        return new MtpImportResult(
            Downloaded: downloaded.Count,
            Failed: downloadErrors,
            Skipped: skipped,
            TotalBytes: totalBytes,
            Errors: errorDetails.Count > 0 ? errorDetails : null);
    }

    /// <summary>
    /// Prüft ob eine Datei-Extension als Video gilt (case-insensitive).
    /// </summary>
    private static bool IsVideoExtension(string extension, CameraConfig cfg)
        => cfg.FileTypes.Video.Any(ext =>
            ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
}
