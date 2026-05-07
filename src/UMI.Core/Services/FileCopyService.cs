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

using System.Threading;
using Microsoft.Extensions.Logging;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Data;

namespace UMI.Core.Services;

/// <summary>
/// Service für paralleles Kopieren von Dateien (Import Phase 2).
/// Kopiert Dateien basierend auf dest_path aus ImportDatabase.
/// Reportet CurrentFile-Info für Spectre.Console Progress.
/// </summary>
public class FileCopyService
{
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<FileCopyService>? _logger;

    public FileCopyService(
        IProcessHistoryService processHistory,
        ILogger<FileCopyService>? logger = null)
    {
        _processHistory = processHistory;
        _logger = logger;
    }

    /// <summary>
    /// Phase 2: Kopiert alle Dateien parallel direkt ans finale Ziel.
    /// dest_path steht bereits fest (aus Phase 1 inkl. Sequenz-Ordner).
    /// </summary>
    public async Task<CopyResult> CopyFilesAsync(
        ImportDatabase db,
        bool dryRun = false,
        IProgress<CopyProgress>? progress = null,
        int maxParallelism = 1,
        CancellationToken ct = default,
        ManualResetEventSlim? pauseEvent = null)
    {
        var jobs = await db.GetPendingCopiesAsync();
        var totalBytes = jobs.Sum(j => j.FileSize);
        long copiedBytes = 0;
        var copiedFiles = 0;
        var skippedFiles = 0;
        var errorFiles = 0;

        _logger?.LogDebug("Kopiere {Count} Dateien ({Size})",
            jobs.Count, Utilities.FormatHelper.FormatBytes(totalBytes));

        if (dryRun)
        {
            _logger?.LogDebug("[DRY-RUN] Keine Dateien werden kopiert");
            foreach (var job in jobs)
            {
                _logger?.LogDebug("[DRY-RUN] {Source} → {Dest}", job.SourcePath, job.DestPath);
                await db.MarkCopyCompletedAsync(job.ImportId);
            }
            return new CopyResult
            {
                TotalFiles = jobs.Count,
                CopiedFiles = 0,
                SkippedFiles = jobs.Count,
                TotalBytes = totalBytes
            };
        }

        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in jobs)
        {
            var dir = Path.GetDirectoryName(job.DestPath);
            if (!string.IsNullOrEmpty(dir)) directories.Add(dir);
        }
        foreach (var dir in directories)
        {
            Directory.CreateDirectory(dir);
        }

        using var semaphore = new SemaphoreSlim(maxParallelism);
        var tasks = jobs.Select(async job =>
        {
            await semaphore.WaitAsync(ct);
            var filename = Path.GetFileName(job.SourcePath);
            long fileCopiedSoFar = 0;

            try
            {

                if (File.Exists(job.DestPath))
                {
                    var existing = new FileInfo(job.DestPath);
                    if (existing.Length == job.FileSize)
                    {
                        await db.MarkCopyCompletedAsync(job.ImportId);
                        Interlocked.Increment(ref skippedFiles);
                        _logger?.LogDebug("Übersprungen (existiert): {File}", filename);
                        return;
                    }
                }

                await CopyWithProgressAsync(
                    job.SourcePath,
                    job.DestPath,
                    job.FileSize,
                    chunkBytes =>
                    {

                        var totalCopied = Interlocked.Add(ref copiedBytes, chunkBytes);

                        fileCopiedSoFar += chunkBytes;

                        progress?.Report(new CopyProgress
                        {
                            CompletedFiles = copiedFiles,
                            TotalFiles = jobs.Count,
                            TotalCopiedBytes = totalCopied,
                            TotalBytes = totalBytes,
                            CurrentFile = filename,
                            CurrentFileSize = job.FileSize,
                            CurrentFileCopiedBytes = fileCopiedSoFar
                        });
                    },
                    ct);

                var sourceInfo = new FileInfo(job.SourcePath);
                File.SetLastWriteTime(job.DestPath, sourceInfo.LastWriteTime);
                File.SetCreationTime(job.DestPath, sourceInfo.CreationTime);

                await db.MarkCopyCompletedAsync(job.ImportId);

                await _processHistory.WriteEntryAsync(job.DestPath, ProcessSteps.Imported,
                    new Dictionary<string, string> { ["source"] = job.SourcePath }, ct);

                var completed = Interlocked.Increment(ref copiedFiles);

                progress?.Report(new CopyProgress
                {
                    CompletedFiles = completed,
                    TotalFiles = jobs.Count,
                    TotalCopiedBytes = Interlocked.Read(ref copiedBytes),
                    TotalBytes = totalBytes,
                    CurrentFile = filename,
                    CurrentFileSize = job.FileSize,
                    CurrentFileCopiedBytes = job.FileSize
                });

                pauseEvent?.Wait(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await db.MarkCopyFailedAsync(job.ImportId, ex.Message);
                Interlocked.Increment(ref errorFiles);
                _logger?.LogError(ex, "Copy-Fehler: {File}", filename);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var result = new CopyResult
        {
            TotalFiles = jobs.Count,
            CopiedFiles = copiedFiles,
            SkippedFiles = skippedFiles,
            ErrorFiles = errorFiles,
            TotalBytes = totalBytes,
            CopiedBytes = copiedBytes
        };

        _logger?.LogDebug(
            "Copy abgeschlossen: {Copied} kopiert, {Skipped} übersprungen, {Errors} Fehler",
            copiedFiles, skippedFiles, errorFiles);

        return result;
    }

    /// <summary>
    /// Kopiert eine Datei stream-basiert mit Intra-File Progress.
    /// Reportet alle ~4 MB den Fortschritt (Sweet Spot: flüssig ohne Flickering).
    /// </summary>
    /// <param name="sourcePath">Quell-Datei</param>
    /// <param name="destPath">Ziel-Datei</param>
    /// <param name="fileSize">Erwartete Dateigröße (für Progress-Berechnung)</param>
    /// <param name="onBytesWritten">Callback bei Progress (Delta-Bytes)</param>
    /// <param name="ct">CancellationToken</param>
    private static async Task CopyWithProgressAsync(
        string sourcePath,
        string destPath,
        long fileSize,
        Action<long>? onBytesWritten = null,
        CancellationToken ct = default)
    {
        const int bufferSize = 1024 * 1024;
        const long reportInterval = 4_194_304;

        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
        using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize, FileOptions.Asynchronous);

        var buffer = new byte[bufferSize];
        long totalWritten = 0;
        long lastReported = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, bufferSize), ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalWritten += bytesRead;

            if (totalWritten - lastReported >= reportInterval || totalWritten >= fileSize)
            {
                var delta = totalWritten - lastReported;
                onBytesWritten?.Invoke(delta);
                lastReported = totalWritten;
            }
        }

        if (totalWritten > lastReported)
        {
            onBytesWritten?.Invoke(totalWritten - lastReported);
        }
    }
}
