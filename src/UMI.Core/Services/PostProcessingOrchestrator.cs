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
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Orchestriert alle registrierten PostProcessors mit Smart-GPS-Inject-Logik.
///
/// Smart-GPS-Entscheidungstabelle:
/// | Aktive Prozessoren       | Verhalten                                              |
/// |--------------------------|--------------------------------------------------------|
/// | Keine                    | Finaler GPS-Inject direkt nach PreProcessing           |
/// | Nur Gyroflow             | GPS überspringen; Inject NACH Gyroflow (stabilisierte) |
/// | Nur Racerender           | GPS VOR Racerender injizieren                          |
/// | Gyroflow + Racerender    | GPS überspringen; Re-Inject zwischen Gyro + Racerender |
/// </summary>
public class PostProcessingOrchestrator : IPostProcessingOrchestrator
{
    private readonly IReadOnlyList<IPostProcessor> _processors;
    private readonly IExifToolWrapper _exifTool;
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<PostProcessingOrchestrator>? _logger;

    public PostProcessingOrchestrator(
        IEnumerable<IPostProcessor> processors,
        IExifToolWrapper exifTool,
        IProcessHistoryService processHistory,
        ILogger<PostProcessingOrchestrator>? logger = null)
    {
        _processors = processors.OrderBy(p => p.Order).ToList();
        _exifTool = exifTool;
        _processHistory = processHistory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IPostProcessor> GetActiveProcessors(CameraConfig config, ImportContext context)
        => _processors.Where(p => p.IsEnabledForCamera(config, context)).ToList();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PostProcessingResult>> RunAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        IProgressReporter? progressReporter = null,
        CancellationToken ct = default,
        ManualResetEventSlim? pauseEvent = null)
    {
        if (files.Count == 0)
            return Array.Empty<PostProcessingResult>();

        var activeProcessors = GetActiveProcessors(context.Config, context);
        var results = new List<PostProcessingResult>();

        if (progressReporter != null)
        {
            context.RenderProgress = new Progress<GyroflowRenderProgress>(rp =>
                progressReporter.OnRenderProgress(rp));
        }

        if (progressReporter != null)
        {
            context.StabilizationProgress = new Progress<StabilizationProgress>(sp =>
                progressReporter.OnBatchProgress(
                    "Gyroflow", sp.Current, sp.Total, sp.CurrentFile));
        }

        _logger?.LogDebug(
            "[{Camera}] PostProcessing gestartet: {Files} Dateien, {Count} aktive Prozessoren",
            context.CameraId, files.Count, activeProcessors.Count);

        var currentFiles = files.ToList();

        var gpsAlreadyInjected = false;

        var gpsEnabled = context.InjectGps && !context.PostProcess;

        foreach (var processor in activeProcessors)
        {
            ct.ThrowIfCancellationRequested();
            pauseEvent?.Wait(ct);

            if (gpsEnabled && processor.NeedsGps && !gpsAlreadyInjected)
            {
                _logger?.LogInformation(
                    "[{Camera}] Smart-GPS: Injiziere GPS vor {Processor}",
                    context.CameraId, processor.Name);

                await InjectGpsAsync(currentFiles, context, ct);
                gpsAlreadyInjected = true;
            }

            _logger?.LogDebug(
                "[{Camera}] PostProcessor startet: {Name} (Order={Order})",
                context.CameraId, processor.Name, processor.Order);

            var progress = progressReporter != null
                ? new Progress<PostProcessingProgress>(p =>
                    progressReporter.OnPhaseProgress(processor.Name, p.CurrentFile ?? ""))
                : null;

            progressReporter?.OnPhaseStart(processor.Name, currentFiles.Count);
            var result = await processor.ProcessAsync(currentFiles, context, progress, ct);
            progressReporter?.OnPhaseComplete(processor.Name);
            results.Add(result);

            if (result.IsSuccess)
            {
                _logger?.LogDebug(
                    "[{Camera}] PostProcessor abgeschlossen: {Name} – Verarbeitet={Processed}, Übersprungen={Skipped}",
                    context.CameraId, processor.Name, result.ProcessedCount, result.SkippedCount);
            }
            else
            {
                _logger?.LogWarning(
                    "[{Camera}] PostProcessor mit Fehlern: {Name} – Fehlgeschlagen={Failed}: {Errors}",
                    context.CameraId, processor.Name, result.FailedCount,
                    string.Join("; ", result.Errors));
            }

            if (processor.CreatesNewFiles)
            {
                _logger?.LogDebug(
                    "[{Camera}] {Processor} hat neue Dateien erzeugt → GPS-Status zurückgesetzt",
                    context.CameraId, processor.Name);

                gpsAlreadyInjected = false;

                await ReInjectMetadataAsync(result.OutputFiles, context, ct);

                currentFiles = currentFiles.Select(f =>
                    result.OutputFiles.TryGetValue(f.DestPath, out var newPath)
                        ? f with { DestPath = newPath }
                        : f).ToList();
            }
        }

        if (gpsEnabled && !gpsAlreadyInjected)
        {
            _logger?.LogInformation(
                "[{Camera}] Smart-GPS: Finaler GPS-Inject (kein PostProcessor hat GPS angefordert)",
                context.CameraId);

            await InjectGpsAsync(currentFiles, context, ct);
        }

        _logger?.LogDebug(
            "[{Camera}] PostProcessing abgeschlossen: {Count} Prozessoren ausgeführt",
            context.CameraId, results.Count);

        return results;
    }

    /// <summary>
    /// Injiziert GPS-Daten in die Videos via ExifTool.
    /// Findet zugehörige GPX-Datei und injiziert sie ins Video.
    /// </summary>
    private async Task InjectGpsAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct)
    {
        var gpxPath = context.Config.Paths.CustomGpxPath
                      ?? context.GlobalSettings.Paths.GpxSource;

        var videos = files.Where(f => f.IsVideo).ToList();

        if (videos.Count == 0 || !Directory.Exists(gpxPath))
        {
            _logger?.LogDebug("[{Camera}] GPS-Inject übersprungen: keine Videos oder GPX-Verzeichnis fehlt",
                context.CameraId);
            return;
        }

        _logger?.LogInformation("[{Camera}] GPS-Inject für {Count} Videos", context.CameraId, videos.Count);

        foreach (var file in videos)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(file.DestPath))
            {
                _logger?.LogWarning("GPS-Inject: Datei nicht gefunden: {File}", file.DestPath);
                continue;
            }

            var gpxFile = FindMatchingGpx(file, gpxPath);
            if (gpxFile is null)
            {
                _logger?.LogDebug("GPS-Inject: Keine passende GPX für {File}", file.FileName);
                continue;
            }

            try
            {
                if (!context.DryRun)
                {
                    var success = await _exifTool.InjectGpsFromGpxAsync(
                        file.DestPath, gpxFile, overwriteOriginal: true, ct);

                    if (success)
                    {
                        await _processHistory.WriteEntryAsync(file.DestPath, ProcessSteps.GpsInjected,
                            new Dictionary<string, string> { ["gpx"] = gpxFile }, ct);
                        _logger?.LogDebug("GPS injiziert: {File} ← {Gpx}",
                            file.FileName, Path.GetFileName(gpxFile));
                    }
                    else
                        _logger?.LogWarning("GPS-Inject fehlgeschlagen: {File}", file.FileName);
                }
                else
                {
                    _logger?.LogInformation("[DRY-RUN] GPS-Inject: {File} ← {Gpx}",
                        file.FileName, Path.GetFileName(gpxFile));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler bei GPS-Inject: {File}", file.FileName);
            }
        }
    }

    /// <summary>
    /// Kopiert Metadaten vom Original in neu erzeugte Dateien via ExifTool -TagsFromFile.
    /// Wird nach PostProcessors mit CreatesNewFiles=true aufgerufen (z.B. Gyroflow).
    /// </summary>
    private async Task ReInjectMetadataAsync(
        IReadOnlyDictionary<string, string> outputFiles,
        ImportContext context,
        CancellationToken ct)
    {
        if (outputFiles.Count == 0) return;

        _logger?.LogInformation(
            "[{Camera}] Re-Inject Metadaten: {Count} Dateien",
            context.CameraId, outputFiles.Count);

        foreach (var (originalPath, newPath) in outputFiles)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(originalPath) || !File.Exists(newPath))
            {
                _logger?.LogWarning(
                    "Re-Inject übersprungen: Datei nicht gefunden (Original={Orig}, Neu={New})",
                    originalPath, newPath);
                continue;
            }

            if (!context.DryRun)
            {
                var success = await _exifTool.CopyTagsFromFileAsync(
                    originalPath, newPath, overwriteOriginal: true, ct);

                if (success)
                {
                    await _processHistory.WriteEntryAsync(newPath, ProcessSteps.MetadataRestored, ct: ct);
                    _logger?.LogDebug("Metadaten kopiert: {Source} → {Dest}",
                        Path.GetFileName(originalPath), Path.GetFileName(newPath));
                }
                else
                    _logger?.LogWarning("Re-Inject fehlgeschlagen: {Dest}", newPath);
            }
            else
            {
                _logger?.LogInformation("[DRY-RUN] Re-Inject: {Source} → {Dest}",
                    Path.GetFileName(originalPath), Path.GetFileName(newPath));
            }
        }
    }

    /// <summary>
    /// Sucht die passende GPX-Datei für ein Video (Namens-Match ohne Extension).
    /// </summary>
    private static string? FindMatchingGpx(ImportedFileInfo file, string gpxDirectory)
    {
        var baseName = Path.GetFileNameWithoutExtension(file.FileName);
        var candidate = Path.Combine(gpxDirectory, baseName + ".gpx");

        return File.Exists(candidate) ? candidate : null;
    }
}
