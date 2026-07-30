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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Helpers;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action that scans all workbench videos and displays a metadata overview table
/// with Date, Source, File, Status, EIS, and GPS columns.
/// </summary>
public sealed class StatisticsActionViewModel : ProcessActionViewModel
{

    public override string Title => Strings.Statistics_Title;

    public override string Description => Strings.Statistics_Description;

    /// <summary>
    /// Table/grid icon. 24x24 stroke-based WPF Path Data.
    /// Represents a simple data table with rows and columns.
    /// </summary>
    public override string IconPathData =>
        "M3,3 H21 V21 H3 Z M3,9 H21 M3,15 H21 M9,3 V21 M15,3 V21";

    /// <summary>
    /// Identifies this action as the statistics card so the XAML template
    /// can show the statistics table instead of the standard results list.
    /// </summary>
    public override bool IsStatisticsAction => true;

    /// <summary>Per-video metadata rows shown in the statistics table.</summary>
    public RangeObservableCollection<StatisticsRowViewModel> StatisticsRows { get; } = new();

    private bool _hasStatisticsRows;
    /// <summary>True when there are statistics rows to display.</summary>
    public bool HasStatisticsRows
    {
        get => _hasStatisticsRows;
        private set => SetProperty(ref _hasStatisticsRows, value);
    }

    private readonly IMp4Parser _mp4Parser;
    private readonly IProcessHistoryService _historyService;
    private readonly UmiConfig _config;
    private readonly DateFilterViewModel _dateFilter;

    public StatisticsActionViewModel(
        IMp4Parser mp4Parser,
        IProcessHistoryService historyService,
        UmiConfig config,
        DateFilterViewModel dateFilter)
    {
        _mp4Parser      = mp4Parser;
        _historyService = historyService;
        _config         = config;
        _dateFilter     = dateFilter;

        StatisticsRows.CollectionChanged += (_, _) =>
            HasStatisticsRows = StatisticsRows.Count > 0;
    }

    protected override async Task ExecuteRunAsync(CancellationToken ct)
    {
        var workbench = _config.GlobalPaths.Workbench;

        SetOnUiThread(() =>
        {
            ProgressText = Strings.Common_Scanning;
            CurrentFile  = string.Empty;
            StatisticsRows.Clear();
        });

        var candidates = Directory
            .EnumerateFiles(workbench, "*.*", SearchOption.AllDirectories)
            .Where(IsVideoCandidate)
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

        var total = candidates.Count;
        var rows  = new List<StatisticsRowViewModel>(total);

        SetOnUiThread(() =>
        {
            ProgressText  = string.Format(Strings.Statistics_ScanningVideos, total);
            StatusMessage = null;
        });

        var semaphore = new SemaphoreSlim(4);
        var processed = 0;

        var tasks = candidates.Select(async entry =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();

                var videoPath = entry.Path;
                var fileName  = Path.GetFileName(videoPath);

                var current = Interlocked.Increment(ref processed);
                SetOnUiThread(() =>
                {
                    ProgressText = string.Format(Strings.Statistics_ScanningProgress, current, total);
                    CurrentFile  = $"{FolderNameConstants.FormatProgressPrefix(videoPath, fileName)}{fileName}";
                    Progress     = (double)current / total;
                });

                var date     = entry.Date ?? "\u2014";
                var cameraId = entry.CameraId ?? "\u2014";

                ProcessHistory? history = null;
                try
                {
                    history = await _historyService.ReadAsync(videoPath, ct);
                }
                catch (OperationCanceledException)
                {
                    throw; // FIX-3 (TASK-218): propagate cancellation, do not swallow
                }
                catch
                {

                }

                var eisText = "N/A";
                try
                {
                    var eisResult = await _mp4Parser.DetectEisStatusAsync(videoPath, ct);
                    eisText = eisResult.Status switch
                    {
                        EisStatus.StabilizationOn  => "ON",
                        EisStatus.StabilizationOff => "OFF",
                        _                          => "N/A"
                    };
                }
                catch (OperationCanceledException)
                {
                    throw; // FIX-3 (TASK-218): propagate cancellation, do not swallow
                }
                catch
                {

                }

                var historyStatus = DeriveHistoryStatus(history);
                var folderStatus  = DeriveFolderStatus(videoPath);
                var status        = historyStatus ?? folderStatus;

                var isWarning = historyStatus != null
                    && !string.Equals(historyStatus, folderStatus, StringComparison.Ordinal);

                var gpsText = DeriveGps(history);

                var backupText = DeriveBackup(videoPath, entry.CameraId);

                var integrityText = DeriveIntegrity(videoPath);

                if (backupText == Strings.Statistics_BackupMissing)
                    isWarning = true;
                if (integrityText != "OK" && integrityText != "\u2014")
                    isWarning = true;

                return new StatisticsRowViewModel
                {
                    Date      = date,
                    Source    = cameraId,
                    FileName  = fileName,
                    Status    = status,
                    Eis       = eisText,
                    Gps       = gpsText,
                    Backup    = backupText,
                    Integrity = integrityText,
                    IsWarning = isWarning,
                };
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);

        var sorted = results
            .OrderBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SetOnUiThread(() =>
        {
            Progress     = 1;
            ProgressText = string.Format(Strings.Statistics_VideosScanned, total);
            CurrentFile  = string.Empty;

            StatusMessage = string.Format(Strings.Statistics_VideosScannedStatus, total);
            IsStatusError = false;

            StatisticsRows.AddRange(sorted);
        });
    }

    /// <summary>
    /// Returns status from process history, or null if no history exists.
    /// </summary>
    private static string? DeriveHistoryStatus(ProcessHistory? history)
    {
        var lastStep = history?.Entries.LastOrDefault()?.Step;
        return lastStep switch
        {
            ProcessSteps.Finalized        => Strings.Statistics_ReadyToUse,
            ProcessSteps.Graded           => Strings.Statistics_GradedRunFinalize,
            ProcessSteps.GpsInjected      => Strings.Statistics_GpsInjected,
            ProcessSteps.MetadataRestored => Strings.Statistics_Stabilized,
            ProcessSteps.GyroflowDone     => Strings.Statistics_Stabilized,
            ProcessSteps.GyroflowQueued   => Strings.Statistics_InGyroflowQueue,
            ProcessSteps.EisDetected      => DeriveFromEisDetails(history!),
            ProcessSteps.Imported         => Strings.Statistics_Imported,
            _                             => null
        };
    }

    private static string DeriveFromEisDetails(ProcessHistory history)
    {
        var eisEntry = history.Entries.LastOrDefault(e => e.Step == ProcessSteps.EisDetected);
        if (eisEntry?.Details?.TryGetValue("status", out var eisStatus) == true)
        {
            return eisStatus == "StabilizationOff" ? Strings.Statistics_AwaitingGyroflow : Strings.Statistics_Ready;
        }
        return Strings.Statistics_EisChecked;
    }

    private static string DeriveFolderStatus(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');

        if (normalized.Contains($"/{FolderNameConstants.PostProcess}/exported/", StringComparison.OrdinalIgnoreCase))
            return Strings.Statistics_Graded;
        if (normalized.Contains($"/{FolderNameConstants.PostProcess}/", StringComparison.OrdinalIgnoreCase))
            return Strings.Statistics_AwaitingGrading;
        if (normalized.Contains($"/{FolderNameConstants.Stabilized}/", StringComparison.OrdinalIgnoreCase))
            return Strings.Statistics_Stabilized;
        if (normalized.Contains($"/{FolderNameConstants.Gyroflow}/", StringComparison.OrdinalIgnoreCase))
            return Strings.Statistics_AwaitingGyroflow;

        return Strings.Statistics_Ready;
    }

    private static string DeriveGps(ProcessHistory? history)
    {
        if (history == null)
            return Strings.Statistics_GpsNo;

        if (history.Entries.Any(e => e.Step == ProcessSteps.GpsInjected))
            return Strings.Statistics_GpsYes;
        if (history.Entries.Any(e => e.Step == ProcessSteps.GpxBuilt))
            return Strings.Statistics_GpsBuilt;

        return Strings.Statistics_GpsNo;
    }

    /// <summary>
    /// Checks whether a .meta.json backup file exists for the given video.
    /// Returns "N/A" if the camera does not have the metadata_backup feature enabled.
    /// </summary>
    private string DeriveBackup(string videoPath, string? cameraId)
    {

        if (cameraId != null
            && _config.Cameras.TryGetValue(cameraId, out var cam)
            && cam.Features.MetadataBackup)
        {
            var metaPath = PathHelper.GetUmiPath(_config.GlobalPaths.Workbench, videoPath, FolderNameConstants.UmiSubDir.Metadata, _config.MetadataBackup.Extension);
            if (File.Exists(metaPath))
                return "OK";
            return Strings.Statistics_BackupMissing;
        }

        return "N/A";
    }

    /// <summary>
    /// Checks basic file integrity (zero-length detection).
    /// Returns "OK" for normal files, "Size=0" for empty files, or em-dash if not applicable.
    /// </summary>
    private static string DeriveIntegrity(string videoPath)
    {
        try
        {
            var fileInfo = new FileInfo(videoPath);
            if (!fileInfo.Exists)
                return "\u2014";
            return fileInfo.Length == 0 ? "Size=0" : "OK";
        }
        catch
        {
            return "\u2014";
        }
    }

    private static bool IsVideoCandidate(string filePath) =>
        FolderNameConstants.IsVideoCandidate(filePath,
            FolderNameConstants.VideoExclusion.CommonExclusions);
}
