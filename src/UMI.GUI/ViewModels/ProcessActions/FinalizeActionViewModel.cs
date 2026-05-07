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

using System.IO;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action that finalizes DaVinci Resolve exports:
/// finds exported videos in Video/postprocess/exported/, injects GPS data,
/// moves them to Video/, and cleans up temporary folders.
/// </summary>
public sealed class FinalizeActionViewModel : ProcessActionViewModel
{

    public override string Title => Strings.Finalize_Title;

    public override string Description => Strings.Finalize_Description;

    /// <summary>
    /// Checkmark-circle icon. 24×24 stroke-based WPF Path Data.
    /// Outer circle + bold check stroke.
    /// </summary>
    public override string IconPathData =>
        "M12,2 A10,10 0 1,1 12,22 A10,10 0 0,1 12,2 M7,12 L10,15 L17,8";

    private readonly IPostProcessingService _postProcessing;
    private readonly UmiConfig _config;

    public FinalizeActionViewModel(
        IPostProcessingService postProcessing,
        UmiConfig config)
    {
        _postProcessing = postProcessing;
        _config         = config;
    }

    protected override async Task ExecuteRunAsync(CancellationToken ct)
    {
        var workbench = _config.GlobalPaths.Workbench;
        var gpxSource = _config.GlobalPaths.GpxSource;

        SetOnUiThread(() =>
        {
            ProgressText = Strings.Finalize_Scanning;
            CurrentFile  = string.Empty;
        });

        var pairs = _postProcessing.FindExportedVideosForFinalize(
            workbench,
            date:   null,
            source: "ALL");

        if (pairs.Count == 0)
        {
            SetOnUiThread(() =>
            {
                StatusMessage = Strings.Finalize_NoExports;
                IsStatusError = false;
                Progress      = 1;
                ProgressText  = Strings.Common_Done;
            });
            return;
        }

        SetOnUiThread(() => ProgressText = string.Format(Strings.Finalize_FoundFiles, pairs.Count));

        var options = new PostProcessingOptions
        {
            Workbench = workbench,
            Source    = "ALL",
            Mode      = "manual",
            GpxSource = !string.IsNullOrWhiteSpace(gpxSource) ? gpxSource : null,
        };

        int finalized = 0;
        int failed    = 0;
        int total     = pairs.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();

            var batch = new List<(FileInfo Exported, FileInfo? Original)> { pairs[i] };
            var file  = pairs[i].Exported.Name;

            SetOnUiThread(() =>
            {
                CurrentFile  = file;
                Progress     = (double)i / total;
                ProgressText = string.Format(Strings.Finalize_FileProgress, i + 1, total);
            });

            var (batchFinalized, batchFailed) =
                await _postProcessing.FinalizeExportedVideosAsync(batch, options, ct);

            finalized += batchFinalized;
            failed    += batchFailed;
        }

        SetOnUiThread(() =>
        {
            Progress     = 1;
            ProgressText = string.Format(Strings.Common_FilesProcessed, total);
            CurrentFile  = string.Empty;

            if (failed > 0)
            {
                StatusMessage = string.Format(Strings.Finalize_ResultWithFailures, finalized, failed);
                IsStatusError = true;
            }
            else
            {
                StatusMessage = string.Format(Strings.Finalize_ResultSuccess, finalized);
                IsStatusError = false;
            }
        });
    }
}
