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
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Utilities;
using UMI.Data;

namespace UMI.Core.Services;

/// <summary>
/// Orchestriert einen vollständigen Import für eine einzelne Kamera-Quelle:
/// Scan → Copy → Pre-Processing → Post-Processing.
/// Kapselt die gemeinsame Import-Logik für CLI und WPF-GUI.
/// </summary>
public class ImportOrchestrationService(
    ImportPipelineService pipeline,
    FileCopyService fileCopyService,
    IPreProcessingOrchestrator preOrchestrator,
    IPostProcessingOrchestrator postOrchestrator,
    ILogger<ImportOrchestrationService>? logger = null,
    IImportHistoryService? historyService = null,
    IThumbnailCacheService? thumbnailCache = null) : IImportOrchestrationService
{
    private readonly IImportHistoryService? _historyService = historyService;
    private readonly IThumbnailCacheService? _thumbnailCache = thumbnailCache;

    public async Task<ImportOrchestrationResult> RunImportAsync(
        ImportContext context,
        IProgressReporter? progressReporter = null,
        CancellationToken ct = default,
        ManualResetEventSlim? pauseEvent = null)
    {
        var sw = Stopwatch.StartNew();
        var warnings = new List<string>();

        var umiDir = Path.Combine(context.WorkbenchPath, FolderNameConstants.UmiDir);
        System.IO.Directory.CreateDirectory(umiDir);
        var dbPath = Path.Combine(umiDir, $".umi-{context.CameraId}.db");
        if (File.Exists(dbPath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
            logger?.LogDebug("Alte Import-DB gelöscht: {Path}", dbPath);
        }

        using var db = new ImportDatabase(dbPath);
        await db.InitializeAsync();

        try
        {

            if (_historyService is not null)
            {
                var folderName = context.Config.FolderName ?? context.CameraId;
                _historyService.ReconcileHistory(
                    context.CameraId, context.WorkbenchPath, folderName);
            }

            progressReporter?.OnScanStart(context.CameraId, context.Config.CameraType);

            var scanResult = await pipeline.ScanSourceAsync(
                context, db,
                progress: new Progress<ScanProgress>(p =>
                    progressReporter?.OnPhaseProgress("Scan", p.CurrentFile)),
                ct: ct);

            progressReporter?.OnScanComplete(context.CameraId, scanResult.TotalFiles, scanResult.TotalBytes);

            logger?.LogDebug(
                "[{Camera}] Scan: {Photos} Fotos, {Videos} Videos, {Seq} Sequenzen",
                context.CameraId, scanResult.Photos, scanResult.Videos, scanResult.Sequences.Count);

            if (scanResult.SkippedByDateFilter > 0)
            {
                logger?.LogInformation(
                    "[{Camera}] Date filter: {Skipped} file(s) outside range, {Remaining} remaining",
                    context.CameraId, scanResult.SkippedByDateFilter,
                    scanResult.TotalFiles);
            }

            if (scanResult.SkippedScanFiles.Count > 0)
            {
                logger?.LogInformation(
                    "[{Camera}] Scan: {Count} file(s) skipped due to read errors — listed in result",
                    context.CameraId, scanResult.SkippedScanFiles.Count);
            }

            if (scanResult.TotalFiles == 0)
            {
                return new ImportOrchestrationResult
                {
                    CameraId = context.CameraId,
                    Success = true,
                    Duration = sw.Elapsed,
                };
            }

            var copyResult = await fileCopyService.CopyFilesAsync(
                db,
                dryRun: context.DryRun,
                progress: new Progress<CopyProgress>(p =>
                    progressReporter?.OnCopyProgress(context.CameraId, p)),
                ct: ct,
                pauseEvent: pauseEvent);

            progressReporter?.OnCopyComplete(context.CameraId);

            logger?.LogDebug(
                "[{Camera}] Copy: {Copied}/{Total} Dateien, {Errors} Fehler",
                context.CameraId, copyResult.CopiedFiles, copyResult.TotalFiles, copyResult.ErrorFiles);

            if (copyResult.ErrorFiles > 0)
            {
                warnings.Add($"{copyResult.ErrorFiles} Datei(en) konnten nicht kopiert werden.");
            }

            if (copyResult.CopiedFiles > 0)
            {
                await pipeline.AppendHistoryIfNeededAsync(context, scanResult, ct);
            }

            if (copyResult.CopiedFiles > 0 && !context.DryRun)
            {

                var destPaths = await db.GetCopiedDestPaths(context.CameraId);
                var importedFiles = destPaths
                    .Where(p => File.Exists(p))
                    .Select(p => new FileInfo(p))
                    .Select(fi => new ImportedFileInfo(
                        SourcePath: fi.FullName,
                        DestPath: fi.FullName,
                        FileName: fi.Name,
                        FileSize: fi.Length,
                        IsPhoto: !context.Config.FileTypes.Video.Any(ext =>
                            ext.Equals(fi.Extension, StringComparison.OrdinalIgnoreCase)),
                        IsVideo: context.Config.FileTypes.Video.Any(ext =>
                            ext.Equals(fi.Extension, StringComparison.OrdinalIgnoreCase))))
                    .ToList();

                if (importedFiles.Count > 0)
                {

                    var preResults = await preOrchestrator.RunAsync(importedFiles, context, ct, pauseEvent);

                    var preErrors = preResults
                        .Where(r => !r.IsSuccess)
                        .SelectMany(r => r.Errors)
                        .ToList();

                    if (preErrors.Count > 0)
                    {
                        warnings.AddRange(preErrors);
                        logger?.LogWarning(
                            "[{Camera}] Pre-Processing-Fehler: {Count}", context.CameraId, preErrors.Count);
                    }

                    await postOrchestrator.RunAsync(importedFiles, context, progressReporter, ct: ct, pauseEvent: pauseEvent);

                    if (context.Config.Features.GenerateThumbnails && _thumbnailCache is not null)
                    {
                        var photoFolders = importedFiles
                            .Where(f => f.IsPhoto)
                            .Select(f => Path.GetDirectoryName(f.DestPath))
                            .Where(d => d is not null)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        foreach (var folder in photoFolders)
                        {
                            logger?.LogInformation(
                                "[{Camera}] GenerateThumbnails: Warming cache for {Folder}",
                                context.CameraId, folder);
                            await _thumbnailCache.WarmCacheAsync(folder!, progress: null, ct);
                        }
                    }
                }
            }

            return new ImportOrchestrationResult
            {
                CameraId = context.CameraId,
                Success = copyResult.ErrorFiles == 0,
                FilesCopied = copyResult.CopiedFiles,
                BytesCopied = copyResult.CopiedBytes,
                PhotoCount = scanResult.Photos,
                VideoCount = scanResult.Videos,
                Duration = sw.Elapsed,
                Warnings = warnings,
                SkippedScanFiles = scanResult.SkippedScanFiles,
            };
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("[{Camera}] Import abgebrochen", context.CameraId);
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[{Camera}] Import fehlgeschlagen", context.CameraId);

            return new ImportOrchestrationResult
            {
                CameraId = context.CameraId,
                Success = false,
                Duration = sw.Elapsed,
                ErrorMessage = ex.Message,
                Warnings = warnings,
            };
        }
    }
}
