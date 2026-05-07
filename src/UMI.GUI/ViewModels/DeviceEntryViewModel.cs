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
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Helpers;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Distinguishes the storage device type for grouping and icon selection in the Devices tab.
/// </summary>
public enum DeviceEntryType
{
    SdCard,
    MtpDevice,
    FixedPath
}

/// <summary>
/// ViewModel for a single registered device entry in the Devices tab.
/// Supports inline expand/collapse, dirty-tracking, save/revert, and delete.
/// </summary>
public class DeviceEntryViewModel : ViewModelBase
{
    private readonly IConfigWriterService _configWriter;
    private readonly IDriveWatcherService? _driveWatcher;
    private readonly ILogger? _logger;
    private readonly UMI.Core.Services.CameraTypeLoader? _typeLoader;

    private string _key;
    /// <summary>Registry key: VolumeSerial (SD), SerialNumber (MTP), or path (FixedPath).</summary>
    public string Key
    {
        get => _key;
        private set => SetProperty(ref _key, value);
    }

    /// <summary>Device type for grouping and icons.</summary>
    public DeviceEntryType DeviceType { get; }

    private string _label = string.Empty;
    /// <summary>Human-readable label shown in the collapsed header.</summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    private string _cameraId = string.Empty;
    /// <summary>Assigned camera ID (primary association).</summary>
    public string CameraId
    {
        get => _cameraId;
        set
        {
            if (SetProperty(ref _cameraId, value))
            {
                OnPropertyChanged(nameof(IsFloating));
                OnPropertyChanged(nameof(IsFixed));
                OnPropertyChanged(nameof(IsCameraEnabled));
                OnPropertyChanged(nameof(IsCameraDisabled));
            }
        }
    }

    /// <summary>True when the card is Floating (CameraId is empty — not permanently assigned to one camera).</summary>
    public bool IsFloating => string.IsNullOrEmpty(_cameraId);

    public bool IsFixed => !IsFloating;

    /// <summary>Disk serial (SD only, read-only). Null if not available.</summary>
    public string? DiskSerial { get; }

    /// <summary>Size in bytes (SD only, read-only).</summary>
    public long SizeBytes { get; }

    /// <summary>FirstSeen timestamp (ISO 8601). Preserved across edits — never overwritten with empty.</summary>
    private readonly string? _firstSeen;

    /// <summary>Human-readable size string.</summary>
    public string SizeDisplay => SizeBytes > 0
        ? FormatHelper.FormatBytes(SizeBytes)
        : string.Empty;

    /// <summary>Usage history: last-used-with camera ids (Floating SD only).</summary>
    public IReadOnlyDictionary<string, DateTime>? LastUsedWith { get; }

    /// <summary>Usage history: import count per camera (SD only). SSOT: SdCardRegistration.UsageHistory.</summary>
    public IReadOnlyDictionary<string, int>? UsageHistory { get; }

    /// <summary>True when the card has at least one recorded import.</summary>
    public bool HasUsageHistory => UsageHistory?.Count > 0;

    /// <summary>Computed usage entries for the history popup, sorted by count descending (mirrors CLI CardsCommand).</summary>
    public IReadOnlyList<CardUsageEntry> UsageEntries { get; }

    /// <summary>Total import count across all cameras.</summary>
    public int TotalImportCount { get; }

    private bool _isHistoryVisible;
    /// <summary>Controls visibility of the usage history popup (SD cards only).</summary>
    public bool IsHistoryVisible
    {
        get => _isHistoryVisible;
        set => SetProperty(ref _isHistoryVisible, value);
    }

    private string _editLabel = string.Empty;
    public string EditLabel
    {
        get => _editLabel;
        set
        {
            if (SetProperty(ref _editLabel, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private string _editCameraId = string.Empty;
    public string EditCameraId
    {
        get => _editCameraId;
        set
        {
            if (SetProperty(ref _editCameraId, value))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(EditIsFloating));
                OnPropertyChanged(nameof(EditIsFixed));
            }
        }
    }

    /// <summary>True when the edit camera assignment is Floating (EditCameraId is empty).</summary>
    public bool EditIsFloating => string.IsNullOrEmpty(_editCameraId);

    public bool EditIsFixed => !EditIsFloating;

    private string _editKey = string.Empty;
    /// <summary>
    /// Editable copy of the registry key (VSN for SD cards).
    /// Changes here are applied on Save — old key is removed, new key is registered.
    /// </summary>
    public string EditKey
    {
        get => _editKey;
        set
        {
            if (SetProperty(ref _editKey, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    /// <summary>True when EditKey differs from the persisted key.</summary>
    public bool IsKeyChanged => DeviceType == DeviceEntryType.SdCard && EditKey != Key;

    private bool _isRelearning;
    public bool IsRelearning
    {
        get => _isRelearning;
        private set => SetProperty(ref _isRelearning, value);
    }

    private string? _reLearnMessage;
    public string? ReLearnMessage
    {
        get => _reLearnMessage;
        private set => SetProperty(ref _reLearnMessage, value);
    }

    /// <summary>True when SD card entries have the Re-learn VSN button available.</summary>
    public bool CanReLearn => DeviceType == DeviceEntryType.SdCard && _driveWatcher is not null;

    /// <summary>True when edit state differs from persisted state.</summary>
    public bool IsDirty =>
        EditLabel != Label ||
        EditCameraId != CameraId ||
        (DeviceType == DeviceEntryType.SdCard && EditKey != Key) ||
        FeatureFlags.Any(f => f.IsModified);

    private bool _isCardConnected;
    /// <summary>True when the SD card for this entry is currently inserted.</summary>
    public bool IsCardConnected
    {
        get => _isCardConnected;
        set => SetProperty(ref _isCardConnected, value);
    }

    private string? _connectedDriveLetter;
    /// <summary>Drive letter of the inserted SD card, or null when not connected.</summary>
    public string? ConnectedDriveLetter
    {
        get => _connectedDriveLetter;
        set => SetProperty(ref _connectedDriveLetter, value);
    }

    /// <summary>
    /// Whether the assigned camera is enabled for import.
    /// Reads from and writes directly to CameraConfig.Enabled — saves immediately on change.
    /// No-op (always returns true) when no camera is assigned to this device.
    /// </summary>
    public bool IsCameraEnabled
    {
        get
        {
            if (string.IsNullOrEmpty(_cameraId)) return true;
            var cameras = _configWriter.Config?.Cameras;
            if (cameras is null || !cameras.TryGetValue(_cameraId, out var cfg)) return true;
            return cfg.Enabled;
        }
        set
        {
            if (string.IsNullOrEmpty(_cameraId)) return;
            var cameras = _configWriter.Config?.Cameras;
            if (cameras is null || !cameras.ContainsKey(_cameraId)) return;

            _configWriter.UpdateCamera(_cameraId, cfg => cfg.Enabled = value);
            OnPropertyChanged(nameof(IsCameraEnabled));
            OnPropertyChanged(nameof(IsCameraDisabled));
            _ = SaveEnabledAsync(value);
        }
    }

    /// <summary>Inverse of IsCameraEnabled — drives body-dimming DataTrigger in XAML.</summary>
    public bool IsCameraDisabled => !IsCameraEnabled;

    private async Task SaveEnabledAsync(bool enabled)
    {
        try
        {
            await _configWriter.SaveAsync();
            _logger?.LogInformation("Camera {CameraId} enabled={Enabled} saved", _cameraId, enabled);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save enabled state for camera {CameraId}", _cameraId);
        }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && !value && _isEditing)
            {

                ExecuteRevert();
            }
        }
    }

    private bool _isEditing;
    /// <summary>
    /// True when the user has clicked the Edit button and fields are editable.
    /// Defaults to false — fields are read-only until explicitly unlocked.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
                OnPropertyChanged(nameof(IsReadOnly));
        }
    }

    /// <summary>Inverse of IsEditing — drives TextBlock/TextBox visibility in XAML.</summary>
    public bool IsReadOnly => !_isEditing;

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    /// <summary>
    /// Save/error status message shown on the device entry.
    /// Delegates to <see cref="ViewModelBase.StatusMessage"/> so
    /// <see cref="ViewModelBase.ScheduleClearStatus"/> clears it without a duplicate CTS.
    /// </summary>
    public string? SaveStatus
    {
        get => StatusMessage;
        private set
        {
            StatusMessage = value;
            OnPropertyChanged(nameof(SaveStatus));
        }
    }

    /// <summary>
    /// Overrides the base setter so that when <see cref="ScheduleClearStatus"/> nulls
    /// <see cref="ViewModelBase.StatusMessage"/>, <see cref="ReLearnMessage"/> is also cleared.
    /// </summary>
    public override string? StatusMessage
    {
        get => base.StatusMessage;
        protected set
        {
            base.StatusMessage = value;
            if (value is null)
            {
                _reLearnMessage = null;
                OnPropertyChanged(nameof(ReLearnMessage));
            }
        }
    }

    /// <summary>
    /// True for SD cards and MTP devices — both support drag-and-drop reordering.
    /// Fixed Paths are always sorted by CameraId and do not have a SortOrder in config.
    /// Used to control grip-handle visibility in the shared DeviceEntryTemplate.
    /// </summary>
    public bool IsReorderable =>
        DeviceType == DeviceEntryType.SdCard || DeviceType == DeviceEntryType.MtpDevice;

    private int _sortOrder;
    /// <summary>
    /// Display order within the device group. Persisted via IConfigWriterService.ReorderSdCards
    /// / ReorderMtpDevices after a drag-and-drop reorder.  Not shown in the UI; managed
    /// exclusively by DevicesTabViewModel.
    /// </summary>
    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    /// <summary>
    /// Theme accent key for this device type: "Drone" (SD), "Mirrorless" (MTP), "Action" (FixedPath).
    /// Resolves Brush{Tag} and Color{Tag}Muted from Default.xaml via ThemeAccentBrushConverter.
    /// </summary>
    public string GroupAccentTag { get; }

    /// <summary>Flat list of camera IDs available for assignment (populated by DevicesTabViewModel).</summary>
    public ObservableCollection<string> AvailableCameraIds { get; } = new();

    /// <summary>
    /// Feature flags for the currently assigned camera.
    /// Built from the camera's CameraFeatures + the profile's available features.
    /// Empty when no camera is assigned (Floating SD card).
    /// Populated by RefreshFeatureFlags() — called on construction and when EditCameraId changes.
    /// </summary>
    public ObservableCollection<DeviceFeatureFlagViewModel> FeatureFlags { get; } = new();

    public ICommand ToggleExpandCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RevertCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <summary>Re-learns the VSN from the currently inserted SD card (SD only).</summary>
    public ICommand ReLearnVsnCommand { get; }

    /// <summary>Toggles the history popup open/closed (SD cards only).</summary>
    public ICommand ShowHistoryCommand { get; }

    /// <summary>Raised when the user confirms deletion. Parent handles the actual unregister + save.</summary>
    public event EventHandler<string>? DeleteRequested;

    /// <summary>Raised after a successful Save so the parent can trigger a storage-icon refresh.</summary>
    public event EventHandler? SaveCompleted;

    public DeviceEntryViewModel(
        string key,
        DeviceEntryType deviceType,
        string label,
        string cameraId,
        string? diskSerial,
        long sizeBytes,
        IReadOnlyDictionary<string, DateTime>? lastUsedWith,
        IConfigWriterService configWriter,
        IDriveWatcherService? driveWatcher = null,
        ILogger? logger = null,
        string? firstSeen = null,
        UMI.Core.Services.CameraTypeLoader? typeLoader = null,
        IReadOnlyDictionary<string, int>? usageHistory = null)
    {
        _key = key;
        DeviceType = deviceType;
        _label = label;
        _cameraId = cameraId;
        DiskSerial = diskSerial;
        SizeBytes = sizeBytes;
        LastUsedWith = lastUsedWith;
        UsageHistory = usageHistory;
        _firstSeen = firstSeen;
        _configWriter = configWriter;
        _driveWatcher = driveWatcher;
        _logger = logger;
        _typeLoader = typeLoader;

        UsageEntries = BuildUsageEntries(usageHistory, lastUsedWith);
        TotalImportCount = usageHistory?.Values.Sum() ?? 0;

        GroupAccentTag = deviceType switch
        {
            DeviceEntryType.SdCard    => "Drone",
            DeviceEntryType.MtpDevice => "Mirrorless",
            DeviceEntryType.FixedPath => "Action",
            _                         => "Drone"
        };

        _editLabel = label;
        _editCameraId = cameraId;
        _editKey = key;

        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        StartEditCommand = new RelayCommand(() => IsEditing = true, () => !IsEditing);
        CancelCommand = new RelayCommand(ExecuteRevert);
        SaveCommand = new RelayCommand(ExecuteSave);
        RevertCommand = new RelayCommand(ExecuteRevert);
        DeleteCommand = new RelayCommand(() => DeleteRequested?.Invoke(this, Key));
        ReLearnVsnCommand = new RelayCommand(ExecuteReLearnVsn, () => CanReLearn && !IsRelearning);
        ShowHistoryCommand = new RelayCommand(() => IsHistoryVisible = !IsHistoryVisible);

        RefreshFeatureFlags();
    }

    private async void ExecuteSave()
    {
        if (!IsDirty) { IsEditing = false; return; }

        IsSaving = true;
        SaveStatus = null;

        try
        {

            ApplyEdits();
            await _configWriter.SaveAsync();
            SaveStatus = Strings.Common_Saved;
            IsEditing = false;
            _logger?.LogInformation("Device {Key} saved", Key);
            SaveCompleted?.Invoke(this, EventArgs.Empty);
            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            SaveStatus = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to save device {Key}", Key);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ApplyEdits()
    {
        Label = EditLabel;
        CameraId = EditCameraId;

        switch (DeviceType)
        {
            case DeviceEntryType.SdCard:
                var newVsn = EditKey.Trim();
                if (!string.IsNullOrWhiteSpace(newVsn) && newVsn != Key)
                {

                    _configWriter.UnregisterSdCard(Key);
                    Key = newVsn;
                }

                _configWriter.RegisterSdCard(Key, SdCardRegistrationHelper.Create(
                    CameraId,
                    label: Label,
                    diskSerial: DiskSerial,
                    sizeBytes: SizeBytes,
                    existing: _firstSeen is { Length: > 0 }
                        ? new SdCardRegistration { FirstSeen = _firstSeen, LastUsedWith = new(), UsageHistory = new() }
                        : null));
                break;

            case DeviceEntryType.MtpDevice:

                _configWriter.RegisterMtpDevice(Key,
                    MtpRegistrationHelper.Create(CameraId, Label));
                break;

            case DeviceEntryType.FixedPath:

                break;
        }

        ApplyFeatureFlags();

        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsKeyChanged));
        OnPropertyChanged(nameof(IsCameraEnabled));
        OnPropertyChanged(nameof(IsCameraDisabled));
    }

    /// <summary>
    /// Writes the current FeatureFlags state back to the assigned camera's CameraFeatures.
    /// Called by ApplyEdits() before SaveAsync. No-op when no camera is assigned.
    /// SSOT for feature-key → CameraFeatures property mapping is in WriteFeatureFlagToConfig.
    /// </summary>
    private void ApplyFeatureFlags()
    {
        if (string.IsNullOrEmpty(CameraId)) return;

        var cameras = _configWriter.Config?.Cameras;
        if (cameras is null || !cameras.TryGetValue(CameraId, out var cameraConfig)) return;

        foreach (var flag in FeatureFlags)
            WriteFeatureFlagToConfig(cameraConfig.Features, flag.FeatureKey, flag.IsEnabled);
    }

    /// <summary>
    /// Maps a canonical feature key to its CameraFeatures property and writes the value.
    /// Delegates to CameraFeatures.SetByKey — SSOT for feature-key → property mapping.
    /// </summary>
    private static void WriteFeatureFlagToConfig(UMI.Core.CameraFeatures features, string featureKey, bool value)
    {
        features.SetByKey(featureKey, value);
    }

    private void ExecuteRevert()
    {
        EditLabel = Label;
        EditCameraId = CameraId;
        EditKey = Key;

        foreach (var flag in FeatureFlags)
            flag.Revert();

        IsEditing = false;
    }

    private async void ExecuteReLearnVsn()
    {
        if (_driveWatcher is null) return;

        IsRelearning = true;
        ReLearnMessage = null;

        try
        {
            var drives = await Task.Run(() => _driveWatcher.GetCurrentDrives());

            if (drives.Count == 0)
            {
                ReLearnMessage = Strings.Device_NoDrivesFound;
                ScheduleClearStatus();
                return;
            }

            if (drives.Count == 1)
            {

                var vsn = await Task.Run(() => VolumeInfoReader.GetVolumeSerial(drives[0].DriveLetter));
                if (!string.IsNullOrWhiteSpace(vsn))
                {
                    EditKey = vsn;
                    ReLearnMessage = string.Format(Strings.Device_ReLearnedVsn, vsn);
                }
                else
                {
                    ReLearnMessage = string.Format(Strings.Device_CannotReadVsn, drives[0].DriveLetter);
                }
            }
            else
            {

                string? foundVsn = null;
                foreach (var drive in drives)
                {
                    var vsn = await Task.Run(() => VolumeInfoReader.GetVolumeSerial(drive.DriveLetter));
                    if (vsn == Key)
                    {

                        ReLearnMessage = string.Format(Strings.Device_VsnUnchanged, vsn);
                        ScheduleClearStatus();
                        return;
                    }

                    foundVsn ??= vsn;
                }

                if (!string.IsNullOrWhiteSpace(foundVsn))
                {
                    EditKey = foundVsn;
                    ReLearnMessage = string.Format(Strings.Device_ReLearnedFirstDrive, foundVsn);
                }
                else
                {
                    ReLearnMessage = Strings.Device_CannotReadVsnAny;
                }
            }

            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            ReLearnMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to re-learn VSN for {Key}", Key);
        }
        finally
        {
            IsRelearning = false;
        }
    }

    /// <summary>
    /// Rebuilds the FeatureFlags collection from the currently assigned camera's CameraFeatures.
    /// Uses FeatureLabels.All for canonical order (SSOT — no inline feature lists).
    /// Filters to features that are "available" in the camera's type profile when a CameraTypeLoader
    /// is available; otherwise shows all 9 known features.
    /// Call when CameraId changes or after a config reload.
    /// </summary>
    public void RefreshFeatureFlags()
    {
        FeatureFlags.Clear();

        if (string.IsNullOrEmpty(_cameraId)) return;

        var cameras = _configWriter.Config?.Cameras;
        if (cameras is null || !cameras.TryGetValue(_cameraId, out var cameraConfig)) return;

        var typeDef = _typeLoader?.GetType(cameraConfig.CameraType);
        var features = cameraConfig.Features;

        foreach (var entry in FeatureLabels.All)
        {
            FeatureDefinition? featureDef = null;

            if (typeDef?.Features is not null)
            {
                typeDef.Features.TryGetValue(entry.Key, out featureDef);
                if (featureDef is not null && !featureDef.Available)
                    continue;
            }

            var isEnabled = GetFeatureFlagFromConfig(features, entry.Key);
            var isSimple  = featureDef?.SimpleOnCard ?? false;
            var flagVm    = new DeviceFeatureFlagViewModel(entry.Key, entry.Label, isEnabled, isSimple);
            flagVm.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsDirty));

            FeatureFlags.Add(flagVm);
        }
    }

    /// <summary>
    /// Reads the current value of a feature flag from CameraFeatures by key.
    /// Delegates to CameraFeatures.GetByKey — SSOT for feature-key → property mapping.
    /// </summary>
    private static bool GetFeatureFlagFromConfig(UMI.Core.CameraFeatures features, string featureKey)
        => features.GetByKey(featureKey);

    /// <summary>
    /// Builds the computed usage entries list from SSOT data (SdCardRegistration.UsageHistory + LastUsedWith).
    /// Sorted by import count descending, mirrors CLI CardsCommand history output.
    /// </summary>
    private static IReadOnlyList<CardUsageEntry> BuildUsageEntries(
        IReadOnlyDictionary<string, int>? usageHistory,
        IReadOnlyDictionary<string, DateTime>? lastUsedWith)
    {
        if (usageHistory is null || usageHistory.Count == 0)
            return Array.Empty<CardUsageEntry>();

        var total = usageHistory.Values.Sum();

        return usageHistory
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new CardUsageEntry(
                CameraId: kvp.Key,
                ImportCount: kvp.Value,
                Percent: total > 0 ? kvp.Value * 100.0 / total : 0,
                LastUsed: lastUsedWith is not null && lastUsedWith.TryGetValue(kvp.Key, out var dt)
                    ? dt.ToString("dd.MM.yyyy")
                    : "\u2014"))
            .ToList();
    }
}

/// <summary>
/// Lightweight record for a single usage history entry in the SD card history popup.
/// </summary>
public record CardUsageEntry(string CameraId, int ImportCount, double Percent, string LastUsed)
{
    /// <summary>Formatted percentage string for display binding.</summary>
    public string PercentDisplay => $"{Percent:F0}%";
}
