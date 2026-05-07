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
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.CLI.Services;

/// <summary>
/// Einfacher Logger-basierter Reporter für non-interactive Umgebungen.
/// </summary>
public class LogProgressReporter : IProgressReporter
{
    private readonly ILogger _logger;
    private readonly ImportProgressState _state = new();

    public LogProgressReporter(ILogger logger)
    {
        _logger = logger;
    }

    public void OnScanStart(string cameraId, string cameraType)
    {
        _state.Cameras[cameraId] = new CameraProgress
        {
            CameraId = cameraId,
            CameraType = cameraType,
            Phase = CameraPhase.Scanning
        };
        _logger.LogInformation("[{CameraId}] Scan started ({Type})", cameraId, cameraType);
    }

    public void OnScanComplete(string cameraId, int fileCount, long totalBytes)
    {
        if (!_state.Cameras.TryGetValue(cameraId, out var cam)) return;

        cam.TotalFiles = fileCount;
        cam.TotalBytes = totalBytes;
        cam.Phase = CameraPhase.Copying;

        _logger.LogInformation("[{CameraId}] Scan complete: {FileCount} files, {TotalBytes} bytes",
            cameraId, fileCount, totalBytes);
    }

    public void OnCopyProgress(string cameraId, CopyProgress progress)
    {
        if (!_state.Cameras.TryGetValue(cameraId, out var cam)) return;

        cam.ProcessedFiles = progress.CompletedFiles;
        cam.ProcessedBytes = progress.TotalCopiedBytes;

        if (progress.CompletedFiles % 10 == 0 || progress.CompletedFiles == progress.TotalFiles)
        {
            _logger.LogInformation("[{CameraId}] Copy {Current}/{Total}: {Bytes}",
                cameraId, progress.CompletedFiles, progress.TotalFiles,
                FormatHelper.FormatBytes(progress.TotalCopiedBytes));
        }
    }

    public void OnCopyComplete(string cameraId)
    {
        if (!_state.Cameras.TryGetValue(cameraId, out var cam)) return;

        cam.Phase = CameraPhase.Done;
        _logger.LogInformation("[{CameraId}] Copy complete: {Files} files",
            cameraId, cam.TotalFiles);
    }

    public void OnPhaseStart(string phase, int totalItems)
    {
        _logger.LogInformation("[{Phase}] Started: {Total} items", phase, totalItems);
    }

    public void OnPhaseProgress(string phase, string item)
    {
        _logger.LogInformation("[{Phase}] Processing: {Item}", phase, item);
    }

    public void OnPhaseComplete(string phase)
    {
        _logger.LogInformation("[{Phase}] Complete", phase);
    }

    public void OnError(string cameraId, string message)
    {
        _logger.LogError("[{CameraId}] Error: {Message}", cameraId, message);
    }

    public void OnComplete(ImportProgressState finalState)
    {
        _logger.LogInformation("Import complete: {Files} files, {Bytes} bytes",
            finalState.ProcessedFiles, finalState.ProcessedBytes);
    }
}
