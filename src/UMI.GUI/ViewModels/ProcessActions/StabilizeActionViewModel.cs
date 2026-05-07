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

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.Data.Models;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action that runs Gyroflow stabilization on videos in the workbench.
/// Optionally runs EIS detection first (DetectEIS toggle) to move EIS-off videos
/// into the Gyroflow/ folder before stabilization.
/// Phase 2 (stabilization) is dispatched through the GPU Task Queue.
/// </summary>
public sealed class StabilizeActionViewModel : ProcessActionViewModel
{

    public override string Title => Strings.Stabilize_Title;

    public override string Description => Strings.Stabilize_Description;

    /// <summary>
    /// Film-strip icon. 24×24 stroke-based WPF Path Data.
    /// Outer frame, side perforations (top/bottom), center viewport.
    /// </summary>
    public override string IconPathData =>
        "M4,6 H20 V18 H4 Z " +
        "M4,8 H6 V10 H4 Z M4,12 H6 V14 H4 Z " +
        "M18,8 H20 V10 H18 Z M18,12 H20 V14 H18 Z " +
        "M8,8 H16 V16 H8 Z";

    private readonly ActionToggle _detectEisToggle = new(Strings.Stabilize_ToggleDetectEis, tooltip: Strings.Tooltip_StabilizeEis);

    public override IReadOnlyList<ActionToggle> Toggles => [_detectEisToggle];

    private readonly IPostProcessingService _postProcessing;
    private readonly IMp4Parser _mp4Parser;
    private readonly UmiConfig _config;
    private readonly IGpuTaskQueue _gpuTaskQueue;
    private readonly DateFilterViewModel _dateFilter;
    private readonly ConfigPathResolver? _configPaths;

    /// <summary>True while watching a resumed (crash-recovery) queue — no manual run active.</summary>
    private bool _isMonitoringResumedTasks;

    /// <summary>True while ExecuteRunAsync is executing (manual run).</summary>
    private bool _isInManualRun;

    private int _resumedTotal;
    private int _resumedCompleted;
    private int _resumedFailed;

    public StabilizeActionViewModel(
        IPostProcessingService postProcessing,
        IMp4Parser mp4Parser,
        UmiConfig config,
        IGpuTaskQueue gpuTaskQueue,
        DateFilterViewModel dateFilter,
        ConfigPathResolver? configPaths = null)
    {
        _postProcessing = postProcessing;
        _mp4Parser      = mp4Parser;
        _config         = config;
        _gpuTaskQueue   = gpuTaskQueue;
        _dateFilter     = dateFilter;
        _configPaths    = configPaths;

        _gpuTaskQueue.TaskProgress  += OnGlobalTaskProgress;
        _gpuTaskQueue.TaskCompleted += OnGlobalTaskCompleted;
        _gpuTaskQueue.TaskFailed    += OnGlobalTaskFailed;
        _gpuTaskQueue.QueueEmpty    += OnGlobalQueueEmpty;

        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IsCancelling) && IsCancelling && _isMonitoringResumedTasks)
                _ = HandleResumedCancelAsync();
        };
    }

    /// <summary>
    /// Formats a Gyroflow ETA string (e.g. "120s", "45.2s") into a human-readable
    /// duration like "2m 00s", "1h 05m", etc. Falls back to the raw string if unparseable.
    /// </summary>
    private static string FormatEta(string raw)
    {

        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var trimmed = raw.TrimEnd('s', ' ');
        if (!double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var totalSeconds))
            return raw;

        var ts = TimeSpan.FromSeconds(totalSeconds);

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";
        return $"{ts.Seconds}s";
    }

    private void OnGlobalTaskProgress(object? s, GpuTaskProgressEventArgs e)
    {

        if (_isInManualRun) return;

        if (!_isMonitoringResumedTasks)
        {
            _isMonitoringResumedTasks = true;
            _ = InitResumedMonitoringAsync();
        }

        var gPrefix = FolderNameConstants.FormatProgressPrefix(e.InputPath, e.FileName);

        SetOnUiThread(() =>
        {
            IsRunning          = true;
            IsRendering        = true;
            CurrentFile        = e.FileName;
            RenderProgressText = $"{gPrefix}{e.FileName} — {e.Percent:F0}% ETA {FormatEta(e.Eta)}";
            RenderProgress     = e.TotalFrames > 0 ? (double)e.CurrentFrame / e.TotalFrames : 0;

            if (_resumedTotal > 0)
            {
                var perVideoFraction = e.TotalFrames > 0
                    ? (double)e.CurrentFrame / e.TotalFrames
                    : 0;
                Progress = (_resumedCompleted + perVideoFraction) / _resumedTotal;
            }
        });
    }

    private void OnGlobalTaskCompleted(object? s, GpuTaskCompletedEventArgs e)
    {
        if (!_isMonitoringResumedTasks) return;
        var completed = Interlocked.Increment(ref _resumedCompleted);
        SetOnUiThread(() =>
        {
            ProgressText = string.Format(Strings.Stabilize_ResumedProgress, completed, _resumedTotal);
        });
    }

    private void OnGlobalTaskFailed(object? s, GpuTaskFailedEventArgs e)
    {
        if (!_isMonitoringResumedTasks) return;
        Interlocked.Increment(ref _resumedFailed);
    }

    private void OnGlobalQueueEmpty(object? s, EventArgs e)
    {
        if (!_isMonitoringResumedTasks) return;
        _isMonitoringResumedTasks = false;

        SetOnUiThread(() =>
        {
            IsRunning          = false;
            IsRendering        = false;
            RenderProgress     = 0;
            RenderProgressText = string.Empty;
            Progress           = 1;
            CurrentFile        = string.Empty;

            if (_resumedFailed > 0)
            {
                StatusMessage = string.Format(Strings.Stabilize_ResumedWithFailures, _resumedCompleted, _resumedFailed);
                IsStatusError = true;
            }
            else
            {
                StatusMessage = string.Format(Strings.Stabilize_ResumedSuccess, _resumedCompleted);
                IsStatusError = false;
            }
        });
    }

    /// <summary>
    /// Loads queue stats to initialise total/completed counters for the
    /// resumed-monitoring display. Called lazily on the first incoming event.
    /// </summary>
    private async Task InitResumedMonitoringAsync()
    {
        var stats = await _gpuTaskQueue.GetStatsAsync();
        _resumedTotal     = stats.Pending + stats.InProgress + stats.Completed;
        _resumedCompleted = stats.Completed;
        _resumedFailed    = 0;

        SetOnUiThread(() =>
        {
            ProgressText  = string.Format(Strings.Stabilize_ResumingQueued, stats.Pending + stats.InProgress);
            StatusMessage = Strings.Stabilize_ResumedFromPrevious;
            IsStatusError = false;
        });
    }

    /// <summary>
    /// Handles the Cancel button while the resumed queue is being monitored.
    /// Stops the queue and resets the UI state.
    /// </summary>
    private async Task HandleResumedCancelAsync()
    {
        _isMonitoringResumedTasks = false;
        await _gpuTaskQueue.CancelAllAsync();

        SetOnUiThread(() =>
        {
            IsRunning          = false;
            IsCancelling       = false;
            IsRendering        = false;
            RenderProgress     = 0;
            RenderProgressText = string.Empty;
            CurrentFile        = string.Empty;
            Progress           = 0;
            ProgressText       = string.Empty;
            StatusMessage      = Strings.Stabilize_QueueCancelled;
            IsStatusError      = false;
        });
    }

    protected override async Task ExecuteRunAsync(CancellationToken ct)
    {

        _isInManualRun            = true;
        _isMonitoringResumedTasks = false;
        try
        {
            var workbench  = _config.GlobalPaths.Workbench;
            var eisScanned = 0;
            var eisMoved   = 0;

            if (_detectEisToggle.IsChecked)
            {
                (eisScanned, eisMoved) = await RunEisDetectionPhaseAsync(workbench, ct);
                ct.ThrowIfCancellationRequested();
            }

            SetOnUiThread(() =>
            {
                ProgressText = Strings.Common_Scanning;
                CurrentFile  = string.Empty;
            });

            var options = new PostProcessingOptions
            {
                Workbench = workbench,
                Source    = "ALL",
                Mode      = "manual",
            };

            var videos = await _postProcessing.FindVideosAsync(options, ct);

            if (_dateFilter.HasDateFilter)
            {
                videos = videos
                    .Where(v =>
                    {
                        var (date, _) = FolderNameConstants.ExtractDateAndCamera(v.FullName);

                        return date == null || _dateFilter.MatchesDateFolder(date);
                    })
                    .ToList();
            }

            if (videos.Count == 0)
            {
                SetOnUiThread(() =>
                {
                    var eisInfo = eisScanned > 0
                        ? string.Format(Strings.Stabilize_EisScanInfo, eisScanned, eisMoved)
                        : "";
                    StatusMessage = $"{eisInfo}{Strings.Stabilize_NoVideosInGyroflow}";
                    IsStatusError = false;
                    Progress      = 1;
                    ProgressText  = Strings.Common_Done;
                });
                return;
            }

            SetOnUiThread(() =>
            {
                ProgressText  = string.Format(Strings.Stabilize_StabilizingVideos, videos.Count);
                StatusMessage = null;
            });

            var totalVideos     = videos.Count;
            var videosCompleted = 0;
            var videosFailed    = 0;
            var batchDone       = new TaskCompletionSource<bool>();

            var requests = videos.Select(v =>
            {
                var outputPath = FolderNameConstants.CalculateStabilizedOutputPath(v.FullName);
                return new GpuTaskRequest
                {
                    TaskType    = GpuTaskTypes.Gyroflow,
                    InputPath   = v.FullName,
                    OutputPath  = outputPath,
                    FileSize    = v.Length,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(
                        new GyroflowTaskPayload
                        {
                            PresetPath = FindPresetForCamera(),
                        }),
                };
            }).ToList();

            requests.Sort((a, b) => string.Compare(
                Path.GetFileName(a.InputPath),
                Path.GetFileName(b.InputPath),
                StringComparison.OrdinalIgnoreCase));

            string? batchId = null;

            void OnTaskProgress(object? s, GpuTaskProgressEventArgs e)
            {
                if (e.BatchId != batchId) return;
                var prefix = FolderNameConstants.FormatProgressPrefix(e.InputPath, e.FileName);
                SetOnUiThread(() =>
                {
                    IsRendering        = true;
                    CurrentFile        = e.FileName;
                    RenderProgressText = $"{prefix}{e.FileName} — {e.Percent:F0}% ETA {FormatEta(e.Eta)}";
                    RenderProgress     = e.TotalFrames > 0 ? (double)e.CurrentFrame / e.TotalFrames : 0;

                    var remaining = totalVideos - videosCompleted - 1;
                    ProgressText = remaining > 0
                        ? string.Format(Strings.Stabilize_RenderingQueued, videosCompleted + 1, totalVideos, remaining)
                        : string.Format(Strings.Stabilize_Rendering, videosCompleted + 1, totalVideos);

                    if (totalVideos > 0)
                    {
                        var perVideoFraction = e.TotalFrames > 0
                            ? (double)e.CurrentFrame / e.TotalFrames
                            : 0;
                        Progress = (videosCompleted + perVideoFraction) / totalVideos;
                    }
                });
            }

            void OnTaskCompleted(object? s, GpuTaskCompletedEventArgs e)
            {
                if (e.BatchId != batchId) return;
                var completed = Interlocked.Increment(ref videosCompleted);
                var remaining = totalVideos - completed;
                SetOnUiThread(() =>
                {
                    ProgressText = remaining > 0
                        ? string.Format(Strings.Stabilize_StabilizedQueued, completed, totalVideos, remaining)
                        : string.Format(Strings.Stabilize_Stabilized, completed, totalVideos);
                });
            }

            void OnTaskFailed(object? s, GpuTaskFailedEventArgs e)
            {
                if (e.BatchId != batchId) return;
                Interlocked.Increment(ref videosFailed);
            }

            void OnBatchCompleted(object? s, GpuBatchCompletedEventArgs e)
            {
                if (e.BatchId != batchId) return;
                batchDone.TrySetResult(true);
            }

            _gpuTaskQueue.TaskProgress   += OnTaskProgress;
            _gpuTaskQueue.TaskCompleted  += OnTaskCompleted;
            _gpuTaskQueue.TaskFailed     += OnTaskFailed;
            _gpuTaskQueue.BatchCompleted += OnBatchCompleted;

            try
            {

                batchId = await _gpuTaskQueue.EnqueueBatchAsync(requests, ct);

                SetOnUiThread(() =>
                {
                    ProgressText  = string.Format(Strings.Stabilize_QueuedWaiting, totalVideos);
                    StatusMessage = null;
                });

                using var reg = ct.Register(() => batchDone.TrySetCanceled());
                await batchDone.Task;
            }
            catch (OperationCanceledException)
            {

                await _gpuTaskQueue.CancelAllAsync();
                throw;
            }
            finally
            {

                _gpuTaskQueue.TaskProgress   -= OnTaskProgress;
                _gpuTaskQueue.TaskCompleted  -= OnTaskCompleted;
                _gpuTaskQueue.TaskFailed     -= OnTaskFailed;
                _gpuTaskQueue.BatchCompleted -= OnBatchCompleted;
            }

            SetOnUiThread(() =>
            {
                Progress           = 1;
                ProgressText       = string.Format(Strings.Common_VideosProcessed, totalVideos);
                CurrentFile        = string.Empty;
                IsRendering        = false;
                RenderProgress     = 0;
                RenderProgressText = string.Empty;

                var eisInfo = eisScanned > 0
                    ? string.Format(Strings.Stabilize_EisInfoPrefix, eisScanned, eisMoved, FolderNameConstants.Gyroflow)
                    : "";

                if (videosFailed > 0)
                {
                    StatusMessage = $"{eisInfo}{string.Format(Strings.Stabilize_ResultWithFailures, videosCompleted, videosFailed)}";
                    IsStatusError = true;
                }
                else
                {
                    StatusMessage = $"{eisInfo}{string.Format(Strings.Stabilize_ResultSuccess, videosCompleted)}";
                    IsStatusError = false;
                }
            });
        }
        finally
        {

            _isInManualRun = false;
        }
    }

    /// <summary>
    /// Phase 0: Scans all .mp4/.mov files anywhere in the workbench (excluding
    /// Gyroflow/, Stabilized/, Metadata/, GPS/, TimeLapse/, Export/ and
    /// postprocess/exported/ folders). Files that report EIS off are moved to the
    /// Gyroflow/ folder so they will be picked up in Phase 1.
    /// </summary>
    /// <returns>(scanned, movedToGyroflow) counts for final status message.</returns>
    private async Task<(int scanned, int moved)> RunEisDetectionPhaseAsync(string workbench, CancellationToken ct)
    {
        SetOnUiThread(() =>
        {
            ProgressText = Strings.Stabilize_EisScanSearching;
            CurrentFile  = string.Empty;
        });

        var candidates = Directory
            .EnumerateFiles(workbench, "*.*", SearchOption.AllDirectories)
            .Where(IsEisScanCandidate)
            .ToList();

        if (candidates.Count == 0)
        {
            SetOnUiThread(() => ProgressText = Strings.Stabilize_EisScanNoCandidates);
            return (0, 0);
        }

        var total  = candidates.Count;
        var moved  = 0;

        for (var i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();

            var path     = candidates[i];
            var filename = Path.GetFileName(path);
            var current  = i + 1;

            SetOnUiThread(() =>
            {
                ProgressText = string.Format(Strings.Stabilize_EisScanProgress, current, total);
                CurrentFile  = filename;
                Progress     = (double)current / total * 0.4;
            });

            try
            {
                var eisResult = await _mp4Parser.DetectEisStatusAsync(path, ct);

                if (eisResult.Status == EisStatus.StabilizationOff)
                {
                    MoveToGyroflow(path);
                    moved++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {

            }
        }

        SetOnUiThread(() =>
        {
            ProgressText = moved > 0
                ? string.Format(Strings.Stabilize_EisScanDoneMoved, moved, total, FolderNameConstants.Gyroflow)
                : string.Format(Strings.Stabilize_EisScanDoneAllEnabled, total);
            CurrentFile = string.Empty;
            Progress    = 0.4;
        });

        return (total, moved);
    }

    /// <summary>
    /// Returns true when the file is a .mp4 or .mov that is NOT inside any exclusion
    /// folder. Any video anywhere in the workbench is a candidate — no requirement
    /// for a Video/ or postprocess/ parent segment.
    /// Uses forward-slash-normalized paths for consistent matching on all platforms.
    /// </summary>
    private static bool IsEisScanCandidate(string filePath) =>
        FolderNameConstants.IsVideoCandidate(filePath,
            FolderNameConstants.VideoExclusion.CommonExclusions |
            FolderNameConstants.VideoExclusion.Stabilized |
            FolderNameConstants.VideoExclusion.Gyroflow);

    /// <summary>
    /// Moves <paramref name="sourcePath"/> to the Gyroflow/ folder.
    /// If a "Video" segment exists in the path it is replaced with Gyroflow/
    /// (preserving the existing folder structure). Otherwise the file is placed
    /// in a Gyroflow/ subfolder of its current directory.
    /// Creates the target directory if needed.
    /// </summary>
    private static void MoveToGyroflow(string sourcePath)
    {

        var parts    = sourcePath.Replace('\\', '/').Split('/');
        var replaced = false;

        for (var i = 0; i < parts.Length; i++)
        {
            if (!replaced &&
                parts[i].Equals(FolderNameConstants.Video, StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = FolderNameConstants.Gyroflow;
                replaced = true;
            }
        }

        string destPath;
        if (replaced)
        {
            destPath = Path.GetFullPath(string.Join(Path.DirectorySeparatorChar, parts));
        }
        else
        {

            var dir      = Path.GetDirectoryName(sourcePath)!;
            var fileName = Path.GetFileName(sourcePath);
            destPath = Path.GetFullPath(Path.Combine(dir, FolderNameConstants.Gyroflow, fileName));
        }

        if (string.Equals(
            Path.GetFullPath(sourcePath),
            destPath,
            StringComparison.OrdinalIgnoreCase))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.Move(sourcePath, destPath, overwrite: false);
    }

    /// <summary>
    /// Finds the first Gyroflow preset file configured for any camera in the config.
    /// Returns the full path to the preset file, or null if none is configured or found.
    /// </summary>
    private string? FindPresetForCamera()
    {

        foreach (var (_, cameraConfig) in _config.Cameras)
        {
            var presetName = cameraConfig.PostProcessing?.Gyroflow?.Preset;
            if (string.IsNullOrEmpty(presetName)) continue;

            var gyroflowPresetsDir = _configPaths?.GyroflowPresetsDir
                ?? Path.Combine(AppContext.BaseDirectory, "config", "presets", "gyroflow");
            var presetFile = Path.Combine(gyroflowPresetsDir, presetName);
            if (File.Exists(presetFile)) return presetFile;
            if (File.Exists(presetName)) return presetName;
        }
        return null;
    }
}
