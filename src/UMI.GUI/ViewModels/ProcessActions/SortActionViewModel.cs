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
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action that sorts the workbench by EXIF capture date into
/// <c>yyyy-MM-dd/Camera/Type/</c> folders. With the "Detect Photo Series"
/// toggle enabled, retroactive burst-detection groups consecutive photos
/// into <c>{Mode}_HHmmss</c> sub-folders inside the photo target dir —
/// same logic as the import-time burst detection, but running on an
/// already-imported workbench.
/// </summary>
public sealed class SortActionViewModel : ProcessActionViewModel
{

    public override string Title => Strings.Sort_Title;

    public override string Description => Strings.Sort_Description;

    /// <summary>
    /// Calendar icon. 24×24 stroke-based WPF Path Data.
    /// Calendar outline with header bar and date grid rows.
    /// </summary>
    public override string IconPathData =>
        "M6,2 V4 H4 A2,2 0 0,0 2,6 V20 A2,2 0 0,0 4,22 H20 A2,2 0 0,0 22,20 V6 A2,2 0 0,0 20,4 H18 V2 H16 V4 H8 V2 Z " +
        "M2,10 H22 " +
        "M7,15 H11 M13,15 H17";

    // Default ON: after-import "Organisieren" should detect photo series out of
    // the box — that's the whole point of running it post-import (Dirk 2026-06-02).
    private readonly ActionToggle _burstToggle = new(Strings.Sort_ToggleBurst, defaultValue: true);

    public override IReadOnlyList<ActionToggle> Toggles => [_burstToggle];

    private readonly IFolderSortService _sortService;
    private readonly UmiConfig _config;

    public SortActionViewModel(IFolderSortService sortService, UmiConfig config)
    {
        _sortService = sortService;
        _config      = config;
    }

    protected override async Task ExecuteRunAsync(CancellationToken ct)
    {
        var workbench = _config.GlobalPaths.Workbench;

        SetOnUiThread(() =>
        {
            ProgressText  = Strings.Sort_StatusScanning;
            CurrentFile   = string.Empty;
            StatusMessage = null;
            Progress      = 0;
        });

        var detectBursts = _burstToggle.IsChecked;

        // The IFolderSortService reports progress for the scan + sort phases
        // separately. We mirror its Phase string into ProgressText and use
        // Current/Total for the progress bar.
        var progress = new Progress<FolderSortProgress>(p =>
        {
            SetOnUiThread(() =>
            {
                if (p.Total > 0) Progress = (double)p.Current / p.Total;
                ProgressText = p.Phase switch
                {
                    FolderSortPhase.Scanning => Strings.Sort_StatusScanning,
                    FolderSortPhase.Sorting  => Strings.Sort_StatusSorting,
                    _                         => p.Phase,
                };
                CurrentFile = p.CurrentFile;
            });
        });

        var request = new FolderSortRequest(
            Workbench:     workbench,
            Mode:          FolderSortMode.Full,
            Config:        _config,
            DetectBursts:  detectBursts,
            DryRun:        false);

        var result = await _sortService.SortAsync(request, progress, ct);

        SetOnUiThread(() =>
        {
            Progress     = 1;
            ProgressText = string.Empty;
            CurrentFile  = string.Empty;

            if (result.Moved == 0 && result.Skipped == 0 && result.Errors == 0)
            {
                StatusMessage = Strings.Sort_NoMediaFiles;
                IsStatusError = false;
                return;
            }

            var parts = new List<string>
            {
                string.Format(Strings.Sort_ResultMoved, result.Moved, result.DateFolders),
            };

            if (detectBursts && result.DetectedSequences > 0)
                parts.Add(string.Format(Strings.Sort_ResultSequences, result.DetectedSequences));

            if (result.Skipped > 0)
                parts.Add(string.Format(Strings.Sort_ResultSkipped, result.Skipped));

            if (result.Errors > 0)
                parts.Add(string.Format(Strings.Sort_ResultErrors, result.Errors));

            StatusMessage = string.Join(", ", parts);
            IsStatusError = result.Errors > 0;

            foreach (var warning in result.Warnings)
            {
                ResultItems.Add(new ActionResultItem
                {
                    Prefix    = string.Empty,
                    FileName  = string.Empty,
                    Status    = warning,
                    IsSuccess = false,
                });
            }
        });
    }
}
