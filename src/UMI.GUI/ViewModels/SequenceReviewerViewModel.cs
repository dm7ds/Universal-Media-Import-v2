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

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Converters;
using UMI.GUI.Helpers;
using UMI.GUI.Resources;
using UMI.GUI.Views;

namespace UMI.GUI.ViewModels;

/// <summary>Filter mode for the Sequence Overview Grid.</summary>
public enum SequenceFilterMode { All, Rated, Unrated }

/// <summary>
/// ViewModel for the fullscreen Sequence Reviewer window.
/// Loads burst sequences via IBurstVisualizerService, persists tag/rating data
/// via IReviewSidecarService, and provides navigation, filtering and bulk
/// Copy/Move/Delete operations.
/// </summary>
public class SequenceReviewerViewModel : ViewModelBase
{
    private readonly IBurstVisualizerService _visualizerService;
    private readonly IReviewSidecarService   _sidecarService;
    private readonly ISequenceSidecarService _sequenceSidecarService;
    private readonly IThumbnailCacheService  _thumbnailCache;
    private readonly BurstProfileLoader      _profileLoader;
    private readonly IConfigWriterService?   _configWriter;

    private static readonly HashSet<string> _rawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr3", ".cr2", ".arw", ".nef", ".dng", ".orf", ".rw2", ".raf"
    };

    private VisualizerData? _cachedData;

    private readonly ConcurrentDictionary<string, byte[]> _lazyThumbnails = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _thumbGenCts;

    private bool _isEvaluating;

    private readonly Dictionary<string, int> _profileColorMap = new(StringComparer.OrdinalIgnoreCase);
    private int _nextProfileColorIndex;

    private string _folderPath = string.Empty;
    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    public ObservableCollection<ReviewSequenceViewModel> Sequences { get; } = new();

    private ReviewSequenceViewModel? _currentSequence;
    public ReviewSequenceViewModel? CurrentSequence
    {
        get => _currentSequence;
        set
        {
            if (SetProperty(ref _currentSequence, value))
                RebuildFilmstrip();
        }
    }

    private ReviewPhotoViewModel? _currentPhoto;
    public ReviewPhotoViewModel? CurrentPhoto
    {
        get => _currentPhoto;
        set
        {
            if (SetProperty(ref _currentPhoto, value))
            {
                UpdateCurrentMarkers();
                _ = LoadMainImageAsync();
                OnPropertyChanged(nameof(PhotoCounterText));
            }
        }
    }

    private int _currentPhotoIndex;
    public int CurrentPhotoIndex
    {
        get => _currentPhotoIndex;
        set => SetProperty(ref _currentPhotoIndex, value);
    }

    private int _currentSequenceIndex;
    public int CurrentSequenceIndex
    {
        get => _currentSequenceIndex;
        set => SetProperty(ref _currentSequenceIndex, value);
    }

    public ObservableCollection<ReviewPhotoViewModel> FilmstripPhotos { get; } = new();

    private BitmapSource? _mainImage;
    public BitmapSource? MainImage
    {
        get => _mainImage;
        private set => SetProperty(ref _mainImage, value);
    }

    private bool _isLoadingImage;
    public bool IsLoadingImage
    {
        get => _isLoadingImage;
        private set => SetProperty(ref _isLoadingImage, value);
    }

    public DateFilterViewModel DateFilter { get; } = new();

    /// <summary>
    /// Camera names for the dropdown. When more than one camera is found, this list
    /// is populated with all distinct camera names (no "all" sentinel — use SelectedCamera == null).
    /// </summary>
    public ObservableCollection<string> AvailableCameras { get; } = new();

    /// <summary>Sentinel value in AvailableCameras that represents "all cameras" (no filter).</summary>
    public const string AllCamerasSentinel = "";

    private string _selectedCamera = AllCamerasSentinel;
    /// <summary>
    /// Currently selected camera name.
    /// Empty string (AllCamerasSentinel) means "all cameras" — no filter applied.
    /// </summary>
    public string SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (SetProperty(ref _selectedCamera, value ?? AllCamerasSentinel))
                ApplyFilter();
        }
    }

    /// <summary>True when AvailableCameras contains more than one entry, driving dropdown visibility.</summary>
    public bool HasMultipleCameras => AvailableCameras.Count >= 1;

    /// <summary>
    /// Sentinel profile shown at the start of AvailableProfiles.
    /// When selected, merges sequences from ALL real profiles into one combined view.
    /// </summary>
    private static readonly BurstProfile AllSequencesProfile = new()
    {
        Name = Strings.SequenceReviewer_AllSequences,
        MatchConditions = new ConditionGroup { Conditions = new List<MatchCondition>() },
        Grouping = new GroupingConfig()
    };

    /// <summary>
    /// Sentinel profile shown at the end of AvailableProfiles.
    /// When selected, displays all photos not covered by ANY real profile's sequences.
    /// </summary>
    private static readonly BurstProfile UnassignedProfile = new()
    {
        Name = Strings.SequenceReviewer_Unmatched,
        MatchConditions = new ConditionGroup { Conditions = new List<MatchCondition>() },
        Grouping = new GroupingConfig()
    };

    /// <summary>All available burst profiles loaded from the preset directory.</summary>
    public ObservableCollection<BurstProfile> AvailableProfiles { get; } = new();

    private BurstProfile? _selectedProfile;
    /// <summary>
    /// The currently selected burst profile.
    /// Changing this property triggers a re-evaluation without re-scanning the folder.
    /// </summary>
    public BurstProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                // A deliberate user-driven profile change dismisses the empty-folder hint.
                EmptyHint = null;
                _ = ReevaluateAsync();
                OnPropertyChanged(nameof(SelectedProfileColorIndex));
            }
        }
    }

    /// <summary>Color index of the currently selected profile, for header display.</summary>
    public int? SelectedProfileColorIndex => SelectedProfile != null
        && !ReferenceEquals(SelectedProfile, AllSequencesProfile)
        && !ReferenceEquals(SelectedProfile, UnassignedProfile)
        ? GetProfileColorIndex(SelectedProfile)
        : null;

    private ReviewTag? _filterTag;
    /// <summary>null = all photos; Favorite / Trash = filtered subset.</summary>
    public ReviewTag? FilterTag
    {
        get => _filterTag;
        set
        {
            if (SetProperty(ref _filterTag, value))
            {
                OnPropertyChanged(nameof(IsFilterActive));
                if (value == ReviewTag.Trash)
                    IsDeleteEnabled = true;
                RebuildFilmstrip();
            }
        }
    }

    private int _filterRatingMin;
    public int FilterRatingMin
    {
        get => _filterRatingMin;
        set => SetProperty(ref _filterRatingMin, value);
    }

    private int _filterRatingMax = 5;
    public int FilterRatingMax
    {
        get => _filterRatingMax;
        set => SetProperty(ref _filterRatingMax, value);
    }

    public bool IsFilterActive => FilterTag.HasValue || FilterRatingMin > 0;

    private bool _isDeleteEnabled;
    public bool IsDeleteEnabled
    {
        get => _isDeleteEnabled;
        set => SetProperty(ref _isDeleteEnabled, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private string _loadingText = string.Empty;
    public string LoadingText
    {
        get => _loadingText;
        private set => SetProperty(ref _loadingText, value);
    }

    private string? _emptyHint;
    /// <summary>
    /// Informational banner text shown when no series were detected but photos are present,
    /// or when the folder contains no images at all. Null in normal operation.
    /// </summary>
    public string? EmptyHint
    {
        get => _emptyHint;
        private set
        {
            if (SetProperty(ref _emptyHint, value))
                OnPropertyChanged(nameof(HasEmptyHint));
        }
    }

    /// <summary>True when <see cref="EmptyHint"/> is non-null and non-empty; drives banner visibility.</summary>
    public bool HasEmptyHint => !string.IsNullOrEmpty(_emptyHint);

    public string SequenceCounterText
    {
        get
        {
            if (Sequences.Count == 0) return string.Empty;

            if (SequenceFilter == SequenceFilterMode.All)
                return string.Format(Strings.SequenceReviewer_SequenceOf, CurrentSequenceIndex + 1, Sequences.Count);

            var filtered = FilteredSequences;
            var posInFiltered = (filtered as IList<ReviewSequenceViewModel>)?.IndexOf(CurrentSequence!) + 1 ?? 1;
            if (posInFiltered <= 0) posInFiltered = 1;
            return $"{posInFiltered}/{filtered.Count} ({Sequences.Count})";
        }
    }

    public string PhotoCounterText
    {
        get
        {
            var photos = GetFilteredPhotos();
            if (photos.Count == 0) return string.Empty;
            var idx = photos.IndexOf(CurrentPhoto!);
            return string.Format(Strings.SequenceReviewer_PhotoOf, Math.Max(idx + 1, 1), photos.Count);
        }
    }

    public ICommand NavigateLeftCommand  { get; }
    public ICommand NavigateRightCommand { get; }
    public ICommand NavigateUpCommand    { get; }
    public ICommand NavigateDownCommand  { get; }

    public ICommand ToggleFavoriteCommand { get; }
    public ICommand ToggleTrashCommand    { get; }
    public ICommand SetRatingCommand      { get; }

    public ICommand ExportCommand  { get; }
    public ICommand DeleteCommand  { get; }
    public ICommand CloseCommand   { get; }

    public ICommand ApplyFilterCommand { get; }
    public ICommand ClearFilterCommand { get; }

    private bool _isOverviewMode = true;
    public bool IsOverviewMode
    {
        get => _isOverviewMode;
        set => SetProperty(ref _isOverviewMode, value);
    }

    private SequenceFilterMode _sequenceFilter = SequenceFilterMode.All;
    public SequenceFilterMode SequenceFilter
    {
        get => _sequenceFilter;
        set
        {
            if (SetProperty(ref _sequenceFilter, value))
                OnPropertyChanged(nameof(FilteredSequences));
        }
    }

    /// <summary>
    /// Filtered view of Sequences for the Overview Grid and filtered navigation.
    /// Applies SequenceFilterMode (Rated/Unrated/All) AND active tag/rating/date/camera filters.
    /// Sequences that have no matching photos after filtering are hidden.
    /// </summary>
    public IReadOnlyList<ReviewSequenceViewModel> FilteredSequences
    {
        get
        {
            IEnumerable<ReviewSequenceViewModel> seqs = SequenceFilter switch
            {
                SequenceFilterMode.Rated   => Sequences.Where(s => s.HasRatedPhotos),
                SequenceFilterMode.Unrated => Sequences.Where(s => !s.HasRatedPhotos),
                _                          => Sequences
            };

            if (FilterTag.HasValue || FilterRatingMin > 0 || DateFilter.HasDateFilter
                || !string.IsNullOrEmpty(SelectedCamera))
            {
                seqs = seqs.Where(s => s.Photos.Any(PhotoPassesFilters));
            }

            return seqs.ToList();
        }
    }

    private bool PhotoPassesFilters(ReviewPhotoViewModel p)
    {
        if (FilterTag.HasValue && p.Tag != FilterTag.Value) return false;
        if (FilterRatingMin > 0 && p.Rating < FilterRatingMin) return false;
        if (DateFilter.HasDateFilter && !PassesDateFilter(p)) return false;
        if (!string.IsNullOrEmpty(SelectedCamera)
            && !string.Equals(p.CameraName, SelectedCamera, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public ICommand EscapeCommand                    { get; }
    public ICommand ToggleOverviewCommand           { get; }
    public ICommand SetSequenceFilterCommand        { get; }
    public ICommand SelectSequenceFromOverviewCommand { get; }

    /// <summary>Raised when CloseCommand is executed to close the reviewer window.</summary>
    public event EventHandler? CloseRequested;

    public SequenceReviewerViewModel(
        IBurstVisualizerService visualizerService,
        IReviewSidecarService   sidecarService,
        ISequenceSidecarService sequenceSidecarService,
        IThumbnailCacheService  thumbnailCache,
        BurstProfileLoader      profileLoader,
        IConfigWriterService?   configWriter = null)
    {
        _visualizerService      = visualizerService;
        _sidecarService         = sidecarService;
        _sequenceSidecarService = sequenceSidecarService;
        _thumbnailCache         = thumbnailCache;
        _profileLoader          = profileLoader;
        _configWriter           = configWriter;

        NavigateLeftCommand  = new RelayCommand(NavigateLeft);
        NavigateRightCommand = new RelayCommand(NavigateRight);
        NavigateUpCommand    = new RelayCommand(NavigateUp);
        NavigateDownCommand  = new RelayCommand(NavigateDown);

        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);
        ToggleTrashCommand    = new RelayCommand(ToggleTrash);
        SetRatingCommand      = new RelayCommand<string>(SetRating);

        ExportCommand  = new RelayCommand(ExecuteExport,  () => Sequences.Count > 0);
        DeleteCommand  = new RelayCommand(ExecuteDelete,  () => IsDeleteEnabled && GetFilteredPhotos().Count > 0);
        CloseCommand   = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        EscapeCommand  = new RelayCommand(HandleEscape);

        ApplyFilterCommand = new RelayCommand(ApplyFilter);
        ClearFilterCommand = new RelayCommand(ClearFilter);

        ToggleOverviewCommand            = new RelayCommand(ToggleOverview);
        SetSequenceFilterCommand         = new RelayCommand<string>(SetSequenceFilter);
        SelectSequenceFromOverviewCommand = new RelayCommand<object>(SelectSequenceFromOverview);

        DateFilter.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DateFilterViewModel.DateFrom)
                               or nameof(DateFilterViewModel.DateTo)
                               or nameof(DateFilterViewModel.HasDateFilter))
                ApplyFilter();
        };
    }

    /// <summary>
    /// Loads a folder: scans for photos via IBurstVisualizerService (SSOT),
    /// loads available burst profiles, evaluates with the selected profile,
    /// merges stored sidecar reviews, then selects the first sequence.
    /// </summary>
    public async Task LoadFolderAsync(string folderPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        FolderPath = folderPath;
        IsLoading  = true;
        _thumbGenCts?.Cancel();
        _thumbGenCts?.Dispose();
        _thumbGenCts = null;
        Sequences.Clear();
        FilmstripPhotos.Clear();
        AvailableCameras.Clear();
        _selectedCamera = AllCamerasSentinel;
        _cachedData     = null;
        _lazyThumbnails.Clear();
        OnPropertyChanged(nameof(SelectedCamera));
        MainImage = null;

        try
        {
            var progress = new Progress<ExifScanProgress>(p =>
            {
                LoadingText = $"{p.ScannedFiles}/{p.TotalFiles}";
            });

            _cachedData = await _visualizerService.LoadFolderAsync(
                folderPath, progress, ct, loadThumbnails: false);

            AvailableProfiles.Clear();
            var profileNames = _profileLoader.ListAvailableProfiles();
            var profiles     = _profileLoader.LoadProfiles(profileNames);
            foreach (var p in profiles)
            {
                var probe = _visualizerService.Evaluate(_cachedData, p.MatchConditions, p.Grouping);
                if (probe.Sequences.Count > 0)
                    AvailableProfiles.Add(p);
            }

            AvailableProfiles.Add(UnassignedProfile);

            AvailableProfiles.Insert(0, AllSequencesProfile);

            var autoIdx = 0;
            foreach (var p in AvailableProfiles.Where(p =>
                !ReferenceEquals(p, AllSequencesProfile) && !ReferenceEquals(p, UnassignedProfile)))
            {
                if (p.ColorIndex < 0)
                    p.ColorIndex = autoIdx % 8;
                autoIdx++;
            }

            // Determine whether any real profiles (not sentinels) have sequences.
            var hasRealProfiles = AvailableProfiles.Any(p =>
                !ReferenceEquals(p, AllSequencesProfile) && !ReferenceEquals(p, UnassignedProfile));

            var existingSidecar = await _sequenceSidecarService.ReadAsync(folderPath, ct);

            if (existingSidecar != null)
            {
                var matchingProfile = AvailableProfiles.FirstOrDefault(p =>
                    !ReferenceEquals(p, UnassignedProfile) &&
                    !ReferenceEquals(p, AllSequencesProfile) &&
                    string.Equals(p.Name, existingSidecar.ProfileName, StringComparison.OrdinalIgnoreCase));

                _selectedProfile = AllSequencesProfile;
                OnPropertyChanged(nameof(SelectedProfile));

                await EvaluateAndRebuildAsync(ct);

                return;
            }

            if (!hasRealProfiles && _cachedData!.Photos.Count > 0)
            {
                // No series detected but photos are present — fall back to "Unassigned" so the
                // user sees their images instead of an empty screen. Show an informational banner.
                _selectedProfile = UnassignedProfile;
                EmptyHint = string.Format(Strings.SequenceReviewer_NoSeriesHint, _cachedData.Photos.Count);
            }
            else if (!hasRealProfiles)
            {
                // Folder is genuinely empty — no photos found at all.
                _selectedProfile = AllSequencesProfile;

                // I-005: Liegt der geladene Ordner selbst im .umi-Bereich, filtert
                // der Scan jede gefundene Datei wieder weg — der Ordner wirkt leer
                // obwohl Bilder darin liegen. Ohne diesen Hinweis (und ohne den
                // Pfad im Text) ist die Ursache fuer den User unsichtbar.
                EmptyHint = FolderNameConstants.IsInternalDirectory(folderPath)
                    ? string.Format(Strings.SequenceReviewer_InternalFolder, folderPath)
                    : string.Format(Strings.SequenceReviewer_NoPhotos, folderPath);
            }
            else
            {
                // Normal operation: at least one series detected.
                _selectedProfile = AllSequencesProfile;
                EmptyHint = null;
            }
            OnPropertyChanged(nameof(SelectedProfile));

            await EvaluateAndRebuildAsync(ct);
        }
        catch (Exception ex)
        {
            LoadingText = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;

            _thumbGenCts = new CancellationTokenSource();
            var initialPriority = CurrentSequence?.Photos
                .Select(p => p.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _ = GenerateMissingThumbnailsAsync(_thumbGenCts.Token, initialPriority);
        }
    }

    /// <summary>Returns the color index for the given profile. Uses persisted ColorIndex if set, otherwise auto-assigns.</summary>
    private int GetProfileColorIndex(BurstProfile profile)
    {
        if (profile.ColorIndex >= 0 && profile.ColorIndex < 8)
            return profile.ColorIndex;

        if (!_profileColorMap.TryGetValue(profile.Name, out var idx))
        {
            idx = _nextProfileColorIndex++ % 8;
            _profileColorMap[profile.Name] = idx;
        }
        return idx;
    }

    /// <summary>
    /// Re-evaluates the cached data with the currently selected profile and rebuilds the sequence list.
    /// Called when SelectedProfile changes — does NOT re-scan the folder.
    /// Writes an updated sidecar after evaluation so future opens skip live evaluation.
    /// </summary>
    private async Task ReevaluateAsync()
    {
        if (_cachedData is null) return;

        _thumbGenCts?.Cancel();
        _isEvaluating = true;

        IsLoading = true;
        try
        {
            await EvaluateAndRebuildAsync(CancellationToken.None);

            if (SelectedProfile != null && !ReferenceEquals(SelectedProfile, UnassignedProfile)
                && !ReferenceEquals(SelectedProfile, AllSequencesProfile)
                && !string.IsNullOrEmpty(FolderPath))
            {
                var data = _cachedData!;
                var result = _visualizerService.Evaluate(data,
                    SelectedProfile.MatchConditions, SelectedProfile.Grouping);

                var photoByName = new Dictionary<string, VisualizerPhoto>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in data.Photos)
                    photoByName.TryAdd(p.FileName, p);

                string ResolveRelativePath(string fileName)
                {
                    return photoByName.TryGetValue(fileName, out var vp) && vp.RelativePath != null
                        ? vp.RelativePath
                        : fileName;
                }

                var sidecarSeqIdx = 1;
                var sidecar = new SequenceSidecar
                {
                    ProfileName = SelectedProfile.Name,
                    EvaluatedAt = DateTime.Now,
                    Sequences = result.Sequences.Select(s => new SequenceEntry
                    {
                        Name  = $"{SelectedProfile.Name}_{sidecarSeqIdx++}",
                        Files = s.Photos.Select(p => ResolveRelativePath(p.FileName)).ToList()
                    }).ToList(),
                    Orphans   = result.OrphanedMatches.Select(p => ResolveRelativePath(p.FileName)).ToList(),
                    Unmatched = result.Unmatched.Select(p => ResolveRelativePath(p.FileName)).ToList()
                };
                await _sequenceSidecarService.SaveAsync(FolderPath, sidecar);
            }
        }
        finally
        {
            _isEvaluating = false;
            IsLoading = false;

            _thumbGenCts?.Dispose();
            _thumbGenCts = new CancellationTokenSource();
            var priority = CurrentSequence?.Photos
                .Select(p => p.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _ = GenerateMissingThumbnailsAsync(_thumbGenCts.Token, priority);
        }
    }

    /// <summary>
    /// Runs Evaluate on the cached data with the selected profile and rebuilds the Sequences collection.
    /// Must be called with _cachedData already set.
    /// </summary>
    private async Task EvaluateAndRebuildAsync(CancellationToken ct)
    {
        if (_cachedData is null) return;

        var data = _cachedData;

        var sidecar = await _sidecarService.ReadAsync(FolderPath, ct);
        var reviewLookup = new Dictionary<string, PhotoReview>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in sidecar?.Reviews ?? new List<PhotoReview>())
            reviewLookup.TryAdd(r.FileName, r);

        ReviewPhotoViewModel MakePhotoVmFromData(VisualizerPhoto dp)
        {
            var rel = dp.RelativePath ?? dp.FileName;
            var fullPath = Path.GetFullPath(Path.Combine(FolderPath, rel));
            var cameraName = ReviewPhotoViewModel.ExtractCameraName(rel);
            var thumbData = dp.ThumbnailData
                ?? (_lazyThumbnails.TryGetValue(fullPath, out var lazy) ? lazy : null);
            var vm = new ReviewPhotoViewModel(fullPath, thumbData, dp.CaptureTime, cameraName);
            if (reviewLookup.TryGetValue(dp.FileName, out var review))
            {
                vm.Tag    = review.Tag;
                vm.Rating = review.Rating;
            }
            return vm;
        }

        var vizPhotoLookup = new Dictionary<string, VisualizerPhoto>(StringComparer.OrdinalIgnoreCase);
        foreach (var dp in data.Photos.Where(dp => dp.RelativePath != null))
            vizPhotoLookup.TryAdd(dp.RelativePath!, dp);

        Sequences.Clear();
        FilmstripPhotos.Clear();
        AvailableCameras.Clear();
        _selectedCamera = AllCamerasSentinel;
        OnPropertyChanged(nameof(SelectedCamera));

        ReviewPhotoViewModel MakePhotoVm(VisualizerPhotoResult p)
        {
            var key = p.RelativePath ?? p.FileName;
            var vizPhoto = vizPhotoLookup.TryGetValue(key, out var vp) ? vp : null;
            if (vizPhoto != null)
                return MakePhotoVmFromData(vizPhoto);

            var rel = p.RelativePath ?? p.FileName;
            var fullPath = Path.GetFullPath(Path.Combine(FolderPath, rel));
            var cameraName = ReviewPhotoViewModel.ExtractCameraName(rel);
            var thumbData = _lazyThumbnails.TryGetValue(fullPath, out var lazy) ? lazy : null;
            return new ReviewPhotoViewModel(fullPath, thumbData, null, cameraName);
        }

        if (ReferenceEquals(SelectedProfile, AllSequencesProfile))
        {
            foreach (var profile in AvailableProfiles.Where(p =>
                !ReferenceEquals(p, UnassignedProfile) && !ReferenceEquals(p, AllSequencesProfile)))
            {
                var eval = _visualizerService.Evaluate(data, profile.MatchConditions, profile.Grouping);
                var seqIdx = 1;
                foreach (var seq in eval.Sequences)
                {
                    var photoVMs = seq.Photos.Select(MakePhotoVm)
                        .OrderBy(p => p.CaptureTime ?? DateTime.MaxValue).ThenBy(p => p.FileName).ToList();
                    var name = $"{profile.Name}_{seqIdx++}";
                    Sequences.Add(new ReviewSequenceViewModel(name, photoVMs)
                        { SourceProfileName = profile.Name, ProfileColorIndex = GetProfileColorIndex(profile) });
                }
            }
        }
        else if (ReferenceEquals(SelectedProfile, UnassignedProfile))
        {
            var coveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in AvailableProfiles.Where(p =>
                !ReferenceEquals(p, UnassignedProfile) && !ReferenceEquals(p, AllSequencesProfile)))
            {
                var eval = _visualizerService.Evaluate(data, profile.MatchConditions, profile.Grouping);
                foreach (var seq in eval.Sequences)
                    foreach (var photo in seq.Photos)
                    {
                        var vp = vizPhotoLookup.TryGetValue(photo.RelativePath ?? photo.FileName, out var v) ? v : null;
                        var rel = vp?.RelativePath ?? photo.RelativePath ?? photo.FileName;
                        coveredPaths.Add(Path.GetFullPath(Path.Combine(FolderPath, rel)));
                    }
            }

            var unassigned = data.Photos
                .Where(dp =>
                {
                    var rel = dp.RelativePath ?? dp.FileName;
                    return !coveredPaths.Contains(Path.GetFullPath(Path.Combine(FolderPath, rel)));
                })
                .Select(MakePhotoVmFromData)
                .OrderBy(p => p.CaptureTime ?? DateTime.MaxValue).ThenBy(p => p.FileName)
                .ToList();

            if (unassigned.Count > 0)
                Sequences.Add(new ReviewSequenceViewModel(Strings.SequenceReviewer_Unmatched, unassigned));
        }
        else
        {
            VisualizerResult result;
            if (SelectedProfile != null)
            {
                result = _visualizerService.Evaluate(data,
                    SelectedProfile.MatchConditions,
                    SelectedProfile.Grouping);
            }
            else
            {
                var matchAll = new ConditionGroup { Conditions = new List<MatchCondition>() };
                var grouping = new GroupingConfig { MaxGapSeconds = 3600 };
                result = _visualizerService.Evaluate(data, matchAll, grouping);
            }

            var seqIdx = 1;
            foreach (var seq in result.Sequences)
            {
                var photoVMs = seq.Photos.Select(MakePhotoVm)
                    .OrderBy(p => p.CaptureTime ?? DateTime.MaxValue).ThenBy(p => p.FileName).ToList();
                var name = $"{SelectedProfile?.Name ?? seq.SequenceName}_{seqIdx++}";
                var colorIdx = SelectedProfile != null ? GetProfileColorIndex(SelectedProfile) : (int?)null;
                Sequences.Add(new ReviewSequenceViewModel(name, photoVMs) { ProfileColorIndex = colorIdx });
            }
        }

        var distinctCameras = Sequences
            .SelectMany(s => s.Photos)
            .Select(p => p.CameraName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        if (distinctCameras.Count >= 1)
        {
            if (distinctCameras.Count > 1)
                AvailableCameras.Add(AllCamerasSentinel);
            foreach (var cam in distinctCameras)
                AvailableCameras.Add(cam!);
            _selectedCamera = distinctCameras.Count > 1 ? AllCamerasSentinel : distinctCameras[0]!;
            OnPropertyChanged(nameof(SelectedCamera));
        }

        foreach (var photo in Sequences.SelectMany(s => s.Photos))
        {
            if (photo.ThumbnailData == null
                && _lazyThumbnails.TryGetValue(photo.FullPath, out var cached))
                photo.SetThumbnailData(cached);
        }

        OnPropertyChanged(nameof(HasMultipleCameras));
        OnPropertyChanged(nameof(SequenceCounterText));
        OnPropertyChanged(nameof(FilteredSequences));
        OnPropertyChanged(nameof(SelectedProfileColorIndex));

        if (Sequences.Count > 0)
            SelectSequence(0);

        LoadingText = string.Empty;
    }

    /// <summary>
    /// Builds the Sequences collection from a persisted <see cref="SequenceSidecar"/> without
    /// running a live evaluation. Matches sidecar file names against <paramref name="data"/>.Photos
    /// so thumbnail data and capture times are available. Falls back to live evaluation if the
    /// sidecar is stale (i.e. photos are no longer present in the folder).
    /// </summary>
    private async Task BuildSequencesFromSidecarAsync(SequenceSidecar sidecar, VisualizerData data, CancellationToken ct)
    {
        var photoByName = new Dictionary<string, VisualizerPhoto>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in data.Photos)
            photoByName.TryAdd(p.FileName, p);
        var photoByRelPath = new Dictionary<string, VisualizerPhoto>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in data.Photos.Where(p => p.RelativePath != null))
            photoByRelPath.TryAdd(p.RelativePath!, p);

        var reviewSidecar = await _sidecarService.ReadAsync(FolderPath, ct);
        var reviewLookup = new Dictionary<string, PhotoReview>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in reviewSidecar?.Reviews ?? new List<PhotoReview>())
            reviewLookup.TryAdd(r.FileName, r);

        ReviewPhotoViewModel MakePhotoVmFromFileName(string fileEntry)
        {
            var vizPhoto = photoByRelPath.TryGetValue(fileEntry, out var vp1) ? vp1
                : photoByName.TryGetValue(fileEntry, out var vp2) ? vp2 : null;
            var relativePath = vizPhoto?.RelativePath ?? fileEntry;
            var fullPath   = Path.GetFullPath(Path.Combine(FolderPath, relativePath));
            var cameraName = ReviewPhotoViewModel.ExtractCameraName(relativePath);
            var thumbData = vizPhoto?.ThumbnailData
                ?? (_lazyThumbnails.TryGetValue(fullPath, out var lazy) ? lazy : null);
            var vm = new ReviewPhotoViewModel(fullPath, thumbData, vizPhoto?.CaptureTime, cameraName);

            var reviewKey = vizPhoto?.FileName ?? Path.GetFileName(fileEntry);
            if (reviewLookup.TryGetValue(reviewKey, out var review))
            {
                vm.Tag    = review.Tag;
                vm.Rating = review.Rating;
            }

            return vm;
        }

        Sequences.Clear();
        FilmstripPhotos.Clear();
        AvailableCameras.Clear();
        _selectedCamera = AllCamerasSentinel;
        OnPropertyChanged(nameof(SelectedCamera));

        foreach (var entry in sidecar.Sequences)
        {
            var photoVMs = entry.Files.Select(MakePhotoVmFromFileName)
                .OrderBy(p => p.CaptureTime ?? DateTime.MaxValue).ThenBy(p => p.FileName).ToList();
            var colorIdx = SelectedProfile != null ? GetProfileColorIndex(SelectedProfile) : (int?)null;
            Sequences.Add(new ReviewSequenceViewModel(entry.Name, photoVMs) { ProfileColorIndex = colorIdx });
        }

        var distinctCameras = Sequences
            .SelectMany(s => s.Photos)
            .Select(p => p.CameraName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        if (distinctCameras.Count >= 1)
        {
            if (distinctCameras.Count > 1)
                AvailableCameras.Add(AllCamerasSentinel);
            foreach (var cam in distinctCameras)
                AvailableCameras.Add(cam!);
            _selectedCamera = distinctCameras.Count > 1 ? AllCamerasSentinel : distinctCameras[0]!;
            OnPropertyChanged(nameof(SelectedCamera));
        }

        OnPropertyChanged(nameof(HasMultipleCameras));
        OnPropertyChanged(nameof(SequenceCounterText));
        OnPropertyChanged(nameof(FilteredSequences));

        if (Sequences.Count > 0)
            SelectSequence(0);
    }

    private void NavigateLeft()
    {
        var photos = GetFilteredPhotos();
        if (photos.Count == 0) return;

        var idx = CurrentPhoto is null ? 0 : photos.IndexOf(CurrentPhoto);
        idx = idx <= 0 ? photos.Count - 1 : idx - 1;
        SetCurrentPhoto(photos[idx]);
    }

    private void NavigateRight()
    {
        var photos = GetFilteredPhotos();
        if (photos.Count == 0) return;

        var idx = CurrentPhoto is null ? -1 : photos.IndexOf(CurrentPhoto);
        idx = (idx + 1) % photos.Count;
        SetCurrentPhoto(photos[idx]);
    }

    private void NavigateUp()
    {
        if (Sequences.Count == 0) return;
        var filtered = (IList<ReviewSequenceViewModel>)FilteredSequences;
        if (filtered.Count == 0) return;

        var currentFiltered = CurrentSequence is null ? 0 : filtered.IndexOf(CurrentSequence);
        if (currentFiltered < 0) currentFiltered = 0;

        var prevIdx = currentFiltered <= 0 ? filtered.Count - 1 : currentFiltered - 1;
        var target  = filtered[prevIdx];
        SelectSequence(Sequences.IndexOf(target));
    }

    private void NavigateDown()
    {
        if (Sequences.Count == 0) return;
        var filtered = (IList<ReviewSequenceViewModel>)FilteredSequences;
        if (filtered.Count == 0) return;

        var currentFiltered = CurrentSequence is null ? -1 : filtered.IndexOf(CurrentSequence);
        if (currentFiltered < 0) currentFiltered = -1;

        var nextIdx = (currentFiltered + 1) % filtered.Count;
        var target  = filtered[nextIdx];
        SelectSequence(Sequences.IndexOf(target));
    }

    private void SelectSequence(int index)
    {
        if (index < 0 || index >= Sequences.Count) return;

        if (_currentSequence != null)
            _currentSequence.IsCurrentInOverview = false;
        foreach (var s in Sequences)
            s.IsCurrentInOverview = false;

        CurrentSequenceIndex = index;
        _currentSequence = null;
        CurrentSequence = Sequences[index];
        Sequences[index].IsCurrentInOverview = true;
        OnPropertyChanged(nameof(SequenceCounterText));

        var photos = GetFilteredPhotos();
        if (photos.Count > 0)
            SetCurrentPhoto(photos[0]);
        else
            CurrentPhoto = null;

        if (!_isEvaluating)
            RestartBackgroundGenWithPriority();
    }

    /// <summary>
    /// Cancels the running background thumbnail generator and restarts it
    /// with the current sequence's photos processed first.
    /// No-op if background gen hasn't started yet (initial load).
    /// </summary>
    private void RestartBackgroundGenWithPriority()
    {
        if (_cachedData == null || _thumbGenCts == null) return;

        _thumbGenCts.Cancel();
        _thumbGenCts.Dispose();
        _thumbGenCts = new CancellationTokenSource();

        var priorityPaths = CurrentSequence?.Photos
            .Select(p => p.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _ = GenerateMissingThumbnailsAsync(_thumbGenCts.Token, priorityPaths);
    }

    private void SetCurrentPhoto(ReviewPhotoViewModel photo)
    {
        CurrentPhotoIndex = GetFilteredPhotos().IndexOf(photo);
        CurrentPhoto      = photo;
    }

    private void RebuildFilmstrip()
    {
        FilmstripPhotos.Clear();
        foreach (var p in GetFilteredPhotos())
            FilmstripPhotos.Add(p);

        UpdateCurrentMarkers();
        OnPropertyChanged(nameof(PhotoCounterText));
    }

    private void UpdateCurrentMarkers()
    {
        foreach (var p in FilmstripPhotos)
            p.IsCurrent = ReferenceEquals(p, CurrentPhoto);
    }

    private List<ReviewPhotoViewModel> GetFilteredPhotos()
    {
        var hasTagOrRatingFilter = FilterTag.HasValue || FilterRatingMin > 0;

        IEnumerable<ReviewPhotoViewModel> photos;
        if (hasTagOrRatingFilter)
        {
            // Tag/Rating filter: aggregate across ALL sequences in the current profile
            photos = Sequences.SelectMany(s => s.Photos);
        }
        else
        {
            if (CurrentSequence is null) return new List<ReviewPhotoViewModel>();
            photos = CurrentSequence.Photos;
        }

        if (FilterTag.HasValue)
            photos = photos.Where(p => p.Tag == FilterTag.Value);

        if (FilterRatingMin > 0)
            photos = photos.Where(p => p.Rating >= FilterRatingMin);

        if (DateFilter.HasDateFilter)
            photos = photos.Where(PassesDateFilter);

        if (!string.IsNullOrEmpty(SelectedCamera))
            photos = photos.Where(p => string.Equals(p.CameraName, SelectedCamera, StringComparison.OrdinalIgnoreCase));

        return photos.ToList();
    }

    private bool PassesDateFilter(ReviewPhotoViewModel photo)
    {
        if (!photo.CaptureTime.HasValue) return true;

        var localTime = photo.CaptureTime.Value.Kind == DateTimeKind.Utc
            ? photo.CaptureTime.Value.ToLocalTime()
            : photo.CaptureTime.Value;

        if (DateFilter.DateFrom.HasValue && localTime < DateFilter.DateFrom.Value)
            return false;
        if (DateFilter.DateTo.HasValue && localTime > DateFilter.DateTo.Value)
            return false;

        return true;
    }

    private void ApplyFilter()
    {
        RebuildFilmstrip();

        var hasAnyFilter = FilterTag.HasValue || FilterRatingMin > 0 || DateFilter.HasDateFilter
            || !string.IsNullOrEmpty(SelectedCamera);
        Func<ReviewPhotoViewModel, bool>? pred = hasAnyFilter ? PhotoPassesFilters : null;
        foreach (var s in Sequences)
            s.SetFilterPredicate(pred);

        OnPropertyChanged(nameof(FilteredSequences));
        var photos = GetFilteredPhotos();

        if (photos.Count > 0)
            SetCurrentPhoto(photos[0]);
        else
            CurrentPhoto = null;
    }

    private void ClearFilter()
    {
        _filterTag       = null;
        _filterRatingMin = 0;
        _filterRatingMax = 5;
        _isDeleteEnabled = false;
        _selectedCamera  = AllCamerasSentinel;

        OnPropertyChanged(nameof(FilterTag));
        OnPropertyChanged(nameof(FilterRatingMin));
        OnPropertyChanged(nameof(FilterRatingMax));
        OnPropertyChanged(nameof(IsDeleteEnabled));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(SelectedCamera));

        foreach (var s in Sequences)
            s.SetFilterPredicate(null);

        RebuildFilmstrip();
        OnPropertyChanged(nameof(FilteredSequences));
        var photos = GetFilteredPhotos();
        if (photos.Count > 0 && CurrentPhoto is null)
            SetCurrentPhoto(photos[0]);
    }

    private void HandleEscape()
    {
        if (!IsOverviewMode)
        {
            ToggleOverview();
            return;
        }
    }

    private void ToggleOverview()
    {
        if (!IsOverviewMode)
        {
            foreach (var s in Sequences)
                s.RefreshStatus();

            foreach (var s in Sequences)
                s.IsCurrentInOverview = ReferenceEquals(s, CurrentSequence);

            OnPropertyChanged(nameof(FilteredSequences));
        }
        IsOverviewMode = !IsOverviewMode;
    }

    private void SetSequenceFilter(string? param)
    {
        if (Enum.TryParse<SequenceFilterMode>(param, out var mode))
            SequenceFilter = mode;
    }

    private void SelectSequenceFromOverview(object? param)
    {
        if (param is ReviewSequenceViewModel seq)
        {
            var idx = Sequences.IndexOf(seq);
            if (idx >= 0)
            {
                IsOverviewMode = false;
                SelectSequence(idx);
            }
        }
    }

    private void ToggleFavorite()
    {
        if (CurrentPhoto is null) return;
        CurrentPhoto.Tag = CurrentPhoto.Tag == ReviewTag.Favorite ? ReviewTag.None : ReviewTag.Favorite;
        PersistCurrentPhoto();
    }

    private void ToggleTrash()
    {
        if (CurrentPhoto is null) return;
        CurrentPhoto.Tag = CurrentPhoto.Tag == ReviewTag.Trash ? ReviewTag.None : ReviewTag.Trash;
        PersistCurrentPhoto();
    }

    private void SetRating(string? param)
    {
        if (CurrentPhoto is null) return;
        if (!int.TryParse(param, out var stars)) return;

        CurrentPhoto.Rating = CurrentPhoto.Rating == stars ? 0 : stars;
        PersistCurrentPhoto();
    }

    private void PersistCurrentPhoto()
    {
        if (CurrentPhoto is null || string.IsNullOrEmpty(FolderPath)) return;
        var photo = CurrentPhoto;
        _ = _sidecarService.UpdatePhotoAsync(FolderPath, photo.FileName,
            tag: photo.Tag, rating: photo.Rating);
    }

    private CancellationTokenSource? _imageCts;

    private async Task LoadMainImageAsync()
    {
        _imageCts?.Cancel();
        _imageCts?.Dispose();
        _imageCts = new CancellationTokenSource();
        var ct    = _imageCts.Token;

        var photo = CurrentPhoto;
        if (photo is null)
        {
            MainImage = null;
            return;
        }

        MainImage = photo.Thumbnail;

        IsLoadingImage = true;

        try
        {
            var previewBytes = await _thumbnailCache.GetPreviewAsync(photo.FullPath, ct: ct)
                .ConfigureAwait(false);

            if (!ct.IsCancellationRequested && previewBytes is { Length: >= 2 }
                && previewBytes[0] == 0xFF && previewBytes[1] == 0xD8)
            {
                var previewImage = BytesToBitmapConverter.DecodeThumbnail(previewBytes, decodePixelWidth: 1920);
                if (previewImage is not null)
                    MainImage = previewImage;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsLoadingImage = false;
        }
    }

    /// <summary>
    /// Encodes an already-loaded BitmapSource as a small JPEG thumbnail.
    /// The source is already Frozen (thread-safe), so this works on any thread.
    /// </summary>
    private static byte[]? EncodeThumbnail(BitmapSource source)
    {
        try
        {
            var scale = 256.0 / source.PixelWidth;
            var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            scaled.Freeze();

            var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
            encoder.Frames.Add(BitmapFrame.Create(scaled));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Background-generates JPEG thumbnails + previews for all RAW photos without cached images.
    /// Two-phase pipeline (both parallel):
    ///   Phase 1 — Camera-rendered extraction (MetadataExtractor + ExifTool, ~100ms/file)
    ///   Phase 2 — WPF RAW decode (2-5s/file, only HEIF CR3 where Phase 1 fails)
    /// Updates filmstrip and main image live. Caches to disk so next open is instant.
    /// </summary>
    private async Task GenerateMissingThumbnailsAsync(CancellationToken ct, IReadOnlySet<string>? priorityPaths = null)
    {
        if (_cachedData == null) return;

        var rawPhotos = _cachedData.Photos
            .Where(p => _rawExtensions.Contains(Path.GetExtension(p.FileName)))
            .ToList();

        if (rawPhotos.Count == 0) return;

        if (priorityPaths is { Count: > 0 })
        {
            rawPhotos = rawPhotos
                .OrderByDescending(p => priorityPaths.Contains(
                    Path.GetFullPath(Path.Combine(FolderPath, p.RelativePath ?? p.FileName))))
                .ToList();
        }

        var needsWpfDecode = new ConcurrentBag<(string fullPath, bool needsThumb, bool needsPreview)>();

        try
        {
            await Parallel.ForEachAsync(
                rawPhotos,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (vizPhoto, token) =>
                {
                    var relativePath = vizPhoto.RelativePath ?? vizPhoto.FileName;
                    var fullPath = Path.GetFullPath(Path.Combine(FolderPath, relativePath));

                    var needsThumb = vizPhoto.ThumbnailData == null && !_lazyThumbnails.ContainsKey(fullPath);
                    var needsPreview = !_thumbnailCache.HasCachedPreview(fullPath);

                    if (!needsThumb && !needsPreview) return;

                    try
                    {
                        if (needsThumb)
                        {
                            var thumbBytes = await _thumbnailCache.GetThumbnailAsync(fullPath, token)
                                .ConfigureAwait(false);
                            if (thumbBytes != null)
                            {
                                _lazyThumbnails[fullPath] = thumbBytes;
                                needsThumb = false;

                                Application.Current?.Dispatcher?.BeginInvoke(() =>
                                {
                                    foreach (var vm in Sequences
                                        .SelectMany(s => s.Photos)
                                        .Where(p => string.Equals(p.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
                                        vm.SetThumbnailData(thumbBytes);
                                });
                            }
                        }

                        if (needsPreview)
                        {
                            var exifPreview = await _thumbnailCache.GetPreviewAsync(fullPath, ct: token)
                                .ConfigureAwait(false);
                            if (exifPreview != null)
                            {
                                needsPreview = false;

                                if (string.Equals(CurrentPhoto?.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                                    Application.Current?.Dispatcher?.BeginInvoke(() => _ = LoadMainImageAsync());
                            }
                        }

                        if (needsThumb || needsPreview)
                            needsWpfDecode.Add((fullPath, needsThumb, needsPreview));
                    }
                    catch (OperationCanceledException) { /* stop */ }
                    catch { /* skip this file */ }
                });
        }
        catch (OperationCanceledException) { /* cancelled — expected */ }

        if (needsWpfDecode.IsEmpty || ct.IsCancellationRequested) return;

        var phase2List = needsWpfDecode.ToList();
        if (priorityPaths is { Count: > 0 })
        {
            phase2List = phase2List
                .OrderByDescending(x => priorityPaths.Contains(x.fullPath))
                .ToList();
        }

        var maxParallel = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

        try
        {
            await Parallel.ForEachAsync(
                phase2List,
                new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = ct },
                async (item, token) =>
                {
                    try
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

                        var result = await Task.Run(() =>
                        {
                            timeoutCts.Token.ThrowIfCancellationRequested();

                            var img = LoadRawAtSize(item.fullPath, decodePixelHeight: 1080);
                            if (img == null) return (thumb: (byte[]?)null, preview: (byte[]?)null);

                            var preview = item.needsPreview ? EncodeJpeg(img, quality: 85) : null;
                            var thumb = item.needsThumb ? EncodeThumbnail(img) : null;

                            return (thumb, preview);
                        }, timeoutCts.Token);

                        if (token.IsCancellationRequested) return;

                        if (result.thumb != null)
                        {
                            _lazyThumbnails[item.fullPath] = result.thumb;
                            _ = _thumbnailCache.SaveThumbnailAsync(item.fullPath, result.thumb, CancellationToken.None);

                            Application.Current?.Dispatcher?.BeginInvoke(() =>
                            {
                                var vm = Sequences
                                    .SelectMany(s => s.Photos)
                                    .FirstOrDefault(p => string.Equals(p.FullPath, item.fullPath, StringComparison.OrdinalIgnoreCase));
                                vm?.SetThumbnailData(result.thumb);
                            });
                        }

                        if (result.preview != null)
                        {
                            _ = _thumbnailCache.SavePreviewAsync(item.fullPath, result.preview, CancellationToken.None);

                            if (string.Equals(CurrentPhoto?.FullPath, item.fullPath, StringComparison.OrdinalIgnoreCase))
                                Application.Current?.Dispatcher?.BeginInvoke(() => _ = LoadMainImageAsync());
                        }
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                    }
                    catch (OperationCanceledException) { /* cancellation — stop */ }
                    catch { /* skip this file */ }
                });
        }
        catch (OperationCanceledException) { /* cancelled — expected on sequence change */ }
    }

    /// <summary>
    /// Loads a RAW file at a given height via WPF Raw Image Extension codec.
    /// </summary>
    private static BitmapSource? LoadRawAtSize(string path, int decodePixelHeight)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource         = new Uri(path, UriKind.Absolute);
            image.CacheOption       = BitmapCacheOption.OnLoad;
            image.DecodePixelHeight = decodePixelHeight;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Encodes a BitmapSource as JPEG at the given quality. Source must be Frozen.
    /// </summary>
    private static byte[]? EncodeJpeg(BitmapSource source, int quality)
    {
        try
        {
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Opens the Export Dialog and, on confirmation, executes the export across ALL sequences.
    /// After a Move export, the reviewer refreshes by removing missing photos and empty sequences.
    /// Persists the chosen export path to AppSettings.LastExportPath via IConfigWriterService.
    /// </summary>
    private void ExecuteExport()
    {
        var lastPath = _configWriter?.Config.AppSettings.LastExportPath;
        var allSequences = Sequences.ToList();

        var dialogVm = new ExportDialogViewModel(allSequences, lastPath);
        var dialog   = new ExportDialog(dialogVm)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true) return;

        var exportPath     = dialogVm.ExportPath;
        var isMove         = dialogVm.IsMove;
        var keepSequences  = dialogVm.KeepSequences;
        var tagFilter      = dialogVm.SelectedTagFilter;
        var minRating      = dialogVm.MinRating;

        var allPhotosForExport = CollectPhotosForExport(allSequences, tagFilter, minRating);
        if (allPhotosForExport.Count == 0) return;

        var multiCamera = allPhotosForExport
            .Select(x => x.Photo.CameraName)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() > 1;

        var errors = 0;
        var exported = 0;

        foreach (var (photo, sequenceName) in allPhotosForExport)
        {
            var targetDir = BuildExportTargetDir(
                exportPath, photo, sequenceName, multiCamera, keepSequences);

            try
            {
                Directory.CreateDirectory(targetDir);
                var dest = Path.Combine(targetDir, photo.FileName);

                if (File.Exists(dest))
                    continue;

                if (isMove)
                    File.Move(photo.FullPath, dest);
                else
                    File.Copy(photo.FullPath, dest);

                exported++;
            }
            catch
            {
                errors++;
            }
        }

        PersistLastExportPath(exportPath);

        // Show export summary
        var message = string.Format(Strings.SequenceReviewer_ExportDone, exported, exportPath);
        if (errors > 0)
            message += Environment.NewLine + string.Format(Strings.SequenceReviewer_ExportErrors, errors);
        MessageBox.Show(message, Strings.SequenceReviewer_ExportTitle, MessageBoxButton.OK, MessageBoxImage.Information);

        if (isMove)
            RefreshAfterMove();
    }

    /// <summary>
    /// Collects all photos across all sequences that pass the tag and rating filters.
    /// Each entry pairs the photo with the name of its parent sequence (for folder structure).
    /// </summary>
    private static List<(ReviewPhotoViewModel Photo, string SequenceName)> CollectPhotosForExport(
        IEnumerable<ReviewSequenceViewModel> sequences,
        ExportTagFilter tagFilter,
        int minRating)
    {
        var result = new List<(ReviewPhotoViewModel, string)>();
        foreach (var seq in sequences)
        {
            foreach (var photo in seq.Photos)
            {
                if (!PassesExportTagFilter(photo, tagFilter)) continue;
                if (minRating > 0 && photo.Rating < minRating) continue;
                result.Add((photo, seq.SequenceName));
            }
        }
        return result;
    }

    private static bool PassesExportTagFilter(ReviewPhotoViewModel photo, ExportTagFilter filter) =>
        filter switch
        {
            ExportTagFilter.FavoritesOnly => photo.Tag == ReviewTag.Favorite,
            ExportTagFilter.UntaggedOnly  => photo.Tag == ReviewTag.None,
            ExportTagFilter.AllExclTrash  => photo.Tag != ReviewTag.Trash,
            _                             => false
        };

    /// <summary>
    /// Builds the target directory path for a single photo.
    /// Structure: {exportPath}/{date}/[{camera}/][{sequenceName}/]
    /// Camera folder is added only when more than one distinct camera is in the export set.
    /// Sequence folder is added only when keepSequences is true.
    /// Date and camera are extracted via FolderNameConstants.ExtractDateAndCamera (SSOT).
    /// </summary>
    private static string BuildExportTargetDir(
        string exportPath,
        ReviewPhotoViewModel photo,
        string sequenceName,
        bool multiCamera,
        bool keepSequences)
    {
        var (date, cameraId) = FolderNameConstants.ExtractDateAndCamera(photo.FullPath);
        var dateFolder   = date ?? "unknown-date";
        var cameraFolder = (multiCamera && !string.IsNullOrEmpty(cameraId)) ? cameraId : null;

        var parts = new List<string> { exportPath, dateFolder };
        if (cameraFolder is not null) parts.Add(cameraFolder);
        if (keepSequences && !string.IsNullOrWhiteSpace(sequenceName)) parts.Add(sequenceName);

        return Path.Combine(parts.ToArray());
    }

    /// <summary>
    /// Saves the used export path to AppSettings.LastExportPath via IConfigWriterService.
    /// No-op when configWriter is not injected.
    /// </summary>
    private void PersistLastExportPath(string path)
    {
        if (_configWriter is null) return;
        _configWriter.UpdateSection<AppSettings>("app_settings", s => s.LastExportPath = path);
        _ = _configWriter.SaveAsync();
    }

    /// <summary>
    /// After a Move export: removes photos that no longer exist on disk from all sequences,
    /// removes sequences that became empty, then rebuilds filmstrip and selection state.
    /// </summary>
    private void RefreshAfterMove()
    {
        foreach (var seq in Sequences.ToList())
        {
            var missing = seq.Photos.Where(p => !File.Exists(p.FullPath)).ToList();
            foreach (var p in missing)
            {
                seq.Photos.Remove(p);
                _ = _sidecarService.UpdatePhotoAsync(FolderPath, p.FileName,
                    tag: ReviewTag.None, rating: 0);
            }
        }

        var emptySequences = Sequences.Where(s => s.Photos.Count == 0).ToList();
        foreach (var empty in emptySequences)
            Sequences.Remove(empty);

        if (CurrentSequence is not null && !Sequences.Contains(CurrentSequence))
        {
            var next = Sequences.FirstOrDefault();
            if (next is not null)
                SelectSequence(Sequences.IndexOf(next));
            else
            {
                _currentSequence = null;
                CurrentPhoto     = null;
                OnPropertyChanged(nameof(CurrentSequence));
            }
        }

        OnPropertyChanged(nameof(FilteredSequences));
        OnPropertyChanged(nameof(SequenceCounterText));
        RebuildFilmstrip();

        if (FilmstripPhotos.Count > 0)
            SetCurrentPhoto(FilmstripPhotos[0]);
        else
            CurrentPhoto = null;
    }

    private void ExecuteDelete()
    {
        if (!IsDeleteEnabled) return;

        var photos = GetFilteredPhotos().ToList();
        foreach (var p in photos)
        {
            try
            {
                File.Delete(p.FullPath);
                CurrentSequence?.Photos.Remove(p);
                _ = _sidecarService.UpdatePhotoAsync(FolderPath, p.FileName,
                    tag: ReviewTag.None, rating: 0);
            }
            catch { /* skip */ }
        }

        RebuildFilmstrip();
        if (FilmstripPhotos.Count > 0)
            SetCurrentPhoto(FilmstripPhotos[0]);
        else
            CurrentPhoto = null;
    }
}
