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
using UMI.Data;

namespace UMI.Core.Services;

/// <summary>
/// Service für EIS-basierte Video-Sortierung (Gyroflow/ vs. Video/) und PostProcess-Routing.
/// Prüft EIS-Status und sortiert EIS-Off Videos nach Gyroflow/.
/// Videos mit postProcess=true werden nach Video/postprocess/ geroutet (statt Video/).
/// </summary>
public class EisSortingService
{
    private readonly IMp4Parser? _mp4Parser;
    private readonly LayoutResolver _layoutResolver;
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<EisSortingService>? _logger;

    public EisSortingService(
        LayoutResolver layoutResolver,
        IProcessHistoryService processHistory,
        IMp4Parser? mp4Parser = null,
        ILogger<EisSortingService>? logger = null)
    {
        _layoutResolver = layoutResolver;
        _processHistory = processHistory;
        _mp4Parser = mp4Parser;
        _logger = logger;
    }

    /// <summary>
    /// Prüft ob EIS-basierte Sortierung oder PostProcess-Routing durchgeführt werden soll.
    /// </summary>
    public bool ShouldSortByEis(ImportContext context)
    {

        var needsEisSorting = context.Config.Features.EisDetection && !context.NoEisSort;

        return needsEisSorting || context.PostProcess;
    }

    /// <summary>
    /// Prüft EIS-Status für alle Videos und passt dest_path an:
    /// EIS-Off → Video/ wird zu Gyroflow/
    /// EIS-On + postProcess=true → Video/ wird zu Video/postprocess/
    /// EIS-On + postProcess=false → bleibt Video/
    /// postProcess=true (kein Gyroflow) → Video/ wird zu Video/postprocess/ (ohne EIS-Check)
    /// </summary>
    public async Task ApplyEisSortingAsync(
        ImportDatabase db,
        ImportContext context,
        CancellationToken ct)
    {

        var needsEisSorting = context.Config.Features.EisDetection && !context.NoEisSort;
        var needsPostProcess = context.PostProcess;

        if (!needsEisSorting && !needsPostProcess)
            return;

        var videos = await db.GetVideosByCamera(context.CameraId);

        if (videos.Count == 0) return;

        _logger?.LogDebug("EIS/PostProcess-Routing für {Count} Videos (EisSorting={EisSort}, PostProcess={PP})",
            videos.Count, needsEisSorting, needsPostProcess);

        var updates = new List<(long importId, string destPath)>();

        foreach (var video in videos)
        {
            if (ct.IsCancellationRequested) break;

            var movedToGyroflow = false;

            if (needsEisSorting && _mp4Parser != null)
            {
                try
                {
                    var eisResult = await _mp4Parser.DetectEisStatusAsync(video.SourcePath, ct);

                    await _processHistory.WriteEntryAsync(video.SourcePath, ProcessSteps.EisDetected,
                        new Dictionary<string, string> { ["status"] = eisResult.Status.ToString() }, ct);

                    if (eisResult.Status == EisStatus.StabilizationOff)
                    {

                        var newDest = _layoutResolver.CalculateDestPath(
                            context.WorkbenchPath,
                            video.CaptureDate,
                            context.CameraId,
                            video.Filename,
                            "video",
                            FolderNameConstants.Gyroflow,
                            useMediaFolders: true);

                        var oldPath = video.DestPath ?? "";
                        if (newDest != oldPath)
                        {
                            updates.Add((video.Id, newDest));
                            _logger?.LogDebug("EIS aus → Gyroflow/: {File}", video.Filename);
                            movedToGyroflow = true;
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("EIS an → Video/: {File} ({Status})",
                            video.Filename, eisResult.Status);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "EIS-Check fehlgeschlagen: {File}", video.Filename);
                }
            }
            else if (needsEisSorting && _mp4Parser == null)
            {
                _logger?.LogWarning("Mp4Parser nicht verfügbar – EIS-Sortierung für {File} übersprungen",
                    video.Filename);
            }

            if (needsPostProcess && !movedToGyroflow)
            {
                var oldDest = video.DestPath ?? "";
                if (string.IsNullOrEmpty(oldDest)) continue;

                var parentDir = Path.GetDirectoryName(oldDest);
                if (parentDir == null) continue;

                var newDest = Path.Combine(parentDir, FolderNameConstants.PostProcess, Path.GetFileName(oldDest));

                updates.Add((video.Id, newDest));
                _logger?.LogDebug("PostProcess → Video/postprocess/: {File}", video.Filename);
            }
        }

        if (updates.Count > 0)
        {
            await db.UpdateDestPathBatchAsync(updates);
            _logger?.LogDebug("{Count} Videos im dest_path aktualisiert (Gyroflow/PostProcess-Routing)",
                updates.Count);
        }
    }
}
