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
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.GUI.Resources;
using UMI.GUI.Views;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Devices sub-tab in Settings.
/// Groups registered devices by type (SD Cards, MTP Devices, Fixed Paths).
/// Supports inline expand/edit, delete with confirmation, and launching the Add Device dialog.
/// </summary>
public class DevicesTabViewModel : ViewModelBase
{
    private readonly IConfigWriterService _configWriter;
    private readonly IDriveWatcherService _driveWatcher;
    private readonly IMtpDeviceDetectionService _mtpDetection;
    private readonly ILogger<DevicesTabViewModel>? _logger;
    private readonly CameraTypeLoader _typeLoader;

    public ObservableCollection<DeviceEntryViewModel> SdCards { get; } = new();
    public ObservableCollection<DeviceEntryViewModel> MtpDevices { get; } = new();
    public ObservableCollection<DeviceEntryViewModel> FixedPaths { get; } = new();

    public int SdCardCount => SdCards.Count;
    public int MtpDeviceCount => MtpDevices.Count;
    public int FixedPathCount => FixedPaths.Count;

    /// <summary>
    /// Set by MainViewModel after init. Fired after any device assignment change so
    /// CameraViewModel.RefreshStorageSummary() can be called on all cameras.
    /// </summary>
    public Action? OnDeviceAssignmentsChanged { get; set; }

    private readonly List<string> _cameraIds = new();

    private string? _pendingDeleteKey;
    private DeviceEntryType _pendingDeleteType;

    private bool _showDeleteConfirm;
    public bool ShowDeleteConfirm
    {
        get => _showDeleteConfirm;
        private set => SetProperty(ref _showDeleteConfirm, value);
    }

    private string _deleteConfirmLabel = string.Empty;
    public string DeleteConfirmLabel
    {
        get => _deleteConfirmLabel;
        private set => SetProperty(ref _deleteConfirmLabel, value);
    }

    public ICommand AddDeviceCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }

    /// <summary>
    /// Reorders SD card entries after a drag-and-drop operation.
    /// CommandParameter: (int OldIndex, int NewIndex) tuple from DragDropReorderBehavior.
    /// </summary>
    public ICommand ReorderSdCardsCommand { get; }

    /// <summary>
    /// Reorders MTP device entries after a drag-and-drop operation.
    /// CommandParameter: (int OldIndex, int NewIndex) tuple from DragDropReorderBehavior.
    /// </summary>
    public ICommand ReorderMtpDevicesCommand { get; }

    public DevicesTabViewModel(
        IConfigWriterService configWriter,
        IDriveWatcherService driveWatcher,
        IMtpDeviceDetectionService mtpDetection,
        CameraTypeLoader typeLoader,
        ILogger<DevicesTabViewModel>? logger = null)
    {
        _configWriter = configWriter;
        _driveWatcher = driveWatcher;
        _mtpDetection = mtpDetection;
        _logger = logger;
        _typeLoader = typeLoader;

        AddDeviceCommand = new RelayCommand(ExecuteAddDevice);
        ConfirmDeleteCommand = new RelayCommand(ExecuteConfirmDelete);
        CancelDeleteCommand = new RelayCommand(() => ShowDeleteConfirm = false);
        ReorderSdCardsCommand = new RelayCommand<(int, int)>(ExecuteReorderSdCards);
        ReorderMtpDevicesCommand = new RelayCommand<(int, int)>(ExecuteReorderMtpDevices);
    }

    /// <summary>
    /// Populates device lists from the current config.
    /// Call after config is loaded (from MainViewModel.LoadAsync or DevicesTabViewModel creation).
    /// </summary>
    public void Initialize()
    {
        var config = _configWriter.Config;
        if (config is null) return;

        _cameraIds.Clear();
        foreach (var id in config.Cameras.Keys.OrderBy(k => k))
            _cameraIds.Add(id);

        LoadSdCards(config);
        LoadMtpDevices(config);
        LoadFixedPaths(config);

        OnPropertyChanged(nameof(SdCardCount));
        OnPropertyChanged(nameof(MtpDeviceCount));
        OnPropertyChanged(nameof(FixedPathCount));
    }

    private void LoadSdCards(UMI.Core.Configuration.UmiConfig config)
    {
        SdCards.Clear();

        foreach (var (vsn, reg) in config.SdCards
            .OrderBy(kv => kv.Value.SortOrder)
            .ThenBy(kv => kv.Value.Label ?? kv.Key))
        {
            var entry = CreateSdCardEntry(vsn, reg);
            SdCards.Add(entry);
        }
    }

    private void LoadMtpDevices(UMI.Core.Configuration.UmiConfig config)
    {
        MtpDevices.Clear();

        foreach (var (serial, reg) in config.MtpDevices
            .OrderBy(kv => kv.Value.SortOrder)
            .ThenBy(kv => kv.Value.Label ?? kv.Key))
        {
            var entry = CreateMtpEntry(serial, reg);
            MtpDevices.Add(entry);
        }
    }

    private DeviceEntryViewModel CreateSdCardEntry(string vsn, SdCardRegistration reg)
    {
        var entry = new DeviceEntryViewModel(
            key: vsn,
            deviceType: DeviceEntryType.SdCard,
            label: reg.Label ?? vsn,
            cameraId: reg.CameraId,
            diskSerial: reg.DiskSerial,
            sizeBytes: reg.SizeBytes,
            lastUsedWith: reg.LastUsedWith,
            configWriter: _configWriter,
            driveWatcher: _driveWatcher,
            logger: _logger,
            firstSeen: reg.FirstSeen,
            typeLoader: _typeLoader,
            usageHistory: reg.UsageHistory)
        {
            SortOrder = reg.SortOrder
        };

        PopulateCameraIds(entry);
        entry.DeleteRequested += OnDeleteRequested;
        entry.SaveCompleted += OnEntrySaveCompleted;
        return entry;
    }

    private DeviceEntryViewModel CreateMtpEntry(string serial, MtpDeviceRegistration reg)
    {
        var entry = new DeviceEntryViewModel(
            key: serial,
            deviceType: DeviceEntryType.MtpDevice,
            label: reg.Label ?? serial,
            cameraId: reg.CameraId,
            diskSerial: null,
            sizeBytes: 0,
            lastUsedWith: null,
            configWriter: _configWriter,
            driveWatcher: null,
            logger: _logger,
            typeLoader: _typeLoader)
        {
            SortOrder = reg.SortOrder
        };

        PopulateCameraIds(entry);
        entry.DeleteRequested += OnDeleteRequested;
        entry.SaveCompleted += OnEntrySaveCompleted;
        return entry;
    }

    private void LoadFixedPaths(UMI.Core.Configuration.UmiConfig config)
    {
        FixedPaths.Clear();

        foreach (var (cameraId, camera) in config.Cameras
            .Where(kv => kv.Value.SourceType == UMI.Core.SourceType.FixedPath)
            .OrderBy(kv => kv.Key))
        {
            var entry = CreateFixedPathEntry(cameraId, camera);
            FixedPaths.Add(entry);
        }
    }

    private DeviceEntryViewModel CreateFixedPathEntry(string cameraId, UMI.Core.CameraConfig camera)
    {
        var entry = new DeviceEntryViewModel(
            key: cameraId,
            deviceType: DeviceEntryType.FixedPath,
            label: camera.SourcePath ?? camera.Name ?? cameraId,
            cameraId: cameraId,
            diskSerial: null,
            sizeBytes: 0,
            lastUsedWith: null,
            configWriter: _configWriter,
            driveWatcher: null,
            logger: _logger,
            typeLoader: _typeLoader);

        PopulateCameraIds(entry);
        entry.DeleteRequested += OnDeleteRequested;
        entry.SaveCompleted += OnEntrySaveCompleted;
        return entry;
    }

    private void PopulateCameraIds(DeviceEntryViewModel entry)
    {
        entry.AvailableCameraIds.Clear();

        if (entry.DeviceType == DeviceEntryType.SdCard)
            entry.AvailableCameraIds.Add(string.Empty);
        foreach (var id in _cameraIds)
            entry.AvailableCameraIds.Add(id);
    }

    private void OnEntrySaveCompleted(object? sender, EventArgs e)
    {
        OnDeviceAssignmentsChanged?.Invoke();
    }

    private void OnDeleteRequested(object? sender, string key)
    {
        if (sender is not DeviceEntryViewModel entry) return;

        _pendingDeleteKey = key;
        _pendingDeleteType = entry.DeviceType;
        DeleteConfirmLabel = string.Format(Strings.Devices_RemoveConfirm, entry.Label);
        ShowDeleteConfirm = true;
    }

    private async void ExecuteConfirmDelete()
    {
        if (_pendingDeleteKey is null)
        {
            ShowDeleteConfirm = false;
            return;
        }

        try
        {
            switch (_pendingDeleteType)
            {
                case DeviceEntryType.SdCard:
                    _configWriter.UnregisterSdCard(_pendingDeleteKey);
                    var sdToRemove = SdCards.FirstOrDefault(e => e.Key == _pendingDeleteKey);
                    if (sdToRemove is not null)
                    {
                        sdToRemove.DeleteRequested -= OnDeleteRequested;
                        SdCards.Remove(sdToRemove);
                    }
                    break;

                case DeviceEntryType.MtpDevice:
                    _configWriter.UnregisterMtpDevice(_pendingDeleteKey);
                    var mtpToRemove = MtpDevices.FirstOrDefault(e => e.Key == _pendingDeleteKey);
                    if (mtpToRemove is not null)
                    {
                        mtpToRemove.DeleteRequested -= OnDeleteRequested;
                        MtpDevices.Remove(mtpToRemove);
                    }
                    break;

                case DeviceEntryType.FixedPath:
                    var fpToRemove = FixedPaths.FirstOrDefault(e => e.Key == _pendingDeleteKey);
                    if (fpToRemove is not null)
                    {
                        fpToRemove.DeleteRequested -= OnDeleteRequested;
                        FixedPaths.Remove(fpToRemove);
                    }
                    break;
            }

            await _configWriter.SaveAsync();
            StatusMessage = Strings.Devices_Removed;
            OnPropertyChanged(nameof(SdCardCount));
            OnPropertyChanged(nameof(MtpDeviceCount));
            OnPropertyChanged(nameof(FixedPathCount));
            _logger?.LogInformation("Device removed: {Key} ({Type})", _pendingDeleteKey, _pendingDeleteType);
            OnDeviceAssignmentsChanged?.Invoke();
            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to remove device {Key}", _pendingDeleteKey);
        }
        finally
        {
            _pendingDeleteKey = null;
            ShowDeleteConfirm = false;
        }
    }

    private void ExecuteAddDevice()
    {
        try
        {
            var owner = Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault();

            var dialogVm = new AddDeviceDialogViewModel(
                _configWriter,
                _driveWatcher,
                _mtpDetection,
                _cameraIds,
                _logger as ILogger);

            var dialog = new AddDeviceDialog(dialogVm)
            {
                Owner = owner
            };

            dialog.ShowDialog();

            Initialize();

            OnDeviceAssignmentsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open Add Device dialog");
        }
    }

    /// <summary>
    /// Adds a newly registered SD card to the Devices tab.
    /// Called after FloatingCardDialog registers an unknown card.
    /// If an entry with the same VSN already exists, it is not duplicated.
    /// </summary>
    public void AddSdCardEntry(string vsn, SdCardRegistration registration, string? driveLetter = null)
    {

        if (SdCards.Any(e => string.Equals(e.Key, vsn, StringComparison.OrdinalIgnoreCase)))
        {

            UpdateCardStatus(vsn, driveLetter ?? "", true);
            return;
        }

        var entry = CreateSdCardEntry(vsn, registration);
        if (driveLetter is not null)
        {
            entry.IsCardConnected = true;
            entry.ConnectedDriveLetter = driveLetter;
        }
        SdCards.Add(entry);
        OnPropertyChanged(nameof(SdCardCount));
    }

    /// <summary>
    /// Updates card connection status for the SD card entry matching the given VSN.
    /// Called by ImportViewModel when DriveWatcher detects card changes.
    /// </summary>
    public void UpdateCardStatus(string? vsn, string driveLetter, bool isConnected)
    {
        if (vsn is null) return;

        var entry = SdCards.FirstOrDefault(e =>
            string.Equals(e.Key, vsn, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return;

        entry.IsCardConnected = isConnected;
        entry.ConnectedDriveLetter = isConnected ? driveLetter : null;
    }

    /// <summary>
    /// Updates MTP device connection status. Must be called on the UI thread.
    /// Finds the device entry matching <paramref name="deviceKey"/> and updates its
    /// <see cref="DeviceEntryViewModel.IsCardConnected"/> and
    /// <see cref="DeviceEntryViewModel.ConnectedDriveLetter"/> properties.
    /// Called by ImportViewModel's MTP polling loop when a device connects or disconnects.
    /// friendlyName holds the device's FriendlyName (used as the tooltip label).
    /// </summary>
    public void UpdateMtpStatus(string deviceKey, string? friendlyName, bool isConnected)
    {
        var entry = MtpDevices.FirstOrDefault(e =>
            string.Equals(e.Key, deviceKey, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return;

        entry.IsCardConnected = isConnected;
        entry.ConnectedDriveLetter = isConnected ? friendlyName : null;
    }

    private async void ExecuteReorderSdCards((int OldIndex, int NewIndex) indices)
    {
        var (oldIndex, newIndex) = indices;
        if (oldIndex == newIndex) return;
        if (oldIndex < 0 || oldIndex >= SdCards.Count) return;
        if (newIndex < 0 || newIndex >= SdCards.Count) return;

        SdCards.Move(oldIndex, newIndex);

        var orderedVsns = SdCards.Select(e => e.Key).ToList();

        try
        {
            _configWriter.ReorderSdCards(orderedVsns);
            await _configWriter.SaveAsync();
            _logger?.LogInformation("SD cards reordered");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reordering SD cards");
        }
    }

    private async void ExecuteReorderMtpDevices((int OldIndex, int NewIndex) indices)
    {
        var (oldIndex, newIndex) = indices;
        if (oldIndex == newIndex) return;
        if (oldIndex < 0 || oldIndex >= MtpDevices.Count) return;
        if (newIndex < 0 || newIndex >= MtpDevices.Count) return;

        MtpDevices.Move(oldIndex, newIndex);

        var orderedKeys = MtpDevices.Select(e => e.Key).ToList();

        try
        {
            _configWriter.ReorderMtpDevices(orderedKeys);
            await _configWriter.SaveAsync();
            _logger?.LogInformation("MTP devices reordered");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reordering MTP devices");
        }
    }

}
