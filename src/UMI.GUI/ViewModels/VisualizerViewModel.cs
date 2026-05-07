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

using System.Collections.ObjectModel;
using System.Windows.Input;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Burst Visualizer panel inside a BurstProfileCard.
/// Loads EXIF data from a folder once (I/O), then evaluates rules in-memory.
/// EvaluateAsync is called by BurstProfileCardViewModel (debounced, 300 ms) when rules change.
/// </summary>
public class VisualizerViewModel : ViewModelBase
{
    private readonly IBurstVisualizerService _visualizerService;
    private readonly Func<string, IProgress<ExifScanProgress>?, CancellationToken, Task<VisualizerData>> _loadFolderCached;

    private string _folderPath = string.Empty;
    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (SetProperty(ref _folderPath, value))
                OnPropertyChanged(nameof(CanLoad));
        }
    }

    private bool _isEditing;
    /// <summary>
    /// Propagated from BurstProfileCardViewModel. When false, Load is disabled.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
                OnPropertyChanged(nameof(CanLoad));
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            SetProperty(ref _isLoading, value);
            OnPropertyChanged(nameof(CanLoad));
        }
    }

    public bool CanLoad => IsEditing && !IsLoading && !string.IsNullOrWhiteSpace(FolderPath);

    private VisualizerData? _data;
    /// <summary>
    /// Loaded EXIF data — set after LoadFolderAsync completes.
    /// Null means no folder has been loaded yet.
    /// </summary>
    public VisualizerData? Data
    {
        get => _data;
        private set => SetProperty(ref _data, value);
    }

    private VisualizerResult? _result;
    /// <summary>Last evaluation result. Null before the first evaluation.</summary>
    public VisualizerResult? Result
    {
        get => _result;
        private set
        {
            SetProperty(ref _result, value);
            UpdateSummary();
        }
    }

    private bool _isDetailView;
    /// <summary>Toggle between compact and detail view in the XAML.</summary>
    public bool IsDetailView
    {
        get => _isDetailView;
        set => SetProperty(ref _isDetailView, value);
    }

    private bool _showThumbnails;
    /// <summary>
    /// When true, the Visualizer shows a thumbnail grid instead of the text list.
    /// Default: false (list view).
    /// </summary>
    public bool ShowThumbnails
    {
        get => _showThumbnails;
        set => SetProperty(ref _showThumbnails, value);
    }

    /// <summary>
    /// Flat list of all loaded photos with their burst-assignment annotations.
    /// Populated after LoadFolderAsync and re-annotated after every EvaluateAsync call.
    /// Reuses <see cref="ThumbnailItemViewModel"/> — no duplication.
    /// </summary>
    public ObservableCollection<ThumbnailItemViewModel> ThumbnailGridItems { get; } = new();

    private string _summary = string.Empty;
    /// <summary>Human-readable summary of the last evaluation result (e.g. "12 matched, 3 sequences, 5 unmatched").</summary>
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    private string _loadStatus = string.Empty;
    /// <summary>Status message for the load operation.</summary>
    public string LoadStatus
    {
        get => _loadStatus;
        private set => SetProperty(ref _loadStatus, value);
    }

    public ICommand LoadFolderCommand    { get; }
    public ICommand BrowseFolderCommand  { get; }
    public ICommand ToggleDetailCommand  { get; }
    public ICommand ToggleViewModeCommand { get; }

    private CancellationTokenSource? _loadCts;

    public VisualizerViewModel(
        IBurstVisualizerService visualizerService,
        Func<string, IProgress<ExifScanProgress>?, CancellationToken, Task<VisualizerData>> loadFolderCached)
    {
        _visualizerService = visualizerService;
        _loadFolderCached  = loadFolderCached;

        LoadFolderCommand     = new RelayCommand(ExecuteLoadFolder,   () => CanLoad);
        BrowseFolderCommand   = new RelayCommand(ExecuteBrowseFolder);
        ToggleDetailCommand   = new RelayCommand(() => IsDetailView = !IsDetailView);
        ToggleViewModeCommand = new RelayCommand(() => ShowThumbnails = !ShowThumbnails);
    }

    /// <summary>
    /// Injects externally loaded data (from shared cache) and triggers evaluation.
    /// Called by BurstTabViewModel when Burst Studio loads a folder.
    /// </summary>
    public async Task SetDataAndEvaluateAsync(VisualizerData data, ConditionGroup conditions, GroupingConfig grouping)
    {
        Data = data;
        FolderPath = "(auto-loaded)";
        LoadStatus = string.Format(Strings.Visualizer_LoadedShared, data.TotalCount);

        ThumbnailGridItems.Clear();
        foreach (var photo in data.Photos)
            ThumbnailGridItems.Add(new ThumbnailItemViewModel(photo));

        await EvaluateAsync(conditions, grouping);
    }

    /// <summary>
    /// Called by BurstProfileCardViewModel (debounced, 300 ms) when match-conditions or grouping change.
    /// Runs evaluation in-memory against the already-loaded Data.
    /// No-op when Data is null (folder not loaded yet).
    /// </summary>
    public async Task EvaluateAsync(ConditionGroup conditions, GroupingConfig grouping)
    {
        if (Data == null) return;

        try
        {

            var capturedData = Data;
            var result = await Task.Run(() =>
                _visualizerService.Evaluate(capturedData, conditions, grouping));

            Result = result;
            RebuildGridItems(result);
        }
        catch (Exception ex)
        {
            LoadStatus = string.Format(Strings.Visualizer_EvaluationError, ex.Message);
        }
    }

    /// <summary>
    /// Maps <see cref="VisualizerResult"/> back onto the flat <see cref="ThumbnailGridItems"/> collection.
    ///
    /// Mapping rules (by FileName):
    ///   Sequences[i].Photos   → SequenceIndex = i, IsOrphaned = false, GapSeconds from result
    ///   OrphanedMatches        → SequenceIndex = null, IsOrphaned = true
    ///   Unmatched              → SequenceIndex = null, IsOrphaned = false
    ///
    /// Pattern is identical to BurstVisualizerV2ViewModel.RebuildGridItems (SSOT: same service, same model).
    /// </summary>
    private void RebuildGridItems(VisualizerResult result)
    {

        var lookup = ThumbnailGridItems.ToDictionary(x => x.FileName, StringComparer.OrdinalIgnoreCase);

        foreach (var item in ThumbnailGridItems)
        {
            item.SequenceIndex = null;
            item.IsOrphaned    = false;
            item.GapSeconds    = null;
        }

        for (int i = 0; i < result.Sequences.Count; i++)
        {
            foreach (var photoResult in result.Sequences[i].Photos)
            {
                if (lookup.TryGetValue(photoResult.FileName, out var item))
                {
                    item.SequenceIndex = i;
                    item.IsOrphaned    = false;
                    item.GapSeconds    = photoResult.GapSeconds;
                }
            }
        }

        foreach (var photoResult in result.OrphanedMatches)
        {
            if (lookup.TryGetValue(photoResult.FileName, out var item))
            {
                item.SequenceIndex = null;
                item.IsOrphaned    = true;
                item.GapSeconds    = null;
            }
        }

    }

    private void ExecuteBrowseFolder()
    {
        var folder = Helpers.DialogHelper.BrowseFolder(Strings.Visualizer_SelectFolder);
        if (folder is not null)
            FolderPath = folder;
    }

    private async void ExecuteLoadFolder()
    {
        if (string.IsNullOrWhiteSpace(FolderPath)) return;

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading  = true;
        LoadStatus = Strings.Visualizer_LoadingExif;
        Data       = null;
        Result     = null;
        ThumbnailGridItems.Clear();

        try
        {
            var progress = new Progress<ExifScanProgress>(p =>
                LoadStatus = string.Format(Strings.Visualizer_LoadProgress, p.ScannedFiles, p.TotalFiles, p.CurrentFile));

            var data = await _loadFolderCached(FolderPath, progress, ct);

            if (ct.IsCancellationRequested) return;

            Data = data;

            foreach (var photo in data.Photos)
                ThumbnailGridItems.Add(new ThumbnailItemViewModel(photo));

            LoadStatus = string.Format(Strings.Visualizer_Loaded, data.TotalCount);
            Summary    = string.Empty;
        }
        catch (OperationCanceledException)
        {
            LoadStatus = Strings.Visualizer_LoadCancelled;
        }
        catch (Exception ex)
        {
            LoadStatus = string.Format(Strings.Common_ErrorFormat, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateSummary()
    {
        if (Result == null)
        {
            Summary = string.Empty;
            return;
        }

        var sequences = Result.Sequences.Count;
        var matched   = Result.MatchedCount;
        var unmatched = Result.UnmatchedCount;
        var orphaned  = Result.OrphanedMatches.Count;

        Summary = $"{matched} matched, {sequences} sequences, {unmatched} unmatched"
                + (orphaned > 0 ? $", {orphaned} orphaned" : string.Empty);
    }
}
