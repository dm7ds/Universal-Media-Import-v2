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
using System.IO;
using System.Threading;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action that builds optimized GPX tracks for videos in the workbench
/// and optionally injects GPS data into the video files via ExifTool.
/// Only processes videos from cameras that have GpsInjection enabled.
/// </summary>
public sealed class GpsInjectActionViewModel : ProcessActionViewModel
{

    public override string Title => Strings.Gps_Title;

    public override string Description => Strings.Gps_Description;

    /// <summary>
    /// Location pin icon. 24×24 stroke-based WPF Path Data.
    /// Outer teardrop shape with inner circle.
    /// </summary>
    public override string IconPathData =>
        "M12,2 C8.13,2 5,5.13 5,9 C5,14.25 12,22 12,22 C12,22 19,14.25 19,9 C19,5.13 15.87,2 12,2 Z " +
        "M12,11.5 C10.62,11.5 9.5,10.38 9.5,9 C9.5,7.62 10.62,6.5 12,6.5 C13.38,6.5 14.5,7.62 14.5,9 C14.5,10.38 13.38,11.5 12,11.5 Z";

    private readonly ActionToggle _injectToggle = new(Strings.Gps_ToggleInject);

    public override IReadOnlyList<ActionToggle> Toggles => [_injectToggle];

    private readonly GpsService _gpsService;
    private readonly UmiConfig _config;
    private readonly DateFilterViewModel _dateFilter;
    private readonly IProcessHistoryService _historyService;

    public GpsInjectActionViewModel(
        GpsService gpsService,
        UmiConfig config,
        DateFilterViewModel dateFilter,
        IProcessHistoryService historyService)
    {
        _gpsService     = gpsService;
        _config         = config;
        _dateFilter     = dateFilter;
        _historyService = historyService;
    }

    protected override async Task ExecuteRunAsync(CancellationToken ct)
    {
        var workbench = _config.GlobalPaths.Workbench;

        SetOnUiThread(() =>
        {
            ProgressText = Strings.Common_Scanning;
            CurrentFile  = string.Empty;
        });

        var candidates = Directory
            .EnumerateFiles(workbench, "*.*", SearchOption.AllDirectories)
            .Where(IsGpsCandidate)
            .Select(v =>
            {
                var (date, cameraId) = FolderNameConstants.ExtractDateAndCamera(v);
                return (Path: v, Date: date, CameraId: cameraId);
            })
            .Where(v =>
            {

                if (_dateFilter.HasDateFilter && v.Date != null && !_dateFilter.MatchesDateFolder(v.Date))
                    return false;

                if (v.CameraId != null && _config.Cameras.TryGetValue(v.CameraId, out var camConfig))
                    return camConfig.Features.GpsInjection;
                return false;
            })
            .ToList();

        if (candidates.Count == 0)
        {
            SetOnUiThread(() =>
            {
                StatusMessage = Strings.Gps_NoEnabledVideos;
                IsStatusError = false;
                Progress      = 1;
                ProgressText  = Strings.Common_Done;
            });
            return;
        }

        candidates.Sort((a, b) => string.Compare(
            Path.GetFileName(a.Path), Path.GetFileName(b.Path),
            StringComparison.OrdinalIgnoreCase));

        var total    = candidates.Count;
        var built    = 0;
        var injected = 0;
        var skipped  = 0;

        SetOnUiThread(() =>
        {
            ProgressText  = string.Format(Strings.Common_ProcessingVideos, total);
            StatusMessage = null;
        });

        var resultItems = new List<ActionResultItem>();

        for (var i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry     = candidates[i];
            var videoPath = entry.Path;
            var fileName  = Path.GetFileName(videoPath);
            var current   = i + 1;

            string? gpxDir = null;
            if (entry.CameraId != null && _config.Cameras.TryGetValue(entry.CameraId, out var camConfig))
                gpxDir = camConfig.Paths?.CustomGpxPath;
            gpxDir ??= _config.GlobalPaths.GpxSource;

            var prefix = FolderNameConstants.FormatProgressPrefix(videoPath, fileName);

            SetOnUiThread(() =>
            {
                ProgressText = string.Format(Strings.Gps_BuildingProgress, current, total);
                CurrentFile  = $"{prefix}{fileName}";
                Progress     = (double)current / total;
            });

            if (!Directory.Exists(gpxDir))
            {
                skipped++;
                resultItems.Add(new ActionResultItem
                {
                    Prefix = prefix, FileName = fileName,
                    Status = Strings.Gps_StatusSkippedNoDir, IsSuccess = false
                });
                continue;
            }

            try
            {
                if (_injectToggle.IsChecked)
                {

                    var success = await _gpsService.InjectOptimizedGpsAsync(videoPath, gpxDir, ct);
                    if (success)
                    {
                        built++;
                        injected++;
                        await _historyService.WriteEntryAsync(videoPath, ProcessSteps.GpxBuilt, ct: ct);
                        await _historyService.WriteEntryAsync(videoPath, ProcessSteps.GpsInjected, ct: ct);
                        resultItems.Add(new ActionResultItem
                        {
                            Prefix = prefix, FileName = fileName,
                            Status = Strings.Gps_StatusInjected, IsSuccess = true
                        });
                    }
                    else
                    {
                        skipped++;
                        resultItems.Add(new ActionResultItem
                        {
                            Prefix = prefix, FileName = fileName,
                            Status = Strings.Gps_StatusSkippedNoTrack, IsSuccess = false
                        });
                    }
                }
                else
                {

                    var gpxFile = await _gpsService.OptimizeGpsForVideoAsync(videoPath, gpxDir, ct);
                    if (gpxFile != null)
                    {
                        built++;
                        await _historyService.WriteEntryAsync(videoPath, ProcessSteps.GpxBuilt, ct: ct);
                        resultItems.Add(new ActionResultItem
                        {
                            Prefix = prefix, FileName = fileName,
                            Status = Strings.Gps_StatusGpxBuilt, IsSuccess = true
                        });
                    }
                    else
                    {
                        skipped++;
                        resultItems.Add(new ActionResultItem
                        {
                            Prefix = prefix, FileName = fileName,
                            Status = Strings.Gps_StatusSkippedNoTrack, IsSuccess = false
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {

                skipped++;
                resultItems.Add(new ActionResultItem
                {
                    Prefix = prefix, FileName = fileName,
                    Status = Strings.Gps_StatusSkippedError, IsSuccess = false
                });
            }
        }

        SetOnUiThread(() =>
        {
            Progress     = 1;
            ProgressText = string.Format(Strings.Common_VideosProcessed, total);
            CurrentFile  = string.Empty;

            var parts = new List<string> { string.Format(Strings.Gps_ResultGpxBuilt, built) };
            if (_injectToggle.IsChecked)
                parts.Add(string.Format(Strings.Gps_ResultInjected, injected));
            if (skipped > 0)
                parts.Add(string.Format(Strings.Gps_ResultSkipped, skipped));

            StatusMessage = string.Join(", ", parts);
            IsStatusError = false;

            foreach (var item in resultItems)
                ResultItems.Add(item);
        });
    }

    private static bool IsGpsCandidate(string filePath) =>
        FolderNameConstants.IsVideoCandidate(filePath,
            FolderNameConstants.VideoExclusion.CommonExclusions |
            FolderNameConstants.VideoExclusion.Stabilized);
}
