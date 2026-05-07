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
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.ViewModels;

namespace UMI.GUI.Services;

/// <summary>
/// WPF implementation of IProgressReporter.
/// Routes all progress callbacks onto the UI dispatcher so that
/// CameraViewModel property changes are always made from the UI thread.
/// One instance is created per import run (per camera).
/// </summary>
public class WpfProgressReporter(Dispatcher dispatcher, CameraViewModel camera) : IProgressReporter
{
    private readonly Stopwatch _copyStopwatch = new();
    private int _totalFiles;
    private int _phaseTotal;
    private int _phaseCompleted;

    public void OnScanStart(string cameraId, string cameraType)
    {
        dispatcher.InvokeAsync(() =>
        {
            camera.Phase = ImportPhase.Scanning;
            camera.PhaseLabel = "Scanning…";
            camera.ProgressText = string.Empty;
            camera.SpeedText = string.Empty;
            camera.EtaText = string.Empty;
        });
    }

    public void OnScanComplete(string cameraId, int fileCount, long totalBytes)
    {
        _totalFiles = fileCount;

        dispatcher.InvokeAsync(() =>
        {
            camera.ProgressText = fileCount == 0
                ? "No new files"
                : $"0 of {fileCount.ToString(CultureInfo.InvariantCulture)} files";
        });
    }

    public void OnCopyProgress(string cameraId, CopyProgress progress)
    {

        if (!_copyStopwatch.IsRunning)
            _copyStopwatch.Restart();

        var elapsed = _copyStopwatch.Elapsed.TotalSeconds;
        var speedBytesPerSec = elapsed > 0
            ? (double)progress.TotalCopiedBytes / elapsed
            : 0.0;

        var remainingBytes = progress.TotalBytes - progress.TotalCopiedBytes;
        TimeSpan? eta = speedBytesPerSec > 0
            ? TimeSpan.FromSeconds(remainingBytes / speedBytesPerSec)
            : null;

        var percent = progress.Percentage;

        var displayFile = Math.Min(progress.CompletedFiles + 1, progress.TotalFiles);
        var progressText = $"{displayFile.ToString(CultureInfo.InvariantCulture)} of {progress.TotalFiles.ToString(CultureInfo.InvariantCulture)} files";
        var speedText = speedBytesPerSec > 0 ? FormatHelper.FormatSpeed(speedBytesPerSec) : string.Empty;
        var etaText = eta.HasValue ? FormatEta(eta.Value) : string.Empty;
        var currentFile = Path.GetFileName(progress.CurrentFile ?? string.Empty);

        dispatcher.InvokeAsync(() =>
        {
            camera.Phase = ImportPhase.Copying;
            camera.PhaseLabel = "Copying…";
            camera.ProgressPercent = percent;
            camera.ProgressText = progressText;
            camera.SpeedText = speedText;
            camera.EtaText = etaText;
            camera.CurrentFile = currentFile;
        });
    }

    public void OnCopyComplete(string cameraId)
    {
        _copyStopwatch.Stop();

        dispatcher.InvokeAsync(() =>
        {
            camera.SpeedText = string.Empty;
            camera.EtaText = string.Empty;
            camera.CurrentFile = string.Empty;
        });
    }

    public void OnPhaseStart(string phase, int totalItems)
    {
        _phaseTotal = totalItems;
        _phaseCompleted = 0;

        var isGyroflow = phase.StartsWith("Gyroflow", StringComparison.OrdinalIgnoreCase);
        var label = phase switch
        {
            _ when isGyroflow => "Stabilizing videos…",
            "GPS"             => "GPS Injection…",
            _                 => $"{phase}…"
        };

        dispatcher.InvokeAsync(() =>
        {
            camera.Phase = ImportPhase.PostProcessing;
            camera.PhaseLabel = label;
            camera.ProgressPercent = 0;
            camera.ProgressText = string.Empty;
            camera.SpeedText = string.Empty;
            camera.EtaText = string.Empty;
            camera.CurrentFile = string.Empty;
        });
    }

    public void OnPhaseProgress(string phase, string item)
    {
        _phaseCompleted++;
        var completed = _phaseCompleted;
        var total = _phaseTotal;

        if (!string.IsNullOrEmpty(item))
        {
            var fileName = Path.GetFileName(item);
            dispatcher.InvokeAsync(() =>
            {
                camera.CurrentFile = fileName;
                if (total > 0)
                {
                    camera.ProgressText = $"Video {completed}/{total}";
                    camera.ProgressPercent = (double)completed / total * 100;
                }
            });
        }
    }

    public void OnBatchProgress(string phase, int current, int total, string currentFile)
    {
        _phaseTotal = total;
        _phaseCompleted = current;
        var (date, _) = FolderNameConstants.ExtractDateAndCamera(currentFile);
        var time = FolderNameConstants.ExtractTimeFromFilename(Path.GetFileName(currentFile));
        var dateTimeInfo = (date, time) switch
        {
            (not null, not null) => $" \u00b7 {date} {time}",
            (not null, null) => $" \u00b7 {date}",
            _ => ""
        };

        dispatcher.InvokeAsync(() =>
        {
            var displayCurrent = Math.Min(current + 1, total);
            camera.PhaseLabel = $"Stabilizing video {displayCurrent}/{total}{dateTimeInfo}";
            camera.ProgressText = $"Video {displayCurrent}/{total}";
            if (total > 0)
                camera.ProgressPercent = (double)current / total * 100;
            if (!string.IsNullOrEmpty(currentFile))
                camera.CurrentFile = Path.GetFileName(currentFile);
        });
    }

    public void OnPhaseComplete(string phase)
    {
        dispatcher.InvokeAsync(() =>
        {
            camera.CurrentFile           = string.Empty;
            camera.IsRendering           = false;
            camera.RenderProgressPercent = 0;
            camera.RenderProgressText    = string.Empty;
        });
    }

    public void OnRenderProgress(GyroflowRenderProgress progress)
    {
        dispatcher.InvokeAsync(() =>
        {
            camera.IsRendering           = true;
            camera.CurrentFile           = progress.FileName;
            camera.RenderProgressText    = $"{progress.FileName} — {progress.Percent:F0}% ETA {progress.Eta}";
            camera.RenderProgressPercent = progress.Percent;

            if (_phaseTotal > 0)
            {
                var perVideoFraction = progress.Percent / 100.0;
                camera.ProgressPercent = (_phaseCompleted + perVideoFraction) / _phaseTotal * 100;
            }
        });
    }

    public void OnError(string cameraId, string message)
    {
        dispatcher.InvokeAsync(() =>
        {
            camera.Phase = ImportPhase.Error;
            camera.PhaseLabel = "Error";
            camera.ResultText = message;
            camera.SpeedText = string.Empty;
            camera.EtaText = string.Empty;
            camera.CurrentFile = string.Empty;
        });
    }

    public void OnComplete(ImportProgressState finalState)
    {
        var totalDuration = _copyStopwatch.Elapsed;

        dispatcher.InvokeAsync(() =>
        {
            camera.Phase = ImportPhase.Done;
            camera.PhaseLabel = string.Empty;
            camera.SpeedText = string.Empty;
            camera.EtaText = string.Empty;
            camera.CurrentFile = string.Empty;
            camera.ProgressPercent = 100.0;

            if (finalState.Cameras.TryGetValue(camera.CameraId, out var camProgress))
            {
                var files = camProgress.ProcessedFiles;
                var bytes = FormatHelper.FormatBytes(camProgress.ProcessedBytes);
                var duration = FormatDuration(totalDuration);
                camera.ResultText = $"{files.ToString(CultureInfo.InvariantCulture)} files · {bytes} · {duration}";
            }
        });
    }

    private static string FormatEta(TimeSpan eta)
    {
        if (eta.TotalHours >= 1)
            return $"~{(int)eta.TotalHours}:{eta.Minutes:D2}:{eta.Seconds:D2} remaining";

        return $"~{eta.Minutes:D1}:{eta.Seconds:D2} remaining";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";

        return $"{duration.Minutes:D1}:{duration.Seconds:D2}";
    }
}
