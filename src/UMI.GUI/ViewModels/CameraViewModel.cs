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
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Microsoft.Win32;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.GUI.Helpers;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Import phases shown on the camera card during an active import.
/// </summary>
public enum ImportPhase
{
    Idle,
    Scanning,
    Copying,
    PreProcessing,
    PostProcessing,
    Done,
    Error,
    Cancelled,
}

/// <summary>
/// ViewModel for a single camera card. Wraps CameraConfig and exposes
/// display-friendly properties for the UI. Supports editing with dirty tracking.
/// Cards expand inline to show details — no separate detail panel.
///
/// Feature bubble system (SSOT):
///   - Feature availability/placement comes from CameraTypeDefinition.Features (preset .umi file)
///   - Feature keys map to Edit-properties via GetEditFlag / SetEditFlag (one switch each)
///   - Feature labels/badge-keys come from FeatureLabels.Get(key)
///   - BuildBubbleLists() has NO hardcoded feature lists — iterates the preset dict
/// </summary>
public class CameraViewModel : ViewModelBase
{
    private readonly CameraConfig _config;
    private readonly CameraTypeLoader? _typeLoader;
    private readonly ConfigPathResolver? _configPaths;

    /// <summary>
    /// Called immediately after a feature bubble is toggled so the host can persist
    /// the new flag to config.json without waiting for an explicit save action.
    /// Set by the host (MainViewModel) after construction.
    /// Fire-and-forget is safe: SaveAsync is protected by a SemaphoreSlim.
    /// </summary>
    public Func<Task>? OnFeatureSaveRequested { get; set; }

    /// <summary>
    /// Returns the current app mode (true = Advanced, false = Simple).
    /// Set by the host (MainViewModel) after construction so CameraViewModel
    /// does not depend directly on MainViewModel.
    /// Used in BuildBubbleLists() to filter Settings→Cam bubbles in Simple Mode.
    /// </summary>
    public Func<bool>? GetIsAdvancedMode { get; set; }

    /// <summary>Delegate to check if the app is in Dau (Easy) mode. Set by MainViewModel.</summary>
    public Func<bool>? GetIsDauMode { get; set; }

    public CameraViewModel(string cameraId, CameraConfig config, CameraTypeLoader? typeLoader = null, ConfigPathResolver? configPaths = null)
    {
        CameraId = cameraId;
        _config = config;
        _typeLoader = typeLoader;
        _configPaths = configPaths;

        if (_config.SimpleOnCardOverrides.Count > 0)
            _config.SimpleOnCardOverrides.Clear();

        _editName             = config.Name;
        _editFolderName       = config.FolderName ?? string.Empty;
        _editCameraType       = config.CameraType;
        _editEnabled          = config.Enabled;
        _editVideoExtensions  = FormatExtensions(config.FileTypes.Video);
        _editPhotoExtensions  = FormatExtensions(config.FileTypes.Photo);
        _editGyroflowPreset   = config.PostProcessing?.Gyroflow?.Preset;

        _editGps           = config.Features.GpsInjection;
        _editGyroflow      = config.Features.Gyroflow;
        _editBurstDetection = config.Features.BurstDetection;
        _editMetadataBackup = config.Features.MetadataBackup;
        _editEisDetection  = config.Features.EisDetection;
        _editLensCorrection = config.Features.LensCorrection;
        _editPostProcess   = config.Features.PostProcess;
        _editRenameVideos  = config.Features.RenameVideos;
        _editGoProRename   = config.Features.GoProRename;

        _isEditing = false;

        ToggleGpsCommand         = new RelayCommand(() => SetEditFlag(FeatureKeys.GpsInjection,   !GetEditFlag(FeatureKeys.GpsInjection)));
        ToggleGyroflowCommand    = new RelayCommand(() => SetEditFlag(FeatureKeys.Gyroflow,       !GetEditFlag(FeatureKeys.Gyroflow)));
        ToggleBurstCommand       = new RelayCommand(() => SetEditFlag(FeatureKeys.BurstDetection, !GetEditFlag(FeatureKeys.BurstDetection)));
        ToggleMetadataCommand    = new RelayCommand(() => SetEditFlag(FeatureKeys.MetadataBackup, !GetEditFlag(FeatureKeys.MetadataBackup)));
        ToggleEisCommand         = new RelayCommand(() => SetEditFlag(FeatureKeys.EisDetection,   !GetEditFlag(FeatureKeys.EisDetection)));
        ToggleLensCommand        = new RelayCommand(() => SetEditFlag(FeatureKeys.LensCorrection, !GetEditFlag(FeatureKeys.LensCorrection)));
        TogglePostProcessCommand  = new RelayCommand(() => SetEditFlag(FeatureKeys.PostProcess,   !GetEditFlag(FeatureKeys.PostProcess)));
        ToggleRenameVideosCommand = new RelayCommand(() => SetEditFlag(FeatureKeys.RenameVideos,  !GetEditFlag(FeatureKeys.RenameVideos)));
        ToggleGoProRenameCommand  = new RelayCommand(() => SetEditFlag(FeatureKeys.GoProRename,   !GetEditFlag(FeatureKeys.GoProRename)));

        ToggleGpsSimpleOnCardCommand          = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.GpsInjection,   !GetSimpleOnCard(FeatureKeys.GpsInjection)));
        ToggleGyroflowSimpleOnCardCommand     = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.Gyroflow,       !GetSimpleOnCard(FeatureKeys.Gyroflow)));
        ToggleBurstSimpleOnCardCommand        = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.BurstDetection, !GetSimpleOnCard(FeatureKeys.BurstDetection)));
        ToggleMetadataSimpleOnCardCommand     = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.MetadataBackup, !GetSimpleOnCard(FeatureKeys.MetadataBackup)));
        ToggleEisSimpleOnCardCommand          = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.EisDetection,   !GetSimpleOnCard(FeatureKeys.EisDetection)));
        ToggleLensSimpleOnCardCommand         = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.LensCorrection, !GetSimpleOnCard(FeatureKeys.LensCorrection)));
        TogglePostProcessSimpleOnCardCommand  = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.PostProcess,    !GetSimpleOnCard(FeatureKeys.PostProcess)));
        ToggleRenameVideosSimpleOnCardCommand = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.RenameVideos,   !GetSimpleOnCard(FeatureKeys.RenameVideos)));
        ToggleGoProRenameSimpleOnCardCommand  = new RelayCommand(() => SetSimpleOnCard(FeatureKeys.GoProRename,    !GetSimpleOnCard(FeatureKeys.GoProRename)));

        StartEditCommand = new RelayCommand(() =>
        {

            _nameBeforeEdit        = EditName;
            _folderNameBeforeEdit  = EditFolderName;
            _cameraTypeBeforeEdit  = EditCameraType;
            _enabledBeforeEdit     = EditEnabled;
            _gpsBeforeEdit         = EditGps;
            _gyroflowBeforeEdit    = EditGyroflow;
            _burstBeforeEdit       = EditBurstDetection;
            _metadataBeforeEdit    = EditMetadataBackup;
            _eisBeforeEdit         = EditEisDetection;
            _lensBeforeEdit        = EditLensCorrection;
            _postProcessBeforeEdit = EditPostProcess;
            _renameVideosBeforeEdit = EditRenameVideos;
            _goProRenameBeforeEdit = EditGoProRename;
            _videoExtBeforeEdit         = EditVideoExtensions;
            _photoExtBeforeEdit         = EditPhotoExtensions;
            _gyroflowPresetBeforeEdit   = EditGyroflowPreset;
            IsEditing = true;
        });

        StartEditNameCommand  = StartEditCommand;
        ConfirmEditNameCommand = new RelayCommand(() => IsEditing = false);
        CancelEditCommand = new RelayCommand(() =>
        {
            RevertEdits();
            IsEditing = false;
        });
        CancelEditNameCommand = CancelEditCommand;

        StartChangeTypeCommand   = new RelayCommand(() => IsChangingType = true);
        ConfirmChangeTypeCommand = new RelayCommand(() => IsChangingType = false);

        BrowseGyroflowPresetCommand = new RelayCommand(() =>
        {
            var dlg = new OpenFileDialog
            {
                Title  = Strings.Camera_SelectGyroflowPreset,
                Filter = Strings.Camera_GyroflowPresetFilter,
            };

            var presetDir = _configPaths?.GyroflowPresetsDir
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "presets", "gyroflow");
            if (Directory.Exists(presetDir))
                dlg.InitialDirectory = presetDir;

            if (dlg.ShowDialog() == true)
                EditGyroflowPreset = dlg.FileName;
        }, () => IsEditing);

        ClearGyroflowPresetCommand = new RelayCommand(() =>
        {
            EditGyroflowPreset = null;
        }, () => IsEditing);

        BuildBubbleLists();

        CancelImportCommand  = new RelayCommand(() => _importCts?.Cancel());
        PauseImportCommand   = new RelayCommand(TogglePause, () => IsImporting);
    }

    private CancellationTokenSource? _importCts;

    /// <summary>Provides external access for ImportViewModel to wire up the CTS.</summary>
    public void SetImportCts(CancellationTokenSource? cts) => _importCts = cts;

    private ImportPhase _importPhase = ImportPhase.Idle;
    public ImportPhase Phase
    {
        get => _importPhase;
        set
        {
            if (SetProperty(ref _importPhase, value))
            {
                OnPropertyChanged(nameof(IsImporting));
                OnPropertyChanged(nameof(IsImportDone));
                OnPropertyChanged(nameof(IsImportError));
                OnPropertyChanged(nameof(IsImportCancelled));
            }
        }
    }

    public bool IsImporting =>
        Phase != ImportPhase.Idle
        && Phase != ImportPhase.Done
        && Phase != ImportPhase.Error
        && Phase != ImportPhase.Cancelled;

    public bool IsImportDone      => Phase == ImportPhase.Done;
    public bool IsImportError     => Phase == ImportPhase.Error;
    public bool IsImportCancelled => Phase == ImportPhase.Cancelled;

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    private string _progressText = string.Empty;
    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    private string _currentFile = string.Empty;
    public string CurrentFile
    {
        get => _currentFile;
        set => SetProperty(ref _currentFile, value);
    }

    private string _speedText = string.Empty;
    public string SpeedText
    {
        get => _speedText;
        set => SetProperty(ref _speedText, value);
    }

    private string _etaText = string.Empty;
    public string EtaText
    {
        get => _etaText;
        set => SetProperty(ref _etaText, value);
    }

    private string _phaseLabel = string.Empty;
    public string PhaseLabel
    {
        get => _phaseLabel;
        set => SetProperty(ref _phaseLabel, value);
    }

    private string _resultText = string.Empty;
    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    private string? _currentSourceLabel;
    public string? CurrentSourceLabel
    {
        get => _currentSourceLabel;
        set => SetProperty(ref _currentSourceLabel, value);
    }

    private bool _isRendering;
    /// <summary>True while Gyroflow is actively rendering a single video frame-by-frame.</summary>
    public bool IsRendering
    {
        get => _isRendering;
        set => SetProperty(ref _isRendering, value);
    }

    private double _renderProgressPercent;
    /// <summary>Render progress 0–100 (matching CameraCard ProgressBar Maximum=100).</summary>
    public double RenderProgressPercent
    {
        get => _renderProgressPercent;
        set => SetProperty(ref _renderProgressPercent, value);
    }

    private string _renderProgressText = string.Empty;
    /// <summary>Human-readable render progress, e.g. "DJI_clip.mp4 — 57% ETA 9s".</summary>
    public string RenderProgressText
    {
        get => _renderProgressText;
        set => SetProperty(ref _renderProgressText, value);
    }

    public ICommand CancelImportCommand { get; }

    /// <summary>
    /// ManualResetEventSlim passed to RunImportAsync so the core pipeline can block when paused.
    /// Initialized set (running). Reset on pause, Set on resume.
    /// </summary>
    private readonly ManualResetEventSlim _pauseEvent = new(initialState: true);
    public ManualResetEventSlim PauseEvent => _pauseEvent;

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
                OnPropertyChanged(nameof(PauseResumeLabel));
        }
    }

    /// <summary>Label text for the per-camera pause button.</summary>
    public string PauseResumeLabel => IsPaused ? Strings.Camera_Resume : Strings.Camera_Pause;

    public ICommand PauseImportCommand { get; }

    private void TogglePause()
    {
        if (IsPaused)
        {
            _pauseEvent.Set();
            IsPaused = false;
        }
        else
        {
            _pauseEvent.Reset();
            IsPaused = true;
        }
    }

    private int _driveCount;
    /// <summary>
    /// Number of drives that contributed files in a multi-drive quick import.
    /// Set by ImportViewModel.RunQuickImportSdAsync when more than one drive had results.
    /// Reset to 0 on each new import cycle.
    /// </summary>
    public int DriveCount
    {
        get => _driveCount;
        set => SetProperty(ref _driveCount, value);
    }

    public void ResetImportState()
    {
        Phase                = ImportPhase.Idle;
        ProgressPercent      = 0;
        ProgressText         = string.Empty;
        CurrentFile          = string.Empty;
        SpeedText            = string.Empty;
        EtaText              = string.Empty;
        PhaseLabel           = string.Empty;
        ResultText           = string.Empty;
        DriveCount           = 0;
        CurrentSourceLabel   = null;
        IsRendering          = false;
        RenderProgressPercent = 0;
        RenderProgressText   = string.Empty;
        _pauseEvent.Set();
        IsPaused             = false;
    }

    private string? _nameBeforeEdit;
    private string? _folderNameBeforeEdit;
    private string? _cameraTypeBeforeEdit;
    private bool _enabledBeforeEdit;
    private bool _gpsBeforeEdit;
    private bool _gyroflowBeforeEdit;
    private bool _burstBeforeEdit;
    private bool _metadataBeforeEdit;
    private bool _eisBeforeEdit;
    private bool _lensBeforeEdit;
    private bool _postProcessBeforeEdit;
    private bool _renameVideosBeforeEdit;
    private bool _goProRenameBeforeEdit;
    private string? _videoExtBeforeEdit;
    private string? _photoExtBeforeEdit;
    private string? _gyroflowPresetBeforeEdit;

    public CameraConfig Config => _config;

    public string CameraId { get; }
    public string Name         => _config.Name;
    public string Manufacturer => _config.Manufacturer ?? string.Empty;
    public string CameraType   => _config.CameraType;
    public bool   IsEnabled    => _config.Enabled;
    public string SourceType   => _config.SourceType.ToString();

    /// <summary>
    /// Sortierungsreihenfolge für Drag & Drop. Aus CameraConfig.SortOrder.
    /// </summary>
    public int SortOrder => _config.SortOrder;

    public string TypeLabel => CameraType switch
    {
        "Action"     => "Action",
        "Drone"      => "Drone",
        "Mirrorless" => "Mirrorless",
        "DSLR"       => "DSLR",
        "Dashcam"    => "Dashcam",
        _            => CameraType
    };

    /// <summary>CSS-style key used in XAML triggers to choose badge color.</summary>
    public string TypeKey => CameraType switch
    {
        "Action"     => "Action",
        "Drone"      => "Drone",
        "Mirrorless" => "Mirrorless",
        "DSLR"       => "Mirrorless",
        _            => "Other"
    };

    public string TypeColor
    {
        get
        {
            var def = _typeLoader?.GetType(_editCameraType);
            if (def?.Color != null) return def.Color;

            return CameraTypeColors.GetHexColor(_editCameraType);
        }
    }

    public string TypeColorMuted => TypeColor;

    public bool HasNoFeatures => SimpleFeatureBubbles.Count == 0 && AdvancedFeatureBubbles.Count == 0;

    /// <summary>True when the app is in Dau (Easy) mode — drives XAML DataTriggers on CameraCard.</summary>
    public bool IsDauMode => GetIsDauMode?.Invoke() == true;

    public string VideoFormats => _config.FileTypes.Video.Length > 0
        ? string.Join(", ", _config.FileTypes.Video)
        : string.Empty;

    public string PhotoFormats => _config.FileTypes.Photo.Length > 0
        ? string.Join(", ", _config.FileTypes.Photo)
        : string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && !value)
            {
                if (_isEditing)
                {
                    RevertEdits();
                    IsEditing = false;
                }
                IsChangingType = false;
            }
        }
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    private bool _isChangingType;
    public bool IsChangingType
    {
        get => _isChangingType;
        set => SetProperty(ref _isChangingType, value);
    }

    public ICommand ToggleGpsCommand         { get; }
    public ICommand ToggleGyroflowCommand    { get; }
    public ICommand ToggleBurstCommand       { get; }
    public ICommand ToggleMetadataCommand    { get; }
    public ICommand ToggleEisCommand         { get; }
    public ICommand ToggleLensCommand        { get; }
    public ICommand TogglePostProcessCommand { get; }
    public ICommand ToggleRenameVideosCommand { get; }
    public ICommand ToggleGoProRenameCommand { get; }

    public ICommand ToggleGpsSimpleOnCardCommand         { get; }
    public ICommand ToggleGyroflowSimpleOnCardCommand    { get; }
    public ICommand ToggleBurstSimpleOnCardCommand       { get; }
    public ICommand ToggleMetadataSimpleOnCardCommand    { get; }
    public ICommand ToggleEisSimpleOnCardCommand         { get; }
    public ICommand ToggleLensSimpleOnCardCommand        { get; }
    public ICommand TogglePostProcessSimpleOnCardCommand { get; }
    public ICommand ToggleRenameVideosSimpleOnCardCommand { get; }
    public ICommand ToggleGoProRenameSimpleOnCardCommand { get; }

    public ICommand StartEditCommand      { get; }
    public ICommand CancelEditCommand     { get; }

    public ICommand StartEditNameCommand   { get; }
    public ICommand ConfirmEditNameCommand { get; }
    public ICommand CancelEditNameCommand  { get; }
    public ICommand StartChangeTypeCommand   { get; }
    public ICommand ConfirmChangeTypeCommand { get; }

    /// <summary>Opens an OpenFileDialog to pick a .gyroflow preset file. Only active in edit mode.</summary>
    public ICommand BrowseGyroflowPresetCommand { get; }

    /// <summary>Clears the selected gyroflow preset path. Only active in edit mode.</summary>
    public ICommand ClearGyroflowPresetCommand  { get; }

    /// <summary>
    /// ALL profile-available features (featureDef.Available == true) for the current camera type.
    /// Used by the Settings→Cam tab to show every feature with an availability toggle.
    /// IsEnabled here = camera-available (written to AvailableOverrides), NOT feature-enabled.
    /// These are SEPARATE instances from SimpleFeatureBubbles / AdvancedFeatureBubbles.
    /// Bubbles are never removed from this collection — only IsEnabled changes in-place.
    /// </summary>
    public ObservableCollection<FeatureBubbleViewModel> AllAvailableFeatures   { get; } = new();

    /// <summary>
    /// Camera-available features placed on the simple (card header) section.
    /// IsEnabled = feature-enabled state. Separate instances from AllAvailableFeatures.
    /// </summary>
    public ObservableCollection<FeatureBubbleViewModel> SimpleFeatureBubbles   { get; } = new();

    /// <summary>
    /// Camera-available features placed in the advanced (collapsible) section.
    /// IsEnabled = feature-enabled state. Separate instances from AllAvailableFeatures.
    /// </summary>
    public ObservableCollection<FeatureBubbleViewModel> AdvancedFeatureBubbles { get; } = new();

    public bool HasAdvancedFeatures => AdvancedFeatureBubbles.Count > 0;

    /// <summary>
    /// Rebuilds all three bubble collections from scratch.
    ///
    /// SSOT: No hardcoded feature lists here. Iterates Features dict from the preset.
    ///
    /// Cascade logic:
    ///   featureDef.Available == false → not shown anywhere
    ///   GetCameraAvailable == false   → shown in Settings only (hidden from Import)
    ///   GetCameraAvailable == true    → shown in Settings AND Import
    ///
    /// AllAvailableFeatures  — SEPARATE bubble instances, IsEnabled = camera-available.
    ///                         ToggleCommand = ToggleCameraAvailable (writes AvailableOverrides).
    /// SimpleFeatureBubbles  — SEPARATE bubble instances, IsEnabled = feature-enabled.
    ///                         ToggleCommand = feature toggle (writes Features.X).
    /// AdvancedFeatureBubbles — same as Simple but for advanced placement.
    ///
    /// Called on construction, type change, and revert. NOT on individual flag toggles.
    /// Single flag toggles use SyncSingleBubbleState() / SyncImportBubbleAvailability() instead.
    /// </summary>
    private void BuildBubbleLists()
    {
        AllAvailableFeatures.Clear();
        SimpleFeatureBubbles.Clear();
        AdvancedFeatureBubbles.Clear();

        if (GetIsDauMode?.Invoke() == true)
        {
            OnPropertyChanged(nameof(HasAdvancedFeatures));
            OnPropertyChanged(nameof(HasNoFeatures));
            OnPropertyChanged(nameof(IsDauMode));
            OnPropertyChanged(nameof(IsGyroflowAvailable));
            return;
        }

        var typeDef = _typeLoader?.GetType(_editCameraType);
        if (typeDef?.Features == null)
        {

            OnPropertyChanged(nameof(HasAdvancedFeatures));
            OnPropertyChanged(nameof(HasNoFeatures));
            OnPropertyChanged(nameof(IsGyroflowAvailable));
            return;
        }

        var isAdvancedMode  = GetIsAdvancedMode?.Invoke() ?? true;
        var simpleFeatureSet = typeDef.SimpleFeatures;

        foreach (var (key, featureDef) in typeDef.Features)
        {
            if (!featureDef.Available) continue;

            var cameraAvailable = GetCameraAvailable(key, profileDefault: true);
            var isEnabled       = GetEditFlag(key);
            var isSimpleOnCard  = GetSimpleOnCard(key, featureDef.SimpleOnCard);

            var showInSettings = isAdvancedMode || simpleFeatureSet.Contains(key);
            if (showInSettings)
            {
                var settingsBubble = MakeSettingsBubble(key, cameraAvailable);
                AllAvailableFeatures.Add(settingsBubble);
            }

            var showInImport = cameraAvailable && (isAdvancedMode || simpleFeatureSet.Contains(key));
            if (showInImport)
            {
                var importBubble = MakeBubble(key, isEnabled, isSimpleOnCard);
                if (isSimpleOnCard)
                    SimpleFeatureBubbles.Add(importBubble);
                else
                    AdvancedFeatureBubbles.Add(importBubble);
            }
        }

        OnPropertyChanged(nameof(HasAdvancedFeatures));
        OnPropertyChanged(nameof(HasNoFeatures));
        OnPropertyChanged(nameof(IsDauMode));
        OnPropertyChanged(nameof(IsGyroflowAvailable));
    }

    /// <summary>
    /// Creates a FeatureBubbleViewModel for the given feature key.
    /// Labels and badge keys come from FeatureLabels (SSOT — no magic strings).
    /// Toggle commands are resolved via GetToggleCommand / GetToggleSimpleOnCardCommand (SSOT).
    /// </summary>
    private FeatureBubbleViewModel MakeBubble(string featureKey, bool isEnabled, bool isSimpleOnCard)
    {
        var entry   = FeatureLabels.Get(featureKey);
        var label   = entry?.BubbleLabel ?? featureKey;
        var badge   = entry?.BadgeKey    ?? "Post";
        var tooltip = FeatureRegistry.Get(featureKey)?.Description ?? featureKey;

        return new FeatureBubbleViewModel(
            featureKey:       featureKey,
            label:            label,
            badgeKey:         badge,
            tooltip:          tooltip,
            toggleCommand:    GetToggleCommand(featureKey),
            initiallyEnabled: isEnabled,
            isSimpleOnCard:   isSimpleOnCard)
        {
            ToggleSimpleOnCardCommand = GetToggleSimpleOnCardCommand(featureKey),
        };
    }

    /// <summary>
    /// Creates a FeatureBubbleViewModel for the Settings→Cam section.
    /// IsEnabled = camera-available (not feature-enabled).
    /// ToggleCommand = writes to AvailableOverrides via ToggleCameraAvailable.
    /// </summary>
    private FeatureBubbleViewModel MakeSettingsBubble(string featureKey, bool cameraAvailable)
    {
        var entry   = FeatureLabels.Get(featureKey);
        var label   = entry?.BubbleLabel ?? featureKey;
        var badge   = entry?.BadgeKey    ?? "Post";
        var tooltip = FeatureRegistry.Get(featureKey)?.Description ?? featureKey;

        return new FeatureBubbleViewModel(
            featureKey:       featureKey,
            label:            label,
            badgeKey:         badge,
            tooltip:          tooltip,
            toggleCommand:    new RelayCommand(() => ToggleCameraAvailable(featureKey)),
            initiallyEnabled: cameraAvailable,
            isSimpleOnCard:   false)
        {
            ToggleSimpleOnCardCommand = new RelayCommand(() => { }),
        };
    }

    /// <summary>
    /// Toggles camera-level availability for the given feature key.
    /// Writes to AvailableOverrides and triggers an async config save.
    /// Updates the Settings bubble in-place and adds/removes the corresponding Import bubble.
    /// </summary>
    private void ToggleCameraAvailable(string featureKey)
    {
        var current = GetCameraAvailable(featureKey, profileDefault: true);
        var newVal  = !current;
        _config.AvailableOverrides[featureKey] = newVal;
        _ = OnFeatureSaveRequested?.Invoke();

        var settingsBubble = AllAvailableFeatures.FirstOrDefault(b => b.FeatureKey == featureKey);
        if (settingsBubble != null)
            settingsBubble.IsEnabled = newVal;

        SyncImportBubbleAvailability(featureKey, newVal);
    }

    /// <summary>
    /// Adds or removes the Import bubble for a feature when camera-availability changes.
    /// When becoming available: creates and adds a new import bubble.
    /// When becoming unavailable: removes the import bubble from Simple/Advanced collections.
    /// </summary>
    private void SyncImportBubbleAvailability(string featureKey, bool cameraAvailable)
    {
        if (cameraAvailable)
        {

            var typeDef    = _typeLoader?.GetType(_editCameraType);
            var featureDef = typeDef?.Features?.GetValueOrDefault(featureKey);
            if (featureDef == null) return;

            var isEnabled      = GetEditFlag(featureKey);
            var isSimpleOnCard = GetSimpleOnCard(featureKey, featureDef.SimpleOnCard);
            var bubble         = MakeBubble(featureKey, isEnabled, isSimpleOnCard);

            if (isSimpleOnCard)
                SimpleFeatureBubbles.Add(bubble);
            else
                AdvancedFeatureBubbles.Add(bubble);
        }
        else
        {

            var simple   = SimpleFeatureBubbles.FirstOrDefault(b => b.FeatureKey == featureKey);
            var advanced = AdvancedFeatureBubbles.FirstOrDefault(b => b.FeatureKey == featureKey);

            if (simple != null)   SimpleFeatureBubbles.Remove(simple);
            if (advanced != null) AdvancedFeatureBubbles.Remove(advanced);
        }

        OnPropertyChanged(nameof(HasAdvancedFeatures));
        OnPropertyChanged(nameof(HasNoFeatures));
    }

    /// <summary>
    /// Maps a canonical feature key to its toggle ICommand.
    /// SSOT: the ONE place where feature-key → toggle-command is defined.
    /// </summary>
    private ICommand GetToggleCommand(string featureKey) => featureKey switch
    {
        FeatureKeys.GpsInjection   => ToggleGpsCommand,
        FeatureKeys.Gyroflow       => ToggleGyroflowCommand,
        FeatureKeys.EisDetection   => ToggleEisCommand,
        FeatureKeys.BurstDetection => ToggleBurstCommand,
        FeatureKeys.MetadataBackup => ToggleMetadataCommand,
        FeatureKeys.RenameVideos   => ToggleRenameVideosCommand,
        FeatureKeys.GoProRename    => ToggleGoProRenameCommand,
        FeatureKeys.PostProcess    => TogglePostProcessCommand,
        FeatureKeys.LensCorrection => ToggleLensCommand,
        _                          => new RelayCommand(() => { }),
    };

    /// <summary>
    /// Maps a canonical feature key to its SimpleOnCard toggle ICommand.
    /// SSOT: the ONE place where feature-key → SimpleOnCard-toggle-command is defined.
    /// Used by Advanced section rows to promote/demote features to/from the simple card view.
    /// </summary>
    private ICommand GetToggleSimpleOnCardCommand(string featureKey) => featureKey switch
    {
        FeatureKeys.GpsInjection   => ToggleGpsSimpleOnCardCommand,
        FeatureKeys.Gyroflow       => ToggleGyroflowSimpleOnCardCommand,
        FeatureKeys.EisDetection   => ToggleEisSimpleOnCardCommand,
        FeatureKeys.BurstDetection => ToggleBurstSimpleOnCardCommand,
        FeatureKeys.MetadataBackup => ToggleMetadataSimpleOnCardCommand,
        FeatureKeys.RenameVideos   => ToggleRenameVideosSimpleOnCardCommand,
        FeatureKeys.GoProRename    => ToggleGoProRenameSimpleOnCardCommand,
        FeatureKeys.PostProcess    => TogglePostProcessSimpleOnCardCommand,
        FeatureKeys.LensCorrection => ToggleLensSimpleOnCardCommand,
        _                          => new RelayCommand(() => { }),
    };

    /// <summary>
    /// Returns the current value of the Edit-property for the given feature key.
    /// SSOT: the ONE place where feature-key → Edit-property-getter is defined.
    /// </summary>
    private bool GetEditFlag(string featureKey) => featureKey switch
    {
        FeatureKeys.GpsInjection   => _editGps,
        FeatureKeys.Gyroflow       => _editGyroflow,
        FeatureKeys.EisDetection   => _editEisDetection,
        FeatureKeys.BurstDetection => _editBurstDetection,
        FeatureKeys.MetadataBackup => _editMetadataBackup,
        FeatureKeys.RenameVideos   => _editRenameVideos,
        FeatureKeys.GoProRename    => _editGoProRename,
        FeatureKeys.PostProcess    => _editPostProcess,
        FeatureKeys.LensCorrection => _editLensCorrection,
        _                          => false,
    };

    /// <summary>
    /// Returns the SimpleOnCard value for a feature key from the preset.
    /// SimpleOnCardOverrides are legacy (Advanced Section removed in TASK-169) and ignored.
    /// SSOT: preset's simple_on_card is the only source.
    /// </summary>
    private bool GetSimpleOnCard(string featureKey, bool presetDefault)
        => presetDefault;

    /// <summary>
    /// Overload used when no preset default is available (falls back to preset lookup).
    /// </summary>
    private bool GetSimpleOnCard(string featureKey)
    {
        var typeDef = _typeLoader?.GetType(_editCameraType);
        if (typeDef?.Features != null && typeDef.Features.TryGetValue(featureKey, out var featureDef))
            return featureDef.SimpleOnCard;

        return false;
    }

    /// <summary>
    /// Returns effective camera-level availability for a feature.
    /// Per-camera AvailableOverrides takes priority over the profile's Available flag.
    /// </summary>
    private bool GetCameraAvailable(string featureKey, bool profileDefault)
        => _config.AvailableOverrides.TryGetValue(featureKey, out var val) ? val : profileDefault;

    /// <summary>
    /// Sets the SimpleOnCard override for the given feature key and updates the import bubble in-place.
    /// Persists the override to config.SimpleOnCardOverrides and triggers an async save.
    ///
    /// Import bubbles are separate from Settings bubbles (AllAvailableFeatures).
    /// When the value changes direction (simple ↔ advanced), the import bubble is moved between
    /// SimpleFeatureBubbles and AdvancedFeatureBubbles. Only the affected bubble moves —
    /// all other bubbles remain untouched (no full rebuild, no flicker).
    /// </summary>
    private void SetSimpleOnCard(string featureKey, bool value)
    {
        _config.SimpleOnCardOverrides[featureKey] = value;
        _ = OnFeatureSaveRequested?.Invoke();

        var bubble = SimpleFeatureBubbles.FirstOrDefault(b => b.FeatureKey == featureKey)
                  ?? AdvancedFeatureBubbles.FirstOrDefault(b => b.FeatureKey == featureKey);

        if (bubble == null) return;

        bubble.IsSimpleOnCard = value;

        if (value && !SimpleFeatureBubbles.Contains(bubble))
        {
            AdvancedFeatureBubbles.Remove(bubble);
            if (SimpleFeatureBubbles.All(b => b.FeatureKey != featureKey))
                SimpleFeatureBubbles.Add(bubble);
        }
        else if (!value && !AdvancedFeatureBubbles.Contains(bubble))
        {
            SimpleFeatureBubbles.Remove(bubble);
            if (AdvancedFeatureBubbles.All(b => b.FeatureKey != featureKey))
                AdvancedFeatureBubbles.Add(bubble);
        }

        OnPropertyChanged(nameof(HasAdvancedFeatures));
        OnPropertyChanged(nameof(HasNoFeatures));
    }

    /// <summary>
    /// Sets the Edit-property for the given feature key and updates the affected bubble in-place.
    /// SSOT: the ONE place where feature-key → Edit-property-setter is defined.
    ///
    /// Does NOT rebuild bubble collections (avoids flicker — BUG 3 fix).
    /// Only the one FeatureBubbleViewModel whose key matches is updated.
    /// Also writes the new value directly to the underlying CameraConfig and fires
    /// OnFeatureSaveRequested so config.json is persisted immediately (no import required).
    /// </summary>
    private void SetEditFlag(string featureKey, bool value)
    {
        switch (featureKey)
        {
            case FeatureKeys.GpsInjection:   EditGps           = value; break;
            case FeatureKeys.Gyroflow:       EditGyroflow       = value; break;
            case FeatureKeys.EisDetection:   EditEisDetection   = value; break;
            case FeatureKeys.BurstDetection: EditBurstDetection = value; break;
            case FeatureKeys.MetadataBackup: EditMetadataBackup = value; break;
            case FeatureKeys.RenameVideos:   EditRenameVideos   = value; break;
            case FeatureKeys.GoProRename:    EditGoProRename    = value; break;
            case FeatureKeys.PostProcess:    EditPostProcess    = value; break;
            case FeatureKeys.LensCorrection: EditLensCorrection = value; break;
        }

        SyncSingleBubbleState(featureKey, value);

        WriteFeatureToConfig(featureKey, value);
        _ = OnFeatureSaveRequested?.Invoke();
    }

    /// <summary>
    /// Writes a single feature flag directly to the underlying CameraConfig.
    /// Delegates to CameraFeatures.SetByKey — SSOT for feature-key → property mapping.
    /// Called by SetEditFlag immediately after the Edit-property is set so that
    /// _config.Features always mirrors the current Edit-state for feature flags.
    /// </summary>
    private void WriteFeatureToConfig(string featureKey, bool value)
    {
        _config.Features.SetByKey(featureKey, value);
    }

    /// <summary>
    /// Updates the IsEnabled state of the Import bubble for the given feature key.
    /// Import bubbles (SimpleFeatureBubbles / AdvancedFeatureBubbles) are SEPARATE instances
    /// from AllAvailableFeatures — only one of the two Import-Collections will contain the bubble.
    /// No add/remove here: bubbles stay in their section as long as the feature is camera-available.
    /// Visual active/inactive state is conveyed via bubble.IsEnabled (farbig=enabled, grau=disabled).
    /// Called by SetEditFlag after an enable/disable toggle — avoids full rebuild (no flicker).
    /// </summary>
    private void SyncSingleBubbleState(string featureKey, bool isEnabled)
    {

        var bubble = SimpleFeatureBubbles.FirstOrDefault(b => b.FeatureKey == featureKey)
                  ?? AdvancedFeatureBubbles.FirstOrDefault(b => b.FeatureKey == featureKey);

        if (bubble != null)
            bubble.IsEnabled = isEnabled;

    }

    /// <summary>
    /// Full rebuild of bubble collections to reflect the current toggle states.
    /// Called only when type changes or on revert/refresh — NOT on individual flag toggles.
    /// </summary>
    private void SyncBubbleStates() => BuildBubbleLists();

    /// <summary>
    /// Refreshes type-derived display properties after a profile save changes the
    /// CameraTypeDefinition (color, feature availability). Triggers UI rebind without
    /// reloading config from disk.
    /// Called by ProfilesTabViewModel.OnProfileSaved() for all matching cameras.
    /// </summary>
    public void RefreshFromTypeDefinition()
    {

        OnPropertyChanged(nameof(TypeColor));
        OnPropertyChanged(nameof(TypeColorMuted));

        BuildBubbleLists();
    }

    /// <summary>
    /// Rebuilds all bubble lists when the app mode (Simple/Advanced) changes.
    /// Called by MainViewModel.RebuildAllCameraBubbleLists() after an IsAdvancedMode toggle
    /// so Settings→Cam updates instantly without a full config reload.
    /// </summary>
    public void RebuildSettingsBubbles() => BuildBubbleLists();

    public ObservableCollection<StorageDeviceInfo> StorageDevices { get; } = new();
    public bool HasNoStorageDevices => StorageDevices.Count == 0;

    private string _storageSummary = string.Empty;
    public string StorageSummary
    {
        get => _storageSummary;
        set => SetProperty(ref _storageSummary, value);
    }

    public void RefreshStorageSummary(UmiConfig config)
    {
        StorageDevices.Clear();

        var connectedVsns = new HashSet<string>(
            _driveLetterToVsn.Values, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in config.SdCards.Where(kv => kv.Value.CameraId == CameraId))
        {
            var isConnected = connectedVsns.Contains(kv.Key);
            StorageDevices.Add(new StorageDeviceInfo(kv.Value.Label ?? kv.Key, StorageDeviceType.SdCard, IsConnected: isConnected));
        }

        var hasMtpConnected = _connectedDriveLetters.Count > 0;
        foreach (var kv in config.MtpDevices.Where(kv => kv.Value.CameraId == CameraId))
            StorageDevices.Add(new StorageDeviceInfo(kv.Value.Label ?? kv.Key, StorageDeviceType.Mtp, IsConnected: hasMtpConnected));

        if (_config.SourceType == UMI.Core.SourceType.FixedPath
            && !string.IsNullOrWhiteSpace(_config.SourcePath))
        {
            StorageDevices.Add(new StorageDeviceInfo(_config.SourcePath, StorageDeviceType.FixedPath, IsConnected: Directory.Exists(_config.SourcePath)));
        }

        var sdCount       = StorageDevices.Count(d => d.Type == StorageDeviceType.SdCard);
        var mtpCount      = StorageDevices.Count(d => d.Type == StorageDeviceType.Mtp);
        var fixedCount    = StorageDevices.Count(d => d.Type == StorageDeviceType.FixedPath);

        StorageSummary = (sdCount, mtpCount, fixedCount) switch
        {
            (0, 0, 0)          => Strings.Camera_NoDevices,
            (0, 0, _)          => Strings.Camera_FixedPathSource,
            (1, 0, 0)          => Strings.Camera_SdCardAssigned,
            (var s, 0, 0)      => string.Format(Strings.Camera_SdCardsAssigned, s),
            (0, 1, 0)          => Strings.Camera_MtpDeviceAssigned,
            (0, var m, 0)      => string.Format(Strings.Camera_MtpDevicesAssigned, m),
            (1, 1, 0)          => Strings.Camera_SdAndMtp,
            (var s, var m, 0)  => string.Format(Strings.Camera_SdAndMtpMultiple, s, m),
            (var s, var m, _)  => string.Format(Strings.Camera_SdMtpFixed, s, m),
        };

        OnPropertyChanged(nameof(HasNoStorageDevices));
        OnPropertyChanged(nameof(IsCardConnected));
    }

    /// <summary>
    /// Tracks which drive letters are currently connected for this camera.
    /// Populated by ImportViewModel via SetDriveConnected when drives arrive/depart.
    /// Used by RefreshStorageSummary to set IsConnected per StorageDeviceInfo entry.
    /// </summary>
    private readonly HashSet<string> _connectedDriveLetters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps drive letters to their VSN so per-VSN IsConnected can be resolved in RefreshStorageSummary.
    /// Populated by ImportViewModel via SetDriveConnected (vsn parameter) when drives arrive.
    /// </summary>
    private readonly Dictionary<string, string> _driveLetterToVsn =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records that a drive letter is now connected (or disconnected) for this camera.
    /// The vsn parameter maps the drive letter to a specific SD card entry so that
    /// RefreshStorageSummary can mark exactly the right StorageDeviceInfo as connected.
    /// Triggers a StorageDevices refresh so each SdCard entry gets its own IsConnected dot.
    /// </summary>
    public void SetDriveConnected(string driveLetter, string? vsn, bool connected)
    {
        if (connected)
        {
            _connectedDriveLetters.Add(driveLetter);
            if (vsn is not null) _driveLetterToVsn[driveLetter] = vsn;
        }
        else
        {
            _connectedDriveLetters.Remove(driveLetter);
            _driveLetterToVsn.Remove(driveLetter);
        }

    }

    /// <summary>
    /// Returns true when this camera has a drive letter currently registered as connected.
    /// Used by OnDriveRemoved to find the correct camera for a removed drive across all cameras.
    /// </summary>
    public bool HasConnectedDriveLetter(string driveLetter)
        => _connectedDriveLetters.Contains(driveLetter);

    /// <summary>
    /// Returns the VSN mapped to the given drive letter, or null if not found.
    /// Must be called BEFORE SetDriveConnected(false) to retrieve the VSN before it is removed.
    /// </summary>
    public string? GetVsnForDriveLetter(string driveLetter)
        => _driveLetterToVsn.GetValueOrDefault(driveLetter);

    /// <summary>
    /// Computed: true when any registered StorageDevice is currently connected.
    /// No backing field — derived from StorageDevices collection after each RefreshStorageSummary.
    /// </summary>
    public bool IsCardConnected => StorageDevices.Any(d => d.IsConnected);

    private string? _connectedDriveLetter;
    public string? ConnectedDriveLetter
    {
        get => _connectedDriveLetter;
        set => SetProperty(ref _connectedDriveLetter, value);
    }

    private string _editName;
    public string EditName
    {
        get => _editName;
        set
        {
            if (SetProperty(ref _editName, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private string _editFolderName;
    public string EditFolderName
    {
        get => _editFolderName;
        set
        {
            if (SetProperty(ref _editFolderName, value))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(FolderNameDisplay));
            }
        }
    }

    public string FolderNameDisplay =>
        string.IsNullOrWhiteSpace(_editFolderName) ? CameraId : _editFolderName;

    private string _editCameraType;
    public string EditCameraType
    {
        get => _editCameraType;
        set
        {
            if (SetProperty(ref _editCameraType, value))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(GpsHintText));
                OnPropertyChanged(nameof(TypeColor));
                OnPropertyChanged(nameof(TypeColorMuted));

                var newTypeDef = _typeLoader?.GetType(value);
                if (newTypeDef?.Features != null)
                {
                    foreach (var (key, featureDef) in newTypeDef.Features)
                    {
                        if (!featureDef.Available && GetEditFlag(key))
                            SetEditFlag(key, false);
                    }
                }

                BuildBubbleLists();
            }
        }
    }

    private bool _editEnabled;
    public bool EditEnabled
    {
        get => _editEnabled;
        set
        {
            if (SetProperty(ref _editEnabled, value))
            {

                _config.Enabled = value;

                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(IsEnabled));

                _ = OnFeatureSaveRequested?.Invoke();
            }
        }
    }

    private bool _editGps;
    public bool EditGps
    {
        get => _editGps;
        set
        {
            if (SetProperty(ref _editGps, value))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(GpsHintText));
                OnPropertyChanged(nameof(ShowGpsHint));

            }
        }
    }

    public bool ShowGpsHint  => _editGps;

    public string GpsHintText => _editCameraType?.ToLowerInvariant() switch
    {
        "drone" => Strings.Camera_GpsHintDrone,
        _       => Strings.Camera_GpsHintDefault,
    };

    private bool _editGyroflow;
    public bool EditGyroflow
    {
        get => _editGyroflow;
        set
        {
            if (SetProperty(ref _editGyroflow, value))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(IsGyroflowEnabled));
            }
        }
    }

    private bool _editBurstDetection;
    public bool EditBurstDetection
    {
        get => _editBurstDetection;
        set { if (SetProperty(ref _editBurstDetection, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private bool _editMetadataBackup;
    public bool EditMetadataBackup
    {
        get => _editMetadataBackup;
        set { if (SetProperty(ref _editMetadataBackup, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private bool _editEisDetection;
    public bool EditEisDetection
    {
        get => _editEisDetection;
        set { if (SetProperty(ref _editEisDetection, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private bool _editLensCorrection;
    public bool EditLensCorrection
    {
        get => _editLensCorrection;
        set { if (SetProperty(ref _editLensCorrection, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private bool _editPostProcess;
    public bool EditPostProcess
    {
        get => _editPostProcess;
        set { if (SetProperty(ref _editPostProcess, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private bool _editRenameVideos;
    public bool EditRenameVideos
    {
        get => _editRenameVideos;
        set { if (SetProperty(ref _editRenameVideos, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private bool _editGoProRename;
    public bool EditGoProRename
    {
        get => _editGoProRename;
        set { if (SetProperty(ref _editGoProRename, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private string? _editGyroflowPreset;

    /// <summary>
    /// Editable path to the .gyroflow preset file.
    /// Initialized from config.PostProcessing?.Gyroflow?.Preset.
    /// Persisted on Save to config.PostProcessing.Gyroflow.Preset.
    /// </summary>
    public string? EditGyroflowPreset
    {
        get => _editGyroflowPreset;
        set
        {
            if (SetProperty(ref _editGyroflowPreset, value))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(HasGyroflowPreset));
            }
        }
    }

    /// <summary>True when a gyroflow preset path is set.</summary>
    public bool HasGyroflowPreset => !string.IsNullOrWhiteSpace(_editGyroflowPreset);

    /// <summary>
    /// True when the gyroflow feature is enabled for this camera.
    /// Reflects the live edit flag — updates immediately when the user toggles the feature.
    /// </summary>
    public bool IsGyroflowEnabled => _editGyroflow;

    /// <summary>True when gyroflow is an available feature for this camera type (regardless of enabled state).</summary>
    public bool IsGyroflowAvailable => AllAvailableFeatures.Any(f => f.FeatureKey == FeatureKeys.Gyroflow);

    private string _editVideoExtensions;
    /// <summary>
    /// Comma-separated video extensions for inline editing (e.g. ".mp4, .mov").
    /// Parsed back to string[] by ParseExtensions on Save.
    /// </summary>
    public string EditVideoExtensions
    {
        get => _editVideoExtensions;
        set { if (SetProperty(ref _editVideoExtensions, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    private string _editPhotoExtensions;
    /// <summary>
    /// Comma-separated photo extensions for inline editing (e.g. ".jpg, .jpeg, .dng").
    /// Parsed back to string[] by ParseExtensions on Save.
    /// </summary>
    public string EditPhotoExtensions
    {
        get => _editPhotoExtensions;
        set { if (SetProperty(ref _editPhotoExtensions, value)) { OnPropertyChanged(nameof(IsDirty)); } }
    }

    /// <summary>
    /// Parses a comma-separated extension string into a normalized string[] for config storage.
    /// Accepts ".mp4, .mov", filters out entries without leading dot, lowercases, deduplicates.
    /// </summary>
    private static string[] ParseExtensions(string input) =>
        input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Where(s => s.StartsWith('.'))
             .Select(s => s.ToLowerInvariant())
             .Distinct()
             .ToArray();

    /// <summary>
    /// Formats a string[] of extensions into a comma-separated display string.
    /// </summary>
    private static string FormatExtensions(string[] extensions) =>
        string.Join(", ", extensions);

    public bool IsDirty =>
        _editName        != _config.Name ||
        _editFolderName  != (_config.FolderName ?? string.Empty) ||
        _editCameraType  != _config.CameraType ||
        _editEnabled     != _config.Enabled ||
        _editGps         != _config.Features.GpsInjection ||
        _editGyroflow    != _config.Features.Gyroflow ||
        _editBurstDetection != _config.Features.BurstDetection ||
        _editMetadataBackup != _config.Features.MetadataBackup ||
        _editEisDetection   != _config.Features.EisDetection ||
        _editLensCorrection != _config.Features.LensCorrection ||
        _editPostProcess    != _config.Features.PostProcess ||
        _editRenameVideos   != _config.Features.RenameVideos ||
        _editGoProRename    != _config.Features.GoProRename ||
        _editVideoExtensions != FormatExtensions(_config.FileTypes.Video) ||
        _editPhotoExtensions != FormatExtensions(_config.FileTypes.Photo) ||
        _editGyroflowPreset != _config.PostProcessing?.Gyroflow?.Preset;

    public void ApplyEdits()
    {
        _config.Name       = _editName;
        _config.FolderName = string.IsNullOrWhiteSpace(_editFolderName) ? null : _editFolderName;
        _config.CameraType = _editCameraType;
        _config.Enabled    = _editEnabled;

        _config.Features.GpsInjection   = _editGps;
        _config.Features.Gyroflow       = _editGyroflow;
        _config.Features.BurstDetection = _editBurstDetection;
        _config.Features.MetadataBackup = _editMetadataBackup;
        _config.Features.EisDetection   = _editEisDetection;
        _config.Features.LensCorrection = _editLensCorrection;
        _config.Features.PostProcess    = _editPostProcess;
        _config.Features.RenameVideos   = _editRenameVideos;
        _config.Features.GoProRename    = _editGoProRename;

        _config.FileTypes.Video = ParseExtensions(_editVideoExtensions);
        _config.FileTypes.Photo = ParseExtensions(_editPhotoExtensions);

        _config.PostProcessing          ??= new PostProcessingConfig();
        _config.PostProcessing.Gyroflow ??= new GyroflowProcessingConfig();
        _config.PostProcessing.Gyroflow.Preset = _editGyroflowPreset;

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FolderNameDisplay));
        OnPropertyChanged(nameof(CameraType));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(TypeKey));
        OnPropertyChanged(nameof(TypeColor));
        OnPropertyChanged(nameof(TypeColorMuted));
        OnPropertyChanged(nameof(HasNoFeatures));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(GpsHintText));
        OnPropertyChanged(nameof(ShowGpsHint));
        OnPropertyChanged(nameof(VideoFormats));
        OnPropertyChanged(nameof(PhotoFormats));
    }

    public void RevertEdits()
    {
        EditName        = _config.Name;
        EditFolderName  = _config.FolderName ?? string.Empty;
        EditCameraType  = _config.CameraType;
        EditEnabled     = _config.Enabled;

        EditGps            = _config.Features.GpsInjection;
        EditGyroflow       = _config.Features.Gyroflow;
        EditBurstDetection = _config.Features.BurstDetection;
        EditMetadataBackup = _config.Features.MetadataBackup;
        EditEisDetection   = _config.Features.EisDetection;
        EditLensCorrection = _config.Features.LensCorrection;
        EditPostProcess    = _config.Features.PostProcess;
        EditRenameVideos   = _config.Features.RenameVideos;
        EditGoProRename    = _config.Features.GoProRename;

        EditVideoExtensions = FormatExtensions(_config.FileTypes.Video);
        EditPhotoExtensions = FormatExtensions(_config.FileTypes.Photo);
        EditGyroflowPreset  = _config.PostProcessing?.Gyroflow?.Preset;

        OnPropertyChanged(nameof(IsDirty));

        SyncBubbleStates();
    }

    /// <summary>
    /// Camera types available for selection in the type picker.
    /// Loaded from <see cref="CameraTypeLoader"/> when available; falls back to a
    /// minimal built-in list only when no loader is injected (design-time or tests).
    /// </summary>
    public IReadOnlyList<string> AvailableCameraTypes =>
        _typeLoader?.ListAvailableTypes() is { Count: > 0 } loaded
            ? loaded
            : (IReadOnlyList<string>)["Action", "Drone", "Mirrorless", "DSLR", "Dashcam"];
}

/// <summary>
/// Device type for storage icons in the expanded camera card.
/// </summary>
public enum StorageDeviceType
{
    SdCard,
    Mtp,
    FixedPath,
}

/// <summary>
/// Lightweight DTO representing a storage device assigned to a camera.
/// IsConnected is true when this specific device is currently plugged in and recognized.
/// </summary>
public sealed record StorageDeviceInfo(string Label, StorageDeviceType Type, bool IsConnected = false);
