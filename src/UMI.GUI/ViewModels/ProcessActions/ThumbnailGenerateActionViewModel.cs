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
using System.Threading;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action that warms the thumbnail cache for all RAW files in the workbench.
/// Delegates entirely to <see cref="IThumbnailCacheService.WarmCacheAsync"/> — no own
/// thumbnail extraction logic.
/// </summary>
public sealed class ThumbnailGenerateActionViewModel : ProcessActionViewModel
{
    public override string Title => Strings.ThumbnailGenerate_Title;

    public override string Description => Strings.ThumbnailGenerate_Description;

    /// <summary>
    /// Photo/image icon. 24×24 stroke-based WPF Path Data.
    /// Represents a picture frame with a landscape scene (mountains + sun).
    /// </summary>
    public override string IconPathData =>
        "M3,5 H21 V19 H3 Z " +
        "M3,15 L8,10 L13,14 L16,11 L21,15 " +
        "M15,8 m-1,0 a1,1 0 1,0 2,0 a1,1 0 1,0 -2,0";

    private readonly IThumbnailCacheService _thumbnailCache;
    private readonly UmiConfig _config;
    private readonly DateFilterViewModel _dateFilter;

    public ThumbnailGenerateActionViewModel(
        IThumbnailCacheService thumbnailCache,
        UmiConfig config,
        DateFilterViewModel dateFilter)
    {
        _thumbnailCache = thumbnailCache;
        _config         = config;
        _dateFilter     = dateFilter;
    }

    protected override async Task ExecuteRunAsync(CancellationToken ct)
    {
        var workbench = _config.GlobalPaths.Workbench;

        if (string.IsNullOrEmpty(workbench) || !Directory.Exists(workbench))
        {
            SetOnUiThread(() =>
            {
                StatusMessage = string.Format(Strings.Common_ErrorFormat, Strings.ThumbnailGenerate_WorkbenchMissing);
                IsStatusError = true;
                Progress      = 0;
                ProgressText  = string.Empty;
            });
            return;
        }

        SetOnUiThread(() =>
        {
            ProgressText  = Strings.Common_Scanning;
            CurrentFile   = string.Empty;
            StatusMessage = null;
        });

        // Collect date-folder paths to process (top-level date dirs, or all subdirs when no filter)
        IEnumerable<string> folders;
        if (_dateFilter.HasDateFilter)
        {
            folders = Directory
                .GetDirectories(workbench, "*", SearchOption.AllDirectories)
                .Where(d =>
                {
                    var name = System.IO.Path.GetFileName(d);
                    return _dateFilter.MatchesDateFolder(name);
                });
        }
        else
        {
            // Warm whole workbench; WarmCacheAsync recurses internally
            folders = [workbench];
        }

        var totalProcessed = 0;
        var progress = new Progress<int>(count =>
        {
            totalProcessed = count;
            SetOnUiThread(() =>
            {
                ProgressText = string.Format(Strings.ThumbnailGenerate_Progress, count);
            });
        });

        foreach (var folder in folders)
        {
            ct.ThrowIfCancellationRequested();
            await _thumbnailCache.WarmCacheAsync(folder, progress, ct);
        }

        SetOnUiThread(() =>
        {
            Progress     = 1;
            ProgressText = Strings.Common_Done;
            StatusMessage = string.Format(Strings.ThumbnailGenerate_Complete, totalProcessed);
            IsStatusError = false;
            CurrentFile   = string.Empty;
        });
    }
}
