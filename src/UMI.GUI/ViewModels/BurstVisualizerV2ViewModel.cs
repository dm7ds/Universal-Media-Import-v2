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
/// ViewModel for the full-screen Burst Visualizer v2 panel.
/// Loads a photo folder (with thumbnails), evaluates the currently selected burst profile
/// against the loaded data, and displays results in a resizable thumbnail grid.
///
/// Key responsibilities:
///   • LoadFolderAsync  — one-time I/O via IBurstVisualizerService.LoadFolderAsync
///   • EvaluateAsync    — in-memory evaluation via IBurstVisualizerService.Evaluate (DRY: same service as VisualizerViewModel)
///   • RebuildGridItems — maps VisualizerResult back onto ThumbnailItemViewModels (SequenceIndex, GapSeconds, IsOrphaned)
///   • Selection mode   — multi-select for AutoPresetGenerator
///   • AcceptPreset     — saves generated preset via BurstProfileLoader and notifies BurstTabViewModel via callback
///
/// SelectedProfile change → debounced 300 ms → EvaluateAsync
/// (Debounce pattern copied from BurstProfileCardViewModel.OnRuleOrGroupingChanged)
/// </summary>
public class BurstVisualizerV2ViewModel : ViewModelBase, IDisposable
{
    private readonly IBurstVisualizerService _visualizerService;
    private readonly ISequenceSidecarService _sequenceSidecarService;
    private readonly AutoPresetGenerator _presetGenerator;
    private readonly BurstProfileLoader _profileLoader;
    private readonly Action<BurstProfile>? _onProfileAdded;
    private readonly Action<VisualizerData>? _onDataLoaded;
    private readonly Func<string, IProgress<ExifScanProgress>?, CancellationToken, Task<VisualizerData>> _loadFolderCached;

    private VisualizerData? _data;

    /// <summary>Index of the last clicked GridItem for Shift+Click range selection.</summary>
    private int _lastClickedIndex = -1;

    private string _folderPath = string.Empty;
    /// <summary>Path to the folder to load and visualize.</summary>
    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private string _loadStatus = string.Empty;
    /// <summary>Human-readable status for the load operation, e.g. "Loading 42/128…".</summary>
    public string LoadStatus
    {
        get => _loadStatus;
        private set => SetProperty(ref _loadStatus, value);
    }

    /// <summary>
    /// Shared reference to BurstTabViewModel.Profiles — NOT a copy.
    /// Set by BurstTabViewModel constructor so this VM sees profile adds/removes automatically.
    /// </summary>
    public ObservableCollection<BurstProfileCardViewModel> AvailableProfiles { get; }

    private BurstProfileCardViewModel? _selectedProfile;
    /// <summary>
    /// The profile whose rules are used for evaluation.
    /// Changing it triggers a debounced (300 ms) re-evaluation.
    /// Also subscribes to IsEditing=false transitions so that a Save in the inline
    /// Rule Editor triggers an immediate re-evaluation (the profile object itself
    /// does not change, so SelectedProfile's setter would not fire again).
    /// </summary>
    public BurstProfileCardViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
                OnSelectedProfileChanged();
        }
    }

    private double _thumbnailSize = 96.0;
    /// <summary>Thumbnail size in device-independent pixels. Bound to a Slider (range 64–256).</summary>
    public double ThumbnailSize
    {
        get => _thumbnailSize;
        set => SetProperty(ref _thumbnailSize, value);
    }

    /// <summary>Flat list of all photos — populated by LoadFolderAsync and annotated by RebuildGridItems.</summary>
    public ObservableCollection<ThumbnailItemViewModel> GridItems { get; } = new();

    private VisualizerResult? _result;
    public VisualizerResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                UpdateEvalSummary();
                OnPropertyChanged(nameof(ShowSelectionHint));
            }
        }
    }

    private string _evalSummary = string.Empty;
    /// <summary>Human-readable summary, e.g. "5 sequences, 42 matched, 3 orphaned".</summary>
    public string EvalSummary
    {
        get => _evalSummary;
        private set => SetProperty(ref _evalSummary, value);
    }

    private bool _isSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (SetProperty(ref _isSelectionMode, value))
            {
                if (!value) ClearAllSelections();
                OnPropertyChanged(nameof(ShowSelectionHint));
            }
        }
    }

    /// <summary>Number of currently selected grid items. Derived — updated via item callbacks.</summary>
    public int SelectedCount => GridItems.Count(x => x.IsSelected);

    /// <summary>
    /// True when photos are loaded, selection mode is off, and no profile evaluation result exists.
    /// Used to show a contextual hint pointing the user towards the "Select Photos" workflow.
    /// </summary>
    public bool ShowSelectionHint => GridItems.Count > 0 && !IsSelectionMode && Result == null;

    private GeneratedPresetResult? _generatedPreset;
    public GeneratedPresetResult? GeneratedPreset
    {
        get => _generatedPreset;
        private set => SetProperty(ref _generatedPreset, value);
    }

    private bool _isPresetPreviewVisible;
    public bool IsPresetPreviewVisible
    {
        get => _isPresetPreviewVisible;
        private set => SetProperty(ref _isPresetPreviewVisible, value);
    }

    public ICommand BrowseFolderCommand        { get; }
    public ICommand LoadFolderCommand          { get; }
    public ICommand GeneratePresetCommand      { get; }
    public ICommand AcceptPresetCommand        { get; }
    public ICommand DiscardPresetCommand       { get; }
    public ICommand ClearSelectionCommand      { get; }
    public ICommand ToggleSelectionModeCommand { get; }

    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _debounceCts;

    public BurstVisualizerV2ViewModel(
        IBurstVisualizerService visualizerService,
        ISequenceSidecarService sequenceSidecarService,
        AutoPresetGenerator presetGenerator,
        BurstProfileLoader profileLoader,
        ObservableCollection<BurstProfileCardViewModel> profiles,
        Func<string, IProgress<ExifScanProgress>?, CancellationToken, Task<VisualizerData>> loadFolderCached,
        Action<BurstProfile>? onProfileAdded = null,
        Action<VisualizerData>? onDataLoaded = null)
    {
        _visualizerService      = visualizerService;
        _sequenceSidecarService = sequenceSidecarService;
        _presetGenerator        = presetGenerator;
        _profileLoader          = profileLoader;
        _loadFolderCached       = loadFolderCached;
        _onProfileAdded         = onProfileAdded;
        _onDataLoaded           = onDataLoaded;
        AvailableProfiles       = profiles;

        BrowseFolderCommand        = new RelayCommand(ExecuteBrowseFolder);
        LoadFolderCommand          = new RelayCommand(ExecuteLoadFolder, () => !IsLoading && !string.IsNullOrWhiteSpace(FolderPath));
        GeneratePresetCommand      = new RelayCommand(ExecuteGeneratePreset, () => SelectedCount > 0);
        AcceptPresetCommand        = new RelayCommand(ExecuteAcceptPreset, () => GeneratedPreset != null);
        DiscardPresetCommand       = new RelayCommand(ExecuteDiscardPreset);
        ClearSelectionCommand      = new RelayCommand(ClearAllSelections);
        ToggleSelectionModeCommand = new RelayCommand(() => IsSelectionMode = !IsSelectionMode);
    }

    private void ExecuteBrowseFolder()
    {
        var folder = Helpers.DialogHelper.BrowseFolder(
            Strings.BurstViz_SelectFolder,
            string.IsNullOrWhiteSpace(FolderPath) ? null : FolderPath);

        if (folder is not null)
            FolderPath = folder;
    }

    private async void ExecuteLoadFolder()
    {
        if (string.IsNullOrWhiteSpace(FolderPath)) return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading  = true;
        LoadStatus = Strings.BurstViz_Loading;
        _data      = null;
        Result     = null;

        GridItems.Clear();
        OnPropertyChanged(nameof(ShowSelectionHint));

        try
        {
            var progress = new Progress<ExifScanProgress>(p =>
                LoadStatus = string.Format(Strings.BurstViz_LoadProgress, p.ScannedFiles, p.TotalFiles, p.CurrentFile));

            var data = await _loadFolderCached(FolderPath, progress, ct);

            if (ct.IsCancellationRequested) return;

            _data = data;

            _onDataLoaded?.Invoke(data);

            foreach (var photo in data.Photos)
            {
                var item = new ThumbnailItemViewModel(photo);

                item.OnClicked = OnItemClicked;

                item.OnSelectedChanged = () => OnPropertyChanged(nameof(SelectedCount));
                GridItems.Add(item);
            }

            LoadStatus = string.Format(Strings.BurstViz_Loaded, data.TotalCount);
            OnPropertyChanged(nameof(ShowSelectionHint));

            if (SelectedProfile != null)
                await EvaluateAsync();
        }
        catch (OperationCanceledException)
        {
            LoadStatus = Strings.BurstViz_LoadCancelled;
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

    private void ExecuteGeneratePreset()
    {
        var selectedPhotos = GridItems
            .Where(x => x.IsSelected && _data != null)
            .Select(x => _data!.Photos.FirstOrDefault(p => p.FileName == x.FileName))
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();

        if (selectedPhotos.Count == 0) return;

        HashSet<string>? fieldFilter = null;
        if (SelectedProfile?.AvailableExifFields is { Count: > 0 } fields)
            fieldFilter = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);

        GeneratedPreset        = _presetGenerator.GenerateFromSelection(selectedPhotos, fieldFilter: fieldFilter);
        IsPresetPreviewVisible = true;
    }

    private async void ExecuteAcceptPreset()
    {
        if (GeneratedPreset == null) return;

        var conditionGroup = new ConditionGroup
        {
            Operator   = "AND",
            Conditions = GeneratedPreset.SuggestedConditions
        };

        var grouping = GeneratedPreset.SuggestedGrouping;
        if (GeneratedPreset.SuggestedStableFields is { Count: > 0 })
            grouping.StableFields = GeneratedPreset.SuggestedStableFields;

        var profile = new BurstProfile
        {
            Name            = GenerateUniquePresetName(),
            Description     = Strings.BurstViz_AutoDescription,
            Priority        = 100,
            MatchConditions = conditionGroup,
            Grouping        = grouping
        };

        try
        {
            await _profileLoader.SaveProfileAsync(profile);
            _onProfileAdded?.Invoke(profile);

            IsPresetPreviewVisible = false;
            GeneratedPreset        = null;
            IsSelectionMode        = false;

            StatusMessage = string.Format(Strings.BurstViz_ProfileSaved, profile.Name);
            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            LoadStatus = string.Format(Strings.BurstViz_ErrorSavingProfile, ex.Message);
        }
    }

    private void ExecuteDiscardPreset()
    {
        IsPresetPreviewVisible = false;
        GeneratedPreset        = null;
    }

    /// <summary>
    /// Called by ThumbnailItemViewModel when clicked. Handles single toggle and Shift+Click range.
    /// </summary>
    public void OnItemClicked(ThumbnailItemViewModel item, bool shiftHeld)
    {
        var index = GridItems.IndexOf(item);
        if (index < 0) return;

        if (shiftHeld && _lastClickedIndex >= 0 && _lastClickedIndex != index)
        {
            int start = Math.Min(_lastClickedIndex, index);
            int end   = Math.Max(_lastClickedIndex, index);
            for (int i = start; i <= end; i++)
                GridItems[i].IsSelected = true;
        }
        else
        {
            item.IsSelected = !item.IsSelected;
        }

        _lastClickedIndex = index;
    }

    private void ClearAllSelections()
    {
        _lastClickedIndex = -1;

        foreach (var item in GridItems)
            item.IsSelected = false;

        OnPropertyChanged(nameof(SelectedCount));
    }

    private System.ComponentModel.PropertyChangedEventHandler? _profilePropertyChangedHandler;

    /// <summary>
    /// Called when SelectedProfile changes.
    ///   1. Unsubscribes from the previous profile's PropertyChanged.
    ///   2. Subscribes to the new profile's PropertyChanged to re-evaluate when
    ///      IsEditing transitions to false (i.e. after Save — the profile object
    ///      itself does not change so SelectedProfile's setter would not fire again).
    ///   3. Triggers the standard debounced evaluation.
    /// </summary>
    private async void OnSelectedProfileChanged()
    {
        SubscribeToProfilePropertyChanged();

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested && _data != null && SelectedProfile != null)
                await EvaluateAsync();
        }
        catch (TaskCanceledException) { }
    }

    private BurstProfileCardViewModel? _subscribedProfile;

    /// <summary>
    /// Maintains a single PropertyChanged subscription on the currently selected profile.
    /// Re-evaluates whenever IsEditing transitions to false (= Save completed).
    /// </summary>
    private void SubscribeToProfilePropertyChanged()
    {
        if (_subscribedProfile != null && _profilePropertyChangedHandler != null)
            _subscribedProfile.PropertyChanged -= _profilePropertyChangedHandler;

        _subscribedProfile = SelectedProfile;

        if (_subscribedProfile == null)
        {
            _profilePropertyChangedHandler = null;
            return;
        }

        _profilePropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(BurstProfileCardViewModel.IsEditing)
                && !_subscribedProfile!.IsEditing
                && _data != null)
            {
                _ = EvaluateAsync();
            }
        };

        _subscribedProfile.PropertyChanged += _profilePropertyChangedHandler;
    }

    /// <summary>
    /// Runs the profile evaluation in-memory against the loaded data.
    /// Uses IBurstVisualizerService.Evaluate (SSOT — no duplicated grouping logic).
    /// After evaluation, rebuilds grid item annotations via RebuildGridItems.
    /// </summary>
    private async Task EvaluateAsync()
    {
        if (_data == null || SelectedProfile == null) return;

        try
        {
            var builtProfile = SelectedProfile.BuildProfile();
            var conditions   = builtProfile.MatchConditions;
            var grouping     = builtProfile.Grouping;
            var capturedData = _data;
            var folderPath   = FolderPath;
            var profileName  = SelectedProfile.Name;

            var result = await Task.Run(() =>
                _visualizerService.Evaluate(capturedData, conditions, grouping));

            Result = result;
            RebuildGridItems(result);

            var photoByName = capturedData.Photos.ToDictionary(
                dp => dp.FileName, dp => dp, StringComparer.OrdinalIgnoreCase);

            string ResolveRelativePath(string fileName)
            {
                return photoByName.TryGetValue(fileName, out var vp) && vp.RelativePath != null
                    ? vp.RelativePath
                    : fileName;
            }

            var sidecar = new SequenceSidecar
            {
                ProfileName = profileName,
                EvaluatedAt = DateTime.Now,
                Sequences = result.Sequences.Select(s => new SequenceEntry
                {
                    Name  = s.SequenceName,
                    Files = s.Photos.Select(p => ResolveRelativePath(p.FileName)).ToList()
                }).ToList(),
                Orphans   = result.OrphanedMatches.Select(p => ResolveRelativePath(p.FileName)).ToList(),
                Unmatched = result.Unmatched.Select(p => ResolveRelativePath(p.FileName)).ToList()
            };
            await _sequenceSidecarService.SaveAsync(folderPath, sidecar);
        }
        catch (Exception ex)
        {
            LoadStatus = string.Format(Strings.BurstViz_EvaluationError, ex.Message);
        }
    }

    /// <summary>
    /// Maps <see cref="VisualizerResult"/> back onto the flat <see cref="GridItems"/> collection.
    ///
    /// Mapping rules (by FileName):
    ///   Sequences[i].Photos   → SequenceIndex = i, IsOrphaned = false, GapSeconds from result
    ///   OrphanedMatches        → SequenceIndex = null, IsOrphaned = true
    ///   Unmatched              → SequenceIndex = null, IsOrphaned = false
    /// </summary>
    private void RebuildGridItems(VisualizerResult result)
    {
        var lookup = GridItems.ToDictionary(x => x.FileName, StringComparer.OrdinalIgnoreCase);

        foreach (var item in GridItems)
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

    private void UpdateEvalSummary()
    {
        if (Result == null)
        {
            EvalSummary = string.Empty;
            return;
        }

        var sequences = Result.Sequences.Count;
        var matched   = Result.MatchedCount;
        var orphaned  = Result.OrphanedMatches.Count;

        EvalSummary = $"{sequences} sequences, {matched} matched"
            + (orphaned > 0 ? $", {orphaned} orphaned" : string.Empty);
    }

    private string GenerateUniquePresetName()
    {
        var existing = AvailableProfiles
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int i = 1;
        while (existing.Contains($"AutoPreset{i}")) i++;
        return $"AutoPreset{i}";
    }

    public void Dispose()
    {
        if (_subscribedProfile != null && _profilePropertyChangedHandler != null)
        {
            _subscribedProfile.PropertyChanged -= _profilePropertyChangedHandler;
            _subscribedProfile = null;
            _profilePropertyChangedHandler = null;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        CancelScheduledClear();
        GC.SuppressFinalize(this);
    }
}
