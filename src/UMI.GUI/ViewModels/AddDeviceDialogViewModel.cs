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
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Represents a single detected (not yet registered) device in the Add Device dialog.
/// </summary>
public class DetectedDeviceViewModel : ViewModelBase
{

    /// <summary>Registry key: VolumeSerial for SD, SerialNumber for MTP, path for FixedPath.</summary>
    public string Key { get; }

    /// <summary>Original path or ID used for detection (e.g. "F:\" or MTP Device ID).</summary>
    public string SourcePath { get; }

    public DeviceEntryType DeviceType { get; }

    /// <summary>Display name: VolumeLabel / FriendlyName / path.</summary>
    public string DisplayName { get; }

    /// <summary>Sub-info line: drive letter + size for SD, Manufacturer/Model for MTP.</summary>
    public string SubInfo { get; }

    /// <summary>True when this device is already registered in config.</summary>
    public bool IsAlreadyRegistered { get; }

    private string? _selectedCameraId = null;
    public string? SelectedCameraId
    {
        get => _selectedCameraId;
        set
        {
            if (SetProperty(ref _selectedCameraId, value))
                OnPropertyChanged(nameof(CanRegister));
        }
    }

    /// <summary>True when SelectedCameraId represents a floating assignment (empty string, not null).</summary>
    public bool IsFloating => _selectedCameraId == string.Empty;

    public bool IsFixed => !IsFloating;

    /// <summary>Allow register when user has actively selected an option (null = nothing chosen yet).</summary>
    public bool CanRegister => _selectedCameraId is not null && !IsAlreadyRegistered;

    public ICommand RegisterCommand { get; }

    public event EventHandler? RegisterRequested;

    public ObservableCollection<string> AvailableCameraIds { get; } = new();

    public DetectedDeviceViewModel(
        string key,
        string sourcePath,
        DeviceEntryType deviceType,
        string displayName,
        string subInfo,
        bool isAlreadyRegistered,
        IEnumerable<string> availableCameraIds,
        string? suggestedCameraId = null)
    {
        Key = key;
        SourcePath = sourcePath;
        DeviceType = deviceType;
        DisplayName = displayName;
        SubInfo = subInfo;
        IsAlreadyRegistered = isAlreadyRegistered;

        if (deviceType == DeviceEntryType.SdCard)
            AvailableCameraIds.Add(string.Empty);

        foreach (var id in availableCameraIds)
            AvailableCameraIds.Add(id);

        if (suggestedCameraId is not null && AvailableCameraIds.Contains(suggestedCameraId))
            _selectedCameraId = suggestedCameraId;

        RegisterCommand = new RelayCommand(
            () => RegisterRequested?.Invoke(this, EventArgs.Empty),
            () => CanRegister);
    }
}

/// <summary>
/// ViewModel for the Add Device modal dialog.
/// Manages live SD-card detection (DriveWatcherService events) + MTP snapshot detection,
/// and orchestrates device registration via IConfigWriterService.
/// </summary>
public class AddDeviceDialogViewModel : ViewModelBase, IDisposable
{
    private readonly IConfigWriterService _configWriter;
    private readonly IDriveWatcherService _driveWatcher;
    private readonly IMtpDeviceDetectionService _mtpDetection;
    private readonly IReadOnlyList<string> _cameraIds;
    private readonly ILogger? _logger;
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<DetectedDeviceViewModel> DetectedSdCards { get; } = new();
    public ObservableCollection<DetectedDeviceViewModel> DetectedMtpDevices { get; } = new();

    private string _fixedPath = string.Empty;
    public string FixedPath
    {
        get => _fixedPath;
        set
        {
            if (SetProperty(ref _fixedPath, value))
                OnPropertyChanged(nameof(CanRegisterFixedPath));
        }
    }

    private string _fixedPathCameraId = string.Empty;
    public string FixedPathCameraId
    {
        get => _fixedPathCameraId;
        set
        {
            if (SetProperty(ref _fixedPathCameraId, value))
                OnPropertyChanged(nameof(CanRegisterFixedPath));
        }
    }

    public bool CanRegisterFixedPath =>
        !string.IsNullOrWhiteSpace(FixedPath) &&
        !string.IsNullOrWhiteSpace(FixedPathCameraId);

    public ObservableCollection<string> AvailableCameraIds { get; } = new();

    private bool _isRefreshingMtp;
    public bool IsRefreshingMtp
    {
        get => _isRefreshingMtp;
        private set => SetProperty(ref _isRefreshingMtp, value);
    }

    public ICommand RefreshMtpCommand { get; }
    public ICommand BrowseFixedPathCommand { get; }
    public ICommand RegisterFixedPathCommand { get; }
    public ICommand CloseCommand { get; }

    /// <summary>Raised when the dialog should close.</summary>
    public event EventHandler? CloseRequested;

    public AddDeviceDialogViewModel(
        IConfigWriterService configWriter,
        IDriveWatcherService driveWatcher,
        IMtpDeviceDetectionService mtpDetection,
        IReadOnlyList<string> cameraIds,
        ILogger? logger = null)
    {
        _configWriter = configWriter;
        _driveWatcher = driveWatcher;
        _mtpDetection = mtpDetection;
        _cameraIds = cameraIds;
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;

        foreach (var id in cameraIds)
            AvailableCameraIds.Add(id);

        RefreshMtpCommand = new RelayCommand(ExecuteRefreshMtp, () => !IsRefreshingMtp);
        BrowseFixedPathCommand = new RelayCommand(ExecuteBrowseFixedPath);
        RegisterFixedPathCommand = new RelayCommand(ExecuteRegisterFixedPath, () => CanRegisterFixedPath);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// Starts live detection. Call after dialog is loaded.
    /// Subscribes to DriveWatcher events and does initial SD + MTP scan.
    /// </summary>
    public void StartDetection()
    {

        _driveWatcher.DriveArrived += OnDriveArrived;
        _driveWatcher.DriveRemoved += OnDriveRemoved;

        if (!_driveWatcher.IsWatching)
            _driveWatcher.StartWatching();

        RefreshSdCards();

        RefreshMtp();
    }

    /// <summary>
    /// Stops live detection. Call on dialog close / Dispose.
    /// </summary>
    public void StopDetection()
    {
        _driveWatcher.DriveArrived -= OnDriveArrived;
        _driveWatcher.DriveRemoved -= OnDriveRemoved;

    }

    private async void RefreshSdCards()
    {
        var currentDrives = _driveWatcher.GetCurrentDrives();

        var entries = await Task.Run(() =>
        {
            var registeredKeys = _configWriter.Config.SdCards;
            return currentDrives.Select(drive =>
            {
                var registryKey = VolumeInfoReader.GetRegistryKey(drive.DriveLetter);
                var isRegistered = registeredKeys.ContainsKey(registryKey);
                var label = VolumeInfoReader.GetVolumeLabel(drive.DriveLetter);
                var displayLabel = label ?? drive.DriveLetter;
                return (drive, registryKey, isRegistered, displayLabel);
            }).ToList();
        });

        _dispatcher.Invoke(() =>
        {
            DetectedSdCards.Clear();
            foreach (var (drive, registryKey, isRegistered, displayLabel) in entries)
            {
                var vm = BuildSdCardDetectedVm(
                    key: registryKey,
                    sourcePath: drive.DriveLetter,
                    displayName: displayLabel,
                    subInfo: $"{drive.DriveLetter}  ·  {FormatHelper.FormatBytes(drive.TotalSizeBytes)}",
                    isAlreadyRegistered: isRegistered);
                DetectedSdCards.Add(vm);
            }
        });
    }

    private void OnDriveArrived(object? sender, DriveChangedEventArgs e)
    {

        _ = Task.Run(() =>
        {
            var registryKey = VolumeInfoReader.GetRegistryKey(e.DriveLetter);
            var isRegistered = _configWriter.Config.SdCards.ContainsKey(registryKey);
            var label = VolumeInfoReader.GetVolumeLabel(e.DriveLetter);
            var displayLabel = label ?? e.DriveLetter;

            _dispatcher.Invoke(() =>
            {

                if (DetectedSdCards.Any(d => d.Key == registryKey)) return;

                var vm = BuildSdCardDetectedVm(
                    key: registryKey,
                    sourcePath: e.DriveLetter,
                    displayName: displayLabel,
                    subInfo: $"{e.DriveLetter}  ·  {FormatHelper.FormatBytes(e.TotalSizeBytes)}",
                    isAlreadyRegistered: isRegistered);

                DetectedSdCards.Add(vm);
                _logger?.LogInformation("Add Device Dialog: drive arrived {Drive} (vsn={Vsn}, registered={Registered})",
                    e.DriveLetter, registryKey, isRegistered);
            });
        });
    }

    private void OnDriveRemoved(object? sender, DriveChangedEventArgs e)
    {

        _dispatcher.Invoke(() =>
        {
            var toRemove = DetectedSdCards.FirstOrDefault(d => d.Key == e.DriveLetter);
            if (toRemove is not null)
                DetectedSdCards.Remove(toRemove);

            _logger?.LogInformation("Add Device Dialog: drive removed {Drive}", e.DriveLetter);
        });
    }

    private DetectedDeviceViewModel BuildSdCardDetectedVm(
        string key,
        string sourcePath,
        string displayName,
        string subInfo,
        bool isAlreadyRegistered)
    {
        var vm = new DetectedDeviceViewModel(
            key: key,
            sourcePath: sourcePath,
            deviceType: DeviceEntryType.SdCard,
            displayName: displayName,
            subInfo: subInfo,
            isAlreadyRegistered: isAlreadyRegistered,
            availableCameraIds: _cameraIds);

        vm.RegisterRequested += OnSdCardRegisterRequested;
        return vm;
    }

    private async void OnSdCardRegisterRequested(object? sender, EventArgs e)
    {
        if (sender is not DetectedDeviceViewModel detected) return;
        if (detected.SelectedCameraId is null) return;

        try
        {

            var info = await Task.Run(() => VolumeInfoReader.ReadSdCardInfo(detected.SourcePath, _logger));

            var registryKey = !string.IsNullOrWhiteSpace(info.VolumeSerial) ? info.VolumeSerial : detected.Key;

            var sdCameraId = detected.SelectedCameraId ?? string.Empty;
            var reg = SdCardRegistrationHelper.Create(
                sdCameraId,
                label: detected.DisplayName,
                diskSerial: VolumeInfoReader.IsFakeDiskSerial(info.DiskSerial) ? null : info.DiskSerial,
                sizeBytes: info.DiskSizeBytes);

            _configWriter.RegisterSdCard(registryKey, reg);
            await _configWriter.SaveAsync();

            var vsnInfo = registryKey != detected.Key ? $" (VSN: {registryKey})" : string.Empty;
            var assignmentLabel = string.IsNullOrEmpty(sdCameraId) ? "floating" : sdCameraId;
            StatusMessage = string.Format(Strings.AddDevice_SdRegistered, detected.DisplayName, assignmentLabel, vsnInfo);
            _logger?.LogInformation("SD card registered: {Key} → {Camera} (drive: {Drive})", registryKey, detected.SelectedCameraId, detected.Key);

            var idx = DetectedSdCards.IndexOf(detected);
            if (idx >= 0)
            {
                detected.RegisterRequested -= OnSdCardRegisterRequested;
                var updated = new DetectedDeviceViewModel(
                    key: detected.Key,
                    sourcePath: detected.SourcePath,
                    deviceType: DeviceEntryType.SdCard,
                    displayName: detected.DisplayName,
                    subInfo: detected.SubInfo,
                    isAlreadyRegistered: true,
                    availableCameraIds: _cameraIds,
                    suggestedCameraId: detected.SelectedCameraId);
                DetectedSdCards[idx] = updated;
            }

            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to register SD card {Key}", detected.Key);
        }
    }

    private async void ExecuteRefreshMtp()
    {
        IsRefreshingMtp = true;
        try
        {

            await _dispatcher.InvokeAsync(RefreshMtp);
        }
        finally
        {
            IsRefreshingMtp = false;
        }
    }

    private void RefreshMtp()
    {

        try
        {
            var results = _mtpDetection.DetectDevices();
            var registeredMtp = _configWriter.Config.MtpDevices;

            DetectedMtpDevices.Clear();

            foreach (var result in results)
            {
                var key = MtpDeviceDetectionService.GetDeviceKey(result.Device);
                var isRegistered = registeredMtp.ContainsKey(key);

                var vm = new DetectedDeviceViewModel(
                    key: key,
                    sourcePath: result.Device.DeviceId,
                    deviceType: DeviceEntryType.MtpDevice,
                    displayName: result.Device.FriendlyName,
                    subInfo: $"{result.Device.Manufacturer ?? "Unknown"} · {result.Device.Model ?? "–"}",
                    isAlreadyRegistered: isRegistered,
                    availableCameraIds: _cameraIds,
                    suggestedCameraId: result.CameraId);

                vm.RegisterRequested += OnMtpRegisterRequested;
                DetectedMtpDevices.Add(vm);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "MTP scan failed");
        }
    }

    private async void OnMtpRegisterRequested(object? sender, EventArgs e)
    {
        if (sender is not DetectedDeviceViewModel detected) return;
        if (string.IsNullOrWhiteSpace(detected.SelectedCameraId)) return;

        try
        {
            var reg = MtpRegistrationHelper.Create(
                detected.SelectedCameraId, detected.DisplayName);

            _configWriter.RegisterMtpDevice(detected.Key, reg);
            await _configWriter.SaveAsync();

            StatusMessage = string.Format(Strings.AddDevice_MtpRegistered, detected.DisplayName, detected.SelectedCameraId);
            _logger?.LogInformation("MTP device registered: {Key} → {Camera}", detected.Key, detected.SelectedCameraId);

            var idx = DetectedMtpDevices.IndexOf(detected);
            if (idx >= 0)
            {
                detected.RegisterRequested -= OnMtpRegisterRequested;
                var updated = new DetectedDeviceViewModel(
                    key: detected.Key,
                    sourcePath: detected.SourcePath,
                    deviceType: DeviceEntryType.MtpDevice,
                    displayName: detected.DisplayName,
                    subInfo: detected.SubInfo,
                    isAlreadyRegistered: true,
                    availableCameraIds: _cameraIds,
                    suggestedCameraId: detected.SelectedCameraId);
                DetectedMtpDevices[idx] = updated;
            }

            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to register MTP device {Key}", detected.Key);
        }
    }

    private void ExecuteBrowseFixedPath()
    {
        var folder = Helpers.DialogHelper.BrowseFolder(Strings.AddDevice_SelectSourceFolder, FixedPath);
        if (folder is not null)
            FixedPath = folder;
    }

    private async void ExecuteRegisterFixedPath()
    {
        if (!CanRegisterFixedPath) return;

        try
        {

            _configWriter.UpdateCamera(FixedPathCameraId, cam =>
            {
                cam.SourceType = UMI.Core.SourceType.FixedPath;
                cam.SourcePath = FixedPath;
            });

            await _configWriter.SaveAsync();
            StatusMessage = string.Format(Strings.AddDevice_FixedPathRegistered, FixedPathCameraId);
            _logger?.LogInformation("Fixed path registered: {Path} → {Camera}", FixedPath, FixedPathCameraId);

            FixedPath = string.Empty;
            FixedPathCameraId = string.Empty;
            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to register fixed path");
        }
    }

    public void Dispose()
    {
        StopDetection();

        foreach (var vm in DetectedSdCards)
            vm.RegisterRequested -= OnSdCardRegisterRequested;
        foreach (var vm in DetectedMtpDevices)
            vm.RegisterRequested -= OnMtpRegisterRequested;

        CancelScheduledClear();
        GC.SuppressFinalize(this);
    }
}
