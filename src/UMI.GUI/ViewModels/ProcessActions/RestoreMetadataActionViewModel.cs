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
/// Process action that scans all workbench videos, checks for .metadata/ backups,
/// and restores EXIF metadata from backup via MetadataService.
/// </summary>
public sealed class RestoreMetadataActionViewModel : ProcessActionViewModel
{

    public override string Title => Strings.Restore_Title;

    public override string Description => Strings.Restore_Description;

    /// <summary>
    /// Document with restore arrow icon. 24x24 stroke-based WPF Path Data.
    /// </summary>
    public override string IconPathData =>
        "M14,2 H6 C4.9,2 4,2.9 4,4 V20 C4,21.1 4.9,22 6,22 H18 C19.1,22 20,21.1 20,20 V8 L14,2 Z " +
        "M14,2 V8 H20 " +
        "M12,18 C14.21,18 16,16.21 16,14 C16,11.79 14.21,10 12,10 C10.69,10 9.54,10.65 8.85,11.65 " +
        "M8.5,9 V12 H11.5";

    private readonly ActionToggle _forceToggle = new(Strings.Restore_ToggleForce, tooltip: Strings.Tooltip_RestoreForce);

    public override IReadOnlyList<ActionToggle> Toggles => [_forceToggle];

    private readonly MetadataService _metadataService;
    private readonly IProcessHistoryService _historyService;
    private readonly UmiConfig _config;
    private readonly DateFilterViewModel _dateFilter;

    public RestoreMetadataActionViewModel(
        MetadataService metadataService,
        IProcessHistoryService historyService,
        UmiConfig config,
        DateFilterViewModel dateFilter)
    {
        _metadataService = metadataService;
        _historyService  = historyService;
        _config          = config;
        _dateFilter      = dateFilter;
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
            .Where(IsRestoreCandidate)
            .Select(v =>
            {
                var (date, cameraId) = FolderNameConstants.ExtractDateAndCamera(v);
                return (Path: v, Date: date, CameraId: cameraId);
            })
            .Where(v =>
            {

                if (_dateFilter.HasDateFilter && v.Date != null && !_dateFilter.MatchesDateFolder(v.Date))
                    return false;
                return true;
            })
            .ToList();

        if (candidates.Count == 0)
        {
            SetOnUiThread(() =>
            {
                StatusMessage = Strings.Common_NoVideosFound;
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
        var restored = 0;
        var notNeeded = 0;
        var noBackup = 0;
        var failed   = 0;

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

            var prefix = FolderNameConstants.FormatProgressPrefix(videoPath, fileName);

            SetOnUiThread(() =>
            {
                ProgressText = string.Format(Strings.Restore_RestoringProgress, current, total);
                CurrentFile  = $"{prefix}{fileName}";
                Progress     = (double)current / total;
            });

            var metaPath = PathHelper.GetUmiPath(_config.GlobalPaths.Workbench, videoPath, FolderNameConstants.UmiSubDir.Metadata, _config.MetadataBackup.Extension);
            if (!File.Exists(metaPath))
            {
                noBackup++;
                resultItems.Add(new ActionResultItem
                {
                    Prefix = prefix, FileName = fileName,
                    Status = Strings.Restore_StatusNoBackup, IsSuccess = false
                });
                continue;
            }

            var result = await _metadataService.RestoreMetadataAsync(
                videoPath, force: _forceToggle.IsChecked, ct);

            switch (result)
            {
                case MetadataRestoreResult.Restored:
                    restored++;
                    await _historyService.WriteEntryAsync(
                        videoPath, ProcessSteps.MetadataRestored, ct: ct);
                    resultItems.Add(new ActionResultItem
                    {
                        Prefix = prefix, FileName = fileName,
                        Status = Strings.Restore_StatusRestored, IsSuccess = true
                    });
                    break;

                case MetadataRestoreResult.NoRestoreFields:
                    notNeeded++;
                    resultItems.Add(new ActionResultItem
                    {
                        Prefix = prefix, FileName = fileName,
                        Status = Strings.Restore_StatusNotNeeded, IsSuccess = true
                    });
                    break;

                case MetadataRestoreResult.NoBackup:
                    noBackup++;
                    resultItems.Add(new ActionResultItem
                    {
                        Prefix = prefix, FileName = fileName,
                        Status = Strings.Restore_StatusNoBackupFound, IsSuccess = false
                    });
                    break;

                default:
                    failed++;
                    resultItems.Add(new ActionResultItem
                    {
                        Prefix = prefix, FileName = fileName,
                        Status = string.Format(Strings.Restore_StatusFailed, result), IsSuccess = false
                    });
                    break;
            }
        }

        SetOnUiThread(() =>
        {
            Progress     = 1;
            ProgressText = string.Format(Strings.Common_VideosProcessed, total);
            CurrentFile  = string.Empty;

            var parts = new List<string>();
            if (restored > 0) parts.Add(string.Format(Strings.Restore_ResultRestored, restored));
            if (notNeeded > 0) parts.Add(string.Format(Strings.Restore_ResultNotNeeded, notNeeded));
            if (noBackup > 0) parts.Add(string.Format(Strings.Restore_ResultNoBackup, noBackup));
            if (failed > 0) parts.Add(string.Format(Strings.Restore_ResultFailed, failed));

            StatusMessage = parts.Count > 0
                ? string.Join(", ", parts)
                : Strings.Restore_NoVideosProcessed;
            IsStatusError = failed > 0 && restored == 0;

            foreach (var item in resultItems)
                ResultItems.Add(item);
        });
    }

    /// <summary>
    /// Restore candidates: .mp4/.mov excluding Metadata/GPS/TimeLapse/Export/Exported.
    /// Stabilized and Gyroflow folders are INCLUDED — those videos need restore the most.
    /// </summary>
    private static bool IsRestoreCandidate(string filePath) =>
        FolderNameConstants.IsVideoCandidate(filePath,
            FolderNameConstants.VideoExclusion.CommonExclusions);
}
