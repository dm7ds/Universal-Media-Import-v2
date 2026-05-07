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

using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Utilities;
using UMI.Data.Models;

namespace UMI.Core.Services;

/// <summary>
/// PostProcessor für Gyroflow-Stabilisierung.
/// Reiht Videos in die GPU Queue ein statt direkt zu stabilisieren.
/// NeedsGps=false: GPS wird NACH Gyroflow injiziert (neue stabilisierte Datei).
/// CreatesNewFiles=true: Gyroflow erzeugt neue *_stabilized.mp4 Dateien.
/// Order: 1 (läuft zuerst).
/// </summary>
public class GyroflowPostProcessor : IPostProcessor
{
    private readonly IGpuTaskQueue _gpuTaskQueue;
    private readonly UmiConfig _config;
    private readonly ConfigPathResolver _configPaths;
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<GyroflowPostProcessor>? _logger;

    public string Name => "Gyroflow Stabilization";
    public int Order => 1;

    /// <summary>
    /// GPS-Daten werden NICHT vor Gyroflow benötigt, da Gyroflow eine neue Datei erzeugt
    /// und GPS-Daten im Original verloren gehen würden. GPS wird nach Gyroflow injiziert.
    /// </summary>
    public bool NeedsGps => false;

    /// <summary>
    /// Gyroflow erzeugt neue stabilisierte Dateien → GPS-Status nach diesem Prozessor zurücksetzen.
    /// </summary>
    public bool CreatesNewFiles => true;

    public GyroflowPostProcessor(
        IGpuTaskQueue gpuTaskQueue,
        UmiConfig config,
        ConfigPathResolver configPaths,
        IProcessHistoryService processHistory,
        ILogger<GyroflowPostProcessor>? logger = null)
    {
        _gpuTaskQueue = gpuTaskQueue;
        _config = config;
        _configPaths = configPaths;
        _processHistory = processHistory;
        _logger = logger;
    }

    public bool IsEnabledForCamera(CameraConfig config, ImportContext context)
        => context.Stabilize;

    public async Task<PostProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        IProgress<PostProcessingProgress>? progress = null,
        CancellationToken ct = default)
    {
        var videos = files
            .Where(f => f.IsVideo && File.Exists(f.DestPath)
                     && (context.NoEisSort
                         || f.DestPath.Contains(
                             Path.DirectorySeparatorChar + FolderNameConstants.Gyroflow + Path.DirectorySeparatorChar,
                             StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (videos.Count == 0)
        {
            _logger?.LogInformation("Keine Videos für Gyroflow-Stabilisierung gefunden");
            return PostProcessingResult.Empty(Name);
        }

        _logger?.LogInformation("Enqueue {Count} Videos für GPU Queue (Gyroflow)", videos.Count);

        var presetPath = FindPresetForCamera(context);
        var requests = videos.Select(v => new GpuTaskRequest
        {
            TaskType = GpuTaskTypes.Gyroflow,
            InputPath = v.DestPath,
            OutputPath = FolderNameConstants.CalculateStabilizedOutputPath(v.DestPath),
            FileSize = new FileInfo(v.DestPath).Length,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new GyroflowTaskPayload
            {
                PresetPath = presetPath,
                CameraId = context.CameraId,
                SkipPostStabilize = true,
            }),
        }).ToList();

        requests.Sort((a, b) => string.Compare(
            Path.GetFileName(a.InputPath),
            Path.GetFileName(b.InputPath),
            StringComparison.OrdinalIgnoreCase));

        var videosCompleted = 0;
        var videosFailed = 0;
        var totalVideos = videos.Count;
        var outputFiles = new Dictionary<string, string>();
        var batchDone = new TaskCompletionSource<bool>();
        string? batchId = null;

        void OnTaskProgress(object? s, GpuTaskProgressEventArgs e)
        {
            if (e.BatchId != batchId) return;

            context.RenderProgress?.Report(new GyroflowRenderProgress
            {
                FileName = e.FileName,
                Percent = e.Percent,
                Eta = e.Eta,
                CurrentFrame = e.CurrentFrame,
                TotalFrames = e.TotalFrames,
            });

            context.StabilizationProgress?.Report(new StabilizationProgress
            {
                Current = videosCompleted,
                Total = totalVideos,
                CurrentFile = e.InputPath.Length > 0 ? e.InputPath : e.FileName,
            });
        }

        void OnTaskCompleted(object? s, GpuTaskCompletedEventArgs e)
        {
            if (e.BatchId != batchId) return;
            Interlocked.Increment(ref videosCompleted);

            var matchingRequest = requests.FirstOrDefault(r =>
                Path.GetFileName(r.InputPath).Equals(e.FileName, StringComparison.OrdinalIgnoreCase));
            if (matchingRequest != null && !string.IsNullOrEmpty(e.OutputPath))
            {
                lock (outputFiles)
                    outputFiles[matchingRequest.InputPath] = e.OutputPath;
            }
        }

        void OnTaskFailed(object? s, GpuTaskFailedEventArgs e)
        {
            if (e.BatchId != batchId) return;
            Interlocked.Increment(ref videosFailed);
            _logger?.LogWarning("Gyroflow failed for {File}: {Error}", e.FileName, e.Error);
        }

        void OnBatchCompleted(object? s, GpuBatchCompletedEventArgs e)
        {
            if (e.BatchId != batchId) return;
            batchDone.TrySetResult(true);
        }

        _gpuTaskQueue.TaskProgress += OnTaskProgress;
        _gpuTaskQueue.TaskCompleted += OnTaskCompleted;
        _gpuTaskQueue.TaskFailed += OnTaskFailed;
        _gpuTaskQueue.BatchCompleted += OnBatchCompleted;

        batchId = await _gpuTaskQueue.EnqueueBatchAsync(requests, ct);

        foreach (var req in requests)
        {
            await _processHistory.WriteEntryAsync(req.InputPath, ProcessSteps.GyroflowQueued,
                presetPath != null ? new Dictionary<string, string> { ["preset"] = presetPath } : null, ct);
        }

        _logger?.LogInformation("Batch {BatchId}: {Count} Videos in GPU Queue eingereiht", batchId, videos.Count);

        try
        {
            using var reg = ct.Register(() => batchDone.TrySetCanceled());
            await batchDone.Task;
        }
        catch (OperationCanceledException)
        {
            await _gpuTaskQueue.CancelAllAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _gpuTaskQueue.TaskProgress -= OnTaskProgress;
            _gpuTaskQueue.TaskCompleted -= OnTaskCompleted;
            _gpuTaskQueue.TaskFailed -= OnTaskFailed;
            _gpuTaskQueue.BatchCompleted -= OnBatchCompleted;
        }

        return new PostProcessingResult(
            Name,
            ProcessedCount: videosCompleted,
            SkippedCount: 0,
            FailedCount: videosFailed,
            Errors: videosFailed > 0 ? [$"{videosFailed} video(s) failed"] : Array.Empty<string>())
        {
            BatchId = batchId,
            IsDeferredToQueue = false,
            OutputFiles = outputFiles,
        };
    }

    /// <summary>
    /// Sucht den Gyroflow-Preset-Pfad für die aktuelle Kamera aus der Config.
    /// </summary>
    private string? FindPresetForCamera(ImportContext context)
    {
        if (_config.Cameras.TryGetValue(context.CameraId, out var cameraConfig))
        {
            var presetName = cameraConfig.PostProcessing?.Gyroflow?.Preset;
            if (!string.IsNullOrEmpty(presetName))
            {
                var presetFile = Path.Combine(_configPaths.GyroflowPresetsDir, presetName);
                if (File.Exists(presetFile)) return presetFile;
                if (File.Exists(presetName)) return presetName;
            }
        }
        return null;
    }
}
