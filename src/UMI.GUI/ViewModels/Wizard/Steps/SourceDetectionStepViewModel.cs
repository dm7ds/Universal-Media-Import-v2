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
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Represents a single detected source (SD card drive or MTP device) in the SourceDetection step.
/// Shown as a selectable card. The best-confidence source is auto-recommended.
/// </summary>
public class SourceItem : ViewModelBase
{

    /// <summary>Drive letter for SD sources (e.g. "F:"). Empty for MTP sources.</summary>
    public string DriveLetter { get; init; } = string.Empty;

    /// <summary>Root path of the drive (e.g. "F:\"). Empty for MTP sources.</summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>MTP device identifier. Null for SD sources.</summary>
    public string? MtpDeviceId { get; init; }

    /// <summary>Volume label of the drive (e.g. "DJI_ACTION"). Null when not available or for MTP sources.</summary>
    public string? VolumeLabel { get; init; }

    /// <summary>Full display name: "F: — DJI_ACTION (32 GB)" or "DJI Osmo Action 5 Pro [MTP]".</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Human-readable detected camera name. Null when unknown.</summary>
    public string? DetectedCamera { get; init; }

    /// <summary>Detected camera type (e.g. "Action"). Null when unknown.</summary>
    public string? DetectedType { get; init; }

    /// <summary>
    /// Which stage produced the detection: "usb", "exif", "volume_label", "none".
    /// Used for ranking and badge display.
    /// </summary>
    public string? DetectionSource { get; init; }

    /// <summary>Human-readable detection method label for badge display (e.g. "via EXIF").</summary>
    public string? DetectionMethodLabel => DetectionSource switch
    {
        "usb"          => "via USB",
        "exif"         => "via EXIF",
        "volume_label" => "via Label",
        _              => null
    };

    /// <summary>Full CardDetectionResult from ICardDetectionService. Null for MTP sources.</summary>
    public CardDetectionResult? Result { get; init; }

    private bool _isRecommended;
    /// <summary>True for the auto-recommended best-match source (highlighted with accent border).</summary>
    public bool IsRecommended
    {
        get => _isRecommended;
        set => SetProperty(ref _isRecommended, value);
    }

    private bool _isSelected;
    /// <summary>True when the user has clicked this source (confirmed selection).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isAnalyzing;
    /// <summary>True while detection is running on this source.</summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set => SetProperty(ref _isAnalyzing, value);
    }

    /// <summary>
    /// Numeric confidence: usb=3, exif=2, volume_label=1, none=0.
    /// Higher = better recommendation.
    /// </summary>
    public int ConfidenceScore => DetectionSource switch
    {
        "usb"          => 3,
        "exif"         => 2,
        "volume_label" => 1,
        _              => 0
    };
}

/// <summary>
/// Step 3 — Source Detection.
/// Shows camera-related removable drives and MTP devices. Non-camera drives are filtered out
/// unless the user explicitly requests to show all. Runs camera detection on each source,
/// auto-recommends the best match, and lets the user select a source before advancing.
///
/// Replaces the old SdInsertStepViewModel (one-at-a-time detection).
///
/// DriveWatcher events come on WMI thread — always dispatch to UI thread!
/// </summary>
public class SourceDetectionStepViewModel : WizardStepViewModelBase, IDisposable
{
    private readonly WizardSession _session;
    private readonly IDriveWatcherService _driveWatcher;
    private readonly ICardDetectionService _cardDetection;
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Known camera volume label prefixes for pre-filter heuristic.
    /// If the label starts with any of these, the drive is likely camera-related.
    /// </summary>
    private static readonly string[] KnownCameraLabelPrefixes =
    [
        "DJI", "GoPro", "HERO", "GP_CARD", "GOPRO",
        "Insta360", "OSMO", "OsmoAction",
        "Garmin", "VIRB",
        "EOS", "NIKON", "LUMIX", "ILCE", "ZV-E",
        "MAVIC", "MINI", "PHANTOM", "INSPIRE", "AIR"
    ];

    public override string StepTitle => Strings.Wizard_SourceDetectionTitle;
    public override string StepDescription => Strings.Wizard_SourceDetectionDescription;

    private bool _driveEventsSubscribed;
    private CancellationTokenSource? _detectionCts;

    private readonly HashSet<string> _detectedDrives = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<(string DriveLetter, string RootPath, string? VolumeLabel, long TotalSizeBytes)> _allSeenDrives = new();

    /// <summary>All currently detected sources (SD drives + MTP devices) with detection results.</summary>
    public ObservableCollection<SourceItem> DetectedSources { get; } = new();

    private bool _isWaiting = true;
    /// <summary>True while no sources are detected (shows pulsing indicator).</summary>
    public bool IsWaiting
    {
        get => _isWaiting;
        private set => SetProperty(ref _isWaiting, value);
    }

    private bool _hasSources;
    /// <summary>True when at least one source is detected.</summary>
    public bool HasSources
    {
        get => _hasSources;
        private set => SetProperty(ref _hasSources, value);
    }

    private SourceItem? _recommendedSource;
    /// <summary>The auto-detected best-match source. Null until detection completes.</summary>
    public SourceItem? RecommendedSource
    {
        get => _recommendedSource;
        private set => SetProperty(ref _recommendedSource, value);
    }

    private SourceItem? _selectedSource;
    /// <summary>The source the user has currently selected (or the auto-recommended one).</summary>
    public SourceItem? SelectedSource
    {
        get => _selectedSource;
        private set
        {
            if (SetProperty(ref _selectedSource, value))
                RefreshSelectionState();
        }
    }

    private bool _showAllDrives;
    /// <summary>
    /// When true, ALL removable drives are shown (including non-camera ones).
    /// Toggled by the user via ShowAllDrivesCommand when their drive was filtered out.
    /// </summary>
    public bool ShowAllDrives
    {
        get => _showAllDrives;
        private set => SetProperty(ref _showAllDrives, value);
    }

    /// <summary>Confirm the currently selected source → writes to session, enables Next.</summary>
    public ICommand ConfirmSourceCommand { get; }

    /// <summary>Pick a different source from the list → re-runs detection on that source.</summary>
    public ICommand SelectSourceCommand { get; }

    /// <summary>Skip detection — user enters camera data manually in CameraConfirm.</summary>
    public ICommand ManualEntryCommand { get; }

    /// <summary>Show all removable drives (including non-camera ones) as a fallback.</summary>
    public ICommand ShowAllDrivesCommand { get; }

    public SourceDetectionStepViewModel(
        WizardSession session,
        IDriveWatcherService driveWatcher,
        ICardDetectionService cardDetection,
        Dispatcher? dispatcher = null)
    {
        _session = session;
        _driveWatcher = driveWatcher;
        _cardDetection = cardDetection;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

        ConfirmSourceCommand  = new RelayCommand(ExecuteConfirmSource, () => SelectedSource != null || RecommendedSource != null);
        SelectSourceCommand   = new RelayCommand<SourceItem>(ExecuteSelectSource);
        ManualEntryCommand    = new RelayCommand(ExecuteManualEntry);
        ShowAllDrivesCommand  = new RelayCommand(ExecuteShowAllDrives);
    }

    public override async Task OnEnterAsync(CancellationToken ct = default)
    {

        IsWaiting = true;
        HasSources = false;
        RecommendedSource = null;
        SelectedSource = null;
        ShowAllDrives = false;
        StatusMessage = Strings.Wizard_SourceConnectPrompt;
        IsValid = false;
        DetectedSources.Clear();
        _allSeenDrives.Clear();
        _detectedDrives.Clear();

        // Subscribe BEFORE StartWatching so the EmitInitialDriveArrivals burst inside
        // StartWatching is delivered to OnDriveArrived — that single code path now
        // handles both already-mounted drives and hot-plugged ones (dedup, filter,
        // AddDriveToList, kick off detection).
        if (!_driveEventsSubscribed)
        {
            _driveWatcher.DriveArrived  += OnDriveArrived;
            _driveWatcher.DriveRemoved  += OnDriveRemoved;
            _driveEventsSubscribed = true;
        }

        if (!_driveWatcher.IsWatching)
        {
            _driveWatcher.StartWatching();
        }
        else
        {
            // Re-entry into this step (Back → forward, or after a profile loop). The
            // watcher's already running so EmitInitialDriveArrivals won't fire again —
            // synthesize DriveArrived for the current set of removable drives so the
            // same OnDriveArrived path runs and the UI sees what's currently mounted.
            foreach (var drive in _driveWatcher.GetCurrentDrives())
            {
                OnDriveArrived(this, new DriveChangedEventArgs
                {
                    DriveLetter    = drive.DriveLetter,
                    RootPath       = drive.RootPath,
                    VolumeLabel    = drive.VolumeLabel,
                    TotalSizeBytes = drive.TotalSizeBytes
                });
            }
        }

        // OnDriveArrived already kicks off RunDetectionOnAllAsync per arrival, no
        // need to call it again here.
        await Task.CompletedTask;
    }

    public override Task OnLeaveAsync(CancellationToken ct = default)
    {
        UnsubscribeEvents();
        _detectionCts?.Cancel();
        return Task.CompletedTask;
    }

    private void OnDriveArrived(object? sender, DriveChangedEventArgs e)
    {

        _dispatcher.Invoke(() =>
        {

            if (_allSeenDrives.All(d => d.DriveLetter != e.DriveLetter))
                _allSeenDrives.Add((e.DriveLetter, e.RootPath, e.VolumeLabel, e.TotalSizeBytes));

            if (DetectedSources.All(s => s.DriveLetter != e.DriveLetter) &&
                (ShowAllDrives || IsCameraLikelyDrive(e.RootPath, e.VolumeLabel)))
            {
                AddDriveToList(e.DriveLetter, e.RootPath, e.VolumeLabel, e.TotalSizeBytes);
            }

            StatusMessage = string.Format(Strings.Wizard_SourceNewCardAnalysing, e.DriveLetter);
        });

        _detectionCts?.Cancel();
        _detectionCts = new CancellationTokenSource();
        var ct = _detectionCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await _dispatcher.InvokeAsync(async () =>
                    await RunDetectionOnAllAsync(ct));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _dispatcher.Invoke(() =>
                {
                    StatusMessage = string.Format(Strings.Wizard_SourceErrorAnalysing, ex.Message);
                    IsValid = true;
                });
            }
        }, ct);
    }

    private void OnDriveRemoved(object? sender, DriveChangedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {

            _detectedDrives.Remove(e.DriveLetter);

            var existing = DetectedSources.FirstOrDefault(s => s.DriveLetter == e.DriveLetter);
            if (existing != null)
                DetectedSources.Remove(existing);

            if (DetectedSources.Count == 0)
            {
                IsWaiting = true;
                HasSources = false;
                RecommendedSource = null;
                SelectedSource = null;
                IsValid = false;
                StatusMessage = Strings.Wizard_SourceConnectPrompt;
            }
            else
            {
                UpdateRecommendation();
            }
        });
    }

    private void AddDriveToList(string driveLetter, string rootPath, string? volumeLabel, long totalSizeBytes)
    {
        // Dedupe: callers used to live with two paths racing into this method
        // (StartWatching → EmitInitialDriveArrivals → OnDriveArrived AND a separate
        // GetCurrentDrives() loop in OnEnterAsync). The dedupe lives here so no caller
        // can double-add even if the surrounding flow is rearranged.
        if (DetectedSources.Any(s => s.DriveLetter == driveLetter)) return;

        var sizeInfo = WizardFormatHelpers.FormatSize(totalSizeBytes);
        var displayName = string.IsNullOrWhiteSpace(volumeLabel)
            ? $"{driveLetter} ({sizeInfo})"
            : $"{driveLetter} — {volumeLabel} ({sizeInfo})";

        var item = new SourceItem
        {
            DriveLetter   = driveLetter,
            RootPath      = rootPath,
            VolumeLabel   = volumeLabel,
            DisplayName   = displayName,
            DetectionSource = "none"
        };
        DetectedSources.Add(item);
        IsWaiting = false;
        HasSources = true;
    }

    /// <summary>
    /// Runs CardDetectionService on every SD drive in DetectedSources that has no result yet.
    /// After all detections complete, picks the best source as RecommendedSource.
    /// </summary>
    private async Task RunDetectionOnAllAsync(CancellationToken ct)
    {

        var sdSources = DetectedSources.Where(s => !string.IsNullOrEmpty(s.RootPath)).ToList();
        foreach (var source in sdSources)
        {
            ct.ThrowIfCancellationRequested();
            await DetectSingleSourceAsync(source, ct);
        }

        UpdateRecommendation();
    }

    private async Task DetectSingleSourceAsync(SourceItem placeholder, CancellationToken ct)
    {

        if (_detectedDrives.Contains(placeholder.DriveLetter))
            return;

        placeholder.IsAnalyzing = true;
        StatusMessage = string.Format(Strings.Wizard_SourceAnalysing, placeholder.DriveLetter);

        CardDetectionResult result;
        try
        {
            result = await _cardDetection.DetectCameraAsync(
                placeholder.DriveLetter,
                placeholder.RootPath,
                placeholder.VolumeLabel,
                ct);
        }
        finally
        {
            placeholder.IsAnalyzing = false;
        }

        _detectedDrives.Add(placeholder.DriveLetter);

        ct.ThrowIfCancellationRequested();

        var detectedCamera = result.SuggestedDisplayName
                          ?? result.DetectedModel
                          ?? (result.DetectionSource != "none" ? Strings.Wizard_UnknownCamera : null);

        var updatedItem = new SourceItem
        {
            DriveLetter     = placeholder.DriveLetter,
            RootPath        = placeholder.RootPath,
            VolumeLabel     = placeholder.VolumeLabel,
            DisplayName     = placeholder.DisplayName,
            DetectedCamera  = detectedCamera,
            DetectedType    = result.SuggestedCameraType,
            DetectionSource = result.DetectionSource,
            Result          = result,
            IsRecommended   = placeholder.IsRecommended,
            IsSelected      = placeholder.IsSelected
        };

        var idx = DetectedSources.IndexOf(placeholder);
        if (idx >= 0)
            DetectedSources[idx] = updatedItem;

        if (ReferenceEquals(_selectedSource, placeholder))
            SelectedSource = updatedItem;
        if (ReferenceEquals(_recommendedSource, placeholder))
            RecommendedSource = updatedItem;
    }

    private void UpdateRecommendation()
    {

        foreach (var s in DetectedSources)
            s.IsRecommended = false;

        var best = DetectedSources
            .OrderByDescending(s => s.ConfidenceScore)
            .ThenByDescending(s => Directory.Exists(Path.Combine(s.RootPath ?? "", "DCIM")))
            .FirstOrDefault();

        if (best is null)
        {
            StatusMessage = Strings.Wizard_SourceNoDetected;
            return;
        }

        best.IsRecommended = true;
        RecommendedSource = best;

        if (_selectedSource == null)
            SelectedSource = best;

        StatusMessage = best.DetectionSource switch
        {
            "usb"          => string.Format(Strings.Wizard_SourceDetectedUsb, best.DetectedCamera),
            "exif"         => string.Format(Strings.Wizard_SourceDetectedExif, best.DetectedCamera),
            "volume_label" => string.Format(Strings.Wizard_SourceDetectedVolume, best.DetectedCamera ?? Strings.Wizard_UnknownCamera),
            _              => Strings.Wizard_SourceDetectedUnknown
        };

        if (DetectedSources.Count == 1 && best.ConfidenceScore > 0)
        {
            WriteSourceToSession(best);
            IsValid = true;
        }
        else if (_selectedSource != null && _selectedSource.ConfidenceScore > 0)
        {

            WriteSourceToSession(_selectedSource);
            IsValid = true;
        }

        RelayCommand.RaiseCanExecuteChanged();
    }

    private void ExecuteConfirmSource()
    {
        var source = _selectedSource ?? _recommendedSource;
        if (source == null) return;

        WriteSourceToSession(source);
        IsValid = true;
    }

    private void ExecuteSelectSource(SourceItem? source)
    {
        if (source == null) return;

        foreach (var s in DetectedSources)
            s.IsSelected = false;

        source.IsSelected = true;
        SelectedSource = source;

        WriteSourceToSession(source);
        IsValid = true;

        if (source.Result == null && !string.IsNullOrEmpty(source.RootPath))
        {
            _detectionCts?.Cancel();
            _detectionCts = new CancellationTokenSource();
            var ct = _detectionCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _dispatcher.InvokeAsync(async () =>
                    {
                        await DetectSingleSourceAsync(source, ct);
                        UpdateRecommendation();
                    });
                }
                catch (OperationCanceledException) { }
            }, ct);
        }
        else
        {
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Writes the given source to the wizard session. Pure data write, no IsValid change.</summary>
    private void WriteSourceToSession(SourceItem source)
    {
        _session.DetectedFingerprint  = source.Result?.Fingerprint;
        _session.DetectedModel        = source.Result?.ModelMatch;
        _session.DetectedDriveLetter  = source.DriveLetter;
    }

    private void ExecuteManualEntry()
    {
        _detectionCts?.Cancel();
        _session.DetectedFingerprint = null;
        _session.DetectedModel = null;
        _session.DetectedDriveLetter = null;
        StatusMessage = Strings.Wizard_ManualEntry;
        IsValid = true;
    }

    private void ExecuteShowAllDrives()
    {
        ShowAllDrives = true;

        foreach (var (driveLetter, rootPath, volumeLabel, totalSizeBytes) in _allSeenDrives)
        {
            if (DetectedSources.All(s => s.DriveLetter != driveLetter))
                AddDriveToList(driveLetter, rootPath, volumeLabel, totalSizeBytes);
        }

        if (DetectedSources.Count > 0)
        {
            _detectionCts?.Cancel();
            _detectionCts = new CancellationTokenSource();
            var ct = _detectionCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _dispatcher.InvokeAsync(async () =>
                        await RunDetectionOnAllAsync(ct));
                }
                catch (OperationCanceledException) { }
            }, ct);
        }
    }

    private void RefreshSelectionState()
    {
        RelayCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Returns true if the drive is likely a camera source based on DCIM folder presence
    /// or a volume label matching known camera brand prefixes.
    /// Non-camera USB sticks and card readers are filtered out by default.
    /// </summary>
    private static bool IsCameraLikelyDrive(string rootPath, string? volumeLabel)
    {

        if (!string.IsNullOrEmpty(rootPath) &&
            Directory.Exists(Path.Combine(rootPath, "DCIM")))
            return true;

        if (!string.IsNullOrWhiteSpace(volumeLabel))
        {
            foreach (var prefix in KnownCameraLabelPrefixes)
            {
                if (volumeLabel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private void UnsubscribeEvents()
    {
        if (_driveEventsSubscribed)
        {
            _driveWatcher.DriveArrived  -= OnDriveArrived;
            _driveWatcher.DriveRemoved  -= OnDriveRemoved;
            _driveEventsSubscribed = false;
        }
    }

    public void Dispose()
    {
        UnsubscribeEvents();
        _detectionCts?.Cancel();
        _detectionCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
