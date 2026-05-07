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

using System.Windows;
using UMI.Core;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.GUI.Resources;
using UMI.GUI.Views;
using UMI.GUI.ViewModels;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action that opens the fullscreen Sequence Reviewer window.
/// The reviewer lets users tag favorites, mark trash, and rate photos
/// from burst sequences using keyboard shortcuts and a filmstrip UI.
/// </summary>
public sealed class SequenceReviewerActionViewModel : ProcessActionViewModel
{
    public override string Title       => Strings.SequenceReviewer_Title;
    public override string Description => Strings.SequenceReviewer_Description;

    /// <summary>
    /// Viewfinder / film-strip icon. 24×24 stroke-based WPF Path Data.
    /// Frame rectangle with inner aperture circle and three filmstrip perforations on each side.
    /// </summary>
    public override string IconPathData =>
        "M3,5 H21 V19 H3 Z " +
        "M3,7 H1 V9 H3 Z M3,11 H1 V13 H3 Z M3,15 H1 V17 H3 Z " +
        "M21,7 H23 V9 H21 Z M21,11 H23 V13 H21 Z M21,15 H23 V17 H21 Z " +
        "M12,12 m-4,0 a4,4 0 1,0 8,0 a4,4 0 1,0 -8,0 " +
        "M12,12 m-1.5,0 a1.5,1.5 0 1,0 3,0 a1.5,1.5 0 1,0 -3,0";

    private readonly IBurstVisualizerService _visualizerService;
    private readonly IReviewSidecarService   _sidecarService;
    private readonly ISequenceSidecarService _sequenceSidecarService;
    private readonly IThumbnailCacheService  _thumbnailCache;
    private readonly BurstProfileLoader      _profileLoader;
    private readonly GlobalPaths             _globalPaths;
    private readonly IConfigWriterService    _configWriter;

    public SequenceReviewerActionViewModel(
        IBurstVisualizerService visualizerService,
        IReviewSidecarService   sidecarService,
        ISequenceSidecarService sequenceSidecarService,
        IThumbnailCacheService  thumbnailCache,
        BurstProfileLoader      profileLoader,
        GlobalPaths             globalPaths,
        IConfigWriterService    configWriter)
    {
        _visualizerService      = visualizerService;
        _sidecarService         = sidecarService;
        _sequenceSidecarService = sequenceSidecarService;
        _thumbnailCache         = thumbnailCache;
        _profileLoader          = profileLoader;
        _globalPaths            = globalPaths;
        _configWriter           = configWriter;
    }

    /// <summary>
    /// Opens the SequenceReviewerWindow on the UI thread.
    /// Automatically loads the workbench folder — no folder dialog needed.
    /// </summary>
    protected override Task ExecuteRunAsync(CancellationToken ct)
    {
        SetOnUiThread(() =>
        {
            var vm     = new SequenceReviewerViewModel(_visualizerService, _sidecarService, _sequenceSidecarService, _thumbnailCache, _profileLoader, _configWriter);
            var window = new SequenceReviewerWindow(vm, _globalPaths.Workbench);
            window.ShowDialog();

            Progress     = 1;
            ProgressText = string.Empty;
        });

        return Task.CompletedTask;
    }
}
