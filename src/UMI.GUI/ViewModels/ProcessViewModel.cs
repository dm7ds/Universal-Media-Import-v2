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
using System.ComponentModel;
using System.Linq;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.ViewModels.ProcessActions;
using UMI.Core.Models;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Process tab.
/// Owns the ordered list of ProcessActionViewModels and exposes aggregate state.
/// </summary>
public class ProcessViewModel : ViewModelBase
{

    /// <summary>Video pipeline actions: Stabilize, GPS Inject, Restore Metadata, Finalize.</summary>
    public ObservableCollection<ProcessActionViewModel> VideoActions { get; }

    /// <summary>Photo review actions (currently empty — populated in a later task).</summary>
    public ObservableCollection<ProcessActionViewModel> PhotoActions { get; }

    /// <summary>Tool actions: Sort by Date, Workbench Statistics.</summary>
    public ObservableCollection<ProcessActionViewModel> ToolActions { get; }

    /// <summary>
    /// All process actions in display order (VideoActions + PhotoActions + ToolActions).
    /// Computed once in the constructor and cached to avoid per-access allocation.
    /// </summary>
    public IReadOnlyList<ProcessActionViewModel> Actions { get; private set; } = Array.Empty<ProcessActionViewModel>();

    private bool _hasAnyRunning;
    /// <summary>True when at least one action is currently running. Cached to avoid per-access LINQ.</summary>
    public bool HasAnyRunning
    {
        get => _hasAnyRunning;
        private set => SetProperty(ref _hasAnyRunning, value);
    }

    /// <summary>Date filter for Process tab operations. Session-only.</summary>
    public DateFilterViewModel DateFilter { get; } = new();

    private int _selectedGroupTabIndex;
    /// <summary>Selected sub-tab index (0=Video, 1=Photo, 2=Tools).</summary>
    public int SelectedGroupTabIndex
    {
        get => _selectedGroupTabIndex;
        set
        {
            if (SetProperty(ref _selectedGroupTabIndex, value))
                OnPropertyChanged(nameof(HelpAnchor));
        }
    }

    /// <summary>Context-sensitive help anchor based on the selected sub-tab.</summary>
    public string HelpAnchor => _selectedGroupTabIndex switch
    {
        0 => Resources.Strings.Help_AnchorProcessTab,
        1 => Resources.Strings.Help_AnchorSequenceReviewer,
        2 => Resources.Strings.Help_AnchorProcessTab,
        _ => Resources.Strings.Help_AnchorProcessTab
    };

    /// <summary>
    /// Exposes the shared WorkbenchViewModel so ProcessTab.xaml can bind
    /// to WorkbenchViewModel.WorkbenchPath for the readonly workbench pill.
    /// </summary>
    public WorkbenchViewModel WorkbenchViewModel { get; }

    public ProcessViewModel(
        IPostProcessingService postProcessing,
        IMp4Parser mp4Parser,
        UmiConfig config,
        IGpuTaskQueue gpuTaskQueue,
        GpsService gpsService,
        MetadataService metadataService,
        WorkbenchViewModel workbench,
        IProcessHistoryService historyService,
        IBurstVisualizerService burstVisualizer,
        IReviewSidecarService reviewSidecar,
        ISequenceSidecarService sequenceSidecar,
        IThumbnailCacheService thumbnailCache,
        BurstProfileLoader burstProfileLoader,
        GlobalPaths globalPaths,
        IFolderSortService folderSortService,
        IConfigWriterService? configWriter = null,
        ConfigPathResolver? configPaths = null,
        // Muss bis hierher durchgereicht werden: das ist die einzige Stelle,
        // an der der Sequenz-Reviewer real erzeugt wird. Fehlt der Logger hier,
        // bleibt er im Reviewer null und das Fehler-Logging beim Aussortieren
        // ist wirkungslos (I-010, Audit F-01).
        Microsoft.Extensions.Logging.ILogger<SequenceReviewerViewModel>? reviewerLogger = null)
    {
        WorkbenchViewModel = workbench;

        VideoActions = new ObservableCollection<ProcessActionViewModel>
        {
            new StabilizeActionViewModel(postProcessing, mp4Parser, config, gpuTaskQueue, DateFilter, configPaths),
            new GpsInjectActionViewModel(gpsService, config, DateFilter, historyService),
            new RestoreMetadataActionViewModel(metadataService, historyService, config, DateFilter),
            new FinalizeActionViewModel(postProcessing, config),
        };

        PhotoActions = new ObservableCollection<ProcessActionViewModel>
        {
            new SequenceReviewerActionViewModel(burstVisualizer, reviewSidecar, sequenceSidecar, thumbnailCache, burstProfileLoader, globalPaths, configWriter!, reviewerLogger),
        };

        ToolActions = new ObservableCollection<ProcessActionViewModel>
        {
            new SortActionViewModel(folderSortService, config),
            new StatisticsActionViewModel(mp4Parser, historyService, config, DateFilter),
            new ThumbnailGenerateActionViewModel(thumbnailCache, config, DateFilter),
        };

        Actions = VideoActions.Concat(PhotoActions).Concat(ToolActions).ToList();

        foreach (var action in Actions)
            action.PropertyChanged += OnActionPropertyChanged;
    }

    private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessActionViewModel.IsRunning))
            HasAnyRunning = Actions.Any(a => a.IsRunning);
    }
}
