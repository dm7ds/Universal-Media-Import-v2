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
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using UMI.Core;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Add Camera dialog.
/// Lets the user pick a camera profile (CameraType) from installed .umi definitions
/// and enter a unique Camera ID. On confirmation, adds the camera to config and saves.
///
/// Camera detection on connected drives is delegated to <see cref="ICardDetectionService"/> (SSOT).
/// When detection returns a SuggestedCameraType, the profile ComboBox is auto-selected.
/// </summary>
public class AddCameraDialogViewModel : ViewModelBase, IDisposable
{
    private readonly IConfigWriterService _configWriter;
    private readonly CameraTypeLoader _typeLoader;
    private readonly BurstProfileLoader? _burstProfileLoader;
    private readonly IDriveWatcherService? _driveWatcher;
    private readonly ICardDetectionService? _cardDetection;
    private readonly ILogger? _logger;
    private readonly Dispatcher _dispatcher;

    private bool _userHasPickedProfile;

    private string _cameraId = string.Empty;
    /// <summary>The camera ID entered by the user. Must be unique and non-empty.</summary>
    public string CameraId
    {
        get => _cameraId;
        set
        {
            if (SetProperty(ref _cameraId, value))
            {
                OnPropertyChanged(nameof(CanAdd));
                OnPropertyChanged(nameof(CameraIdError));
            }
        }
    }

    /// <summary>Validation error for the Camera ID field (null when valid).</summary>
    public string? CameraIdError
    {
        get
        {
            var id = _cameraId.Trim();
            if (string.IsNullOrEmpty(id)) return null;
            if (_configWriter.Config.Cameras.ContainsKey(id)) return string.Format(Strings.AddCamera_IdAlreadyInUse, id);
            if (id.Contains(' ')) return Strings.AddCamera_IdNoSpaces;
            return null;
        }
    }

    private string _cameraName = string.Empty;
    /// <summary>Display name for the camera (defaults to Camera ID when empty).</summary>
    public string CameraName
    {
        get => _cameraName;
        set => SetProperty(ref _cameraName, value);
    }

    /// <summary>Available camera profiles loaded from CameraTypeLoader.</summary>
    public ObservableCollection<CameraProfileItem> AvailableProfiles { get; } = new();

    private CameraProfileItem? _selectedProfile;
    /// <summary>The profile the user has selected from the dropdown.</summary>
    public CameraProfileItem? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {

                foreach (var p in AvailableProfiles)
                    p.IsSelected = ReferenceEquals(p, value);

                OnPropertyChanged(nameof(CanAdd));
                OnPropertyChanged(nameof(SelectedProfileDescription));
            }
        }
    }

    /// <summary>Description of the selected profile (shown as hint text).</summary>
    public string SelectedProfileDescription =>
        SelectedProfile?.Description ?? Strings.AddCamera_SelectProfileHint;

    /// <summary>
    /// Currently connected removable drives that are NOT yet registered in config.SdCards.
    /// Shown below the profile picker so the user can immediately assign an SD card
    /// when adding a camera. Updates live via DriveWatcher events.
    /// Includes suggested camera type from CardDetectionService for UI display.
    /// </summary>
    public ObservableCollection<NewCameraDriveItem> DetectedUnregisteredDrives { get; } = new();

    /// <summary>True when at least one unregistered drive is detected.</summary>
    public bool HasDetectedDrives => DetectedUnregisteredDrives.Count > 0;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanAdd));
        }
    }

    /// <summary>True when user can click Add.</summary>
    public bool CanAdd =>
        !string.IsNullOrWhiteSpace(_cameraId) &&
        !_configWriter.Config.Cameras.ContainsKey(_cameraId.Trim()) &&
        !_cameraId.Contains(' ') &&
        SelectedProfile is not null &&
        !IsBusy;

    public ICommand AddCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SelectProfileCommand { get; }

    /// <summary>Raised after a camera was successfully added (dialog stays open for batch adding).</summary>
    public event EventHandler? CameraAdded;

    /// <summary>Raised when the dialog should close.</summary>
    public event EventHandler? CloseRequested;

    public AddCameraDialogViewModel(
        IConfigWriterService configWriter,
        CameraTypeLoader typeLoader,
        IDriveWatcherService? driveWatcher = null,
        ICardDetectionService? cardDetection = null,
        BurstProfileLoader? burstProfileLoader = null,
        ILogger? logger = null)
    {
        _configWriter = configWriter;
        _typeLoader = typeLoader;
        _burstProfileLoader = burstProfileLoader;
        _driveWatcher = driveWatcher;
        _cardDetection = cardDetection;
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;

        AddCommand = new RelayCommand(ExecuteAdd, () => CanAdd);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        SelectProfileCommand = new RelayCommand<CameraProfileItem>(p =>
        {
            if (p is not null)
            {
                _userHasPickedProfile = true;
                SelectedProfile = p;
            }
        });

        LoadProfiles();
        LoadDetectedDrives();

        if (_driveWatcher is not null)
        {
            _driveWatcher.DriveArrived += OnDriveArrived;
            _driveWatcher.DriveRemoved += OnDriveRemoved;
            if (!_driveWatcher.IsWatching)
                _driveWatcher.StartWatching();
        }
    }

    private void LoadProfiles()
    {
        AvailableProfiles.Clear();

        foreach (var profile in CameraSetupHelpers.LoadProfiles(_typeLoader))
            AvailableProfiles.Add(profile);

        if (AvailableProfiles.Count > 0)
            SelectedProfile = AvailableProfiles[0];
    }

    private async void LoadDetectedDrives()
    {
        try
        {
            DetectedUnregisteredDrives.Clear();

            if (_driveWatcher is null) return;

            var drives = _driveWatcher.GetCurrentDrives();

            var unregistered = await Task.Run(() =>
            {
                var registeredKeys = _configWriter.Config.SdCards;
                return drives
                    .Select(drive =>
                    {
                        var registryKey = VolumeInfoReader.GetRegistryKey(drive.DriveLetter);
                        var label = VolumeInfoReader.GetVolumeLabel(drive.DriveLetter);
                        var displayLabel = label ?? drive.DriveLetter;
                        return (drive, registryKey, isRegistered: registeredKeys.ContainsKey(registryKey), displayLabel);
                    })
                    .Where(t => !t.isRegistered)
                    .ToList();
            });

            foreach (var (drive, _, _, displayLabel) in unregistered)
            {
                await AddDriveItemWithDetectionAsync(
                    drive.DriveLetter,
                    drive.RootPath,
                    displayLabel,
                    drive.VolumeLabel,
                    drive.TotalSizeBytes);
            }

            OnPropertyChanged(nameof(HasDetectedDrives));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Add Camera: failed to load detected drives");
        }
    }

    private void OnDriveArrived(object? sender, DriveChangedEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            var registryKey = VolumeInfoReader.GetRegistryKey(e.DriveLetter);
            var isRegistered = _configWriter.Config.SdCards.ContainsKey(registryKey);
            var label = VolumeInfoReader.GetVolumeLabel(e.DriveLetter);
            var displayLabel = label ?? e.DriveLetter;

            if (isRegistered) return;

            _dispatcher.Invoke(() =>
            {
                if (DetectedUnregisteredDrives.Any(d => d.DriveLetter == e.DriveLetter)) return;
            });

            await AddDriveItemWithDetectionAsync(
                e.DriveLetter,
                e.RootPath,
                displayLabel,
                e.VolumeLabel,
                e.TotalSizeBytes);

            _dispatcher.Invoke(() => OnPropertyChanged(nameof(HasDetectedDrives)));
        });
    }

    private void OnDriveRemoved(object? sender, DriveChangedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            var item = DetectedUnregisteredDrives.FirstOrDefault(d => d.DriveLetter == e.DriveLetter);
            if (item is not null)
            {
                DetectedUnregisteredDrives.Remove(item);
                OnPropertyChanged(nameof(HasDetectedDrives));
            }
        });
    }

    /// <summary>
    /// Creates a <see cref="NewCameraDriveItem"/> for a drive, optionally running
    /// <see cref="ICardDetectionService"/> to enrich it with a suggested camera type.
    /// Adds the item to <see cref="DetectedUnregisteredDrives"/> on the UI thread.
    /// Also auto-selects the matching profile if the user hasn't picked one manually.
    /// </summary>
    private async Task AddDriveItemWithDetectionAsync(
        string driveLetter,
        string rootPath,
        string displayLabel,
        string? volumeLabel,
        long totalSizeBytes)
    {
        string? suggestedType = null;
        string? suggestedDisplayName = null;

        if (_cardDetection is not null)
        {
            try
            {
                var detection = await _cardDetection.DetectCameraAsync(
                    driveLetter, rootPath, volumeLabel);

                suggestedType = detection.SuggestedCameraType;
                suggestedDisplayName = detection.SuggestedDisplayName;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Add Camera: CardDetection failed for {Drive} — skipping auto-detect", driveLetter);
            }
        }

        _dispatcher.Invoke(() =>
        {

            if (DetectedUnregisteredDrives.Any(d => d.DriveLetter == driveLetter)) return;

            DetectedUnregisteredDrives.Add(new NewCameraDriveItem(
                driveLetter: driveLetter,
                displayName: displayLabel,
                subInfo: $"{driveLetter}  ·  {FormatHelper.FormatBytes(totalSizeBytes)}",
                suggestedCameraType: suggestedType,
                suggestedDisplayName: suggestedDisplayName));

            if (!_userHasPickedProfile && !string.IsNullOrEmpty(suggestedType))
            {
                var matchingProfile = AvailableProfiles.FirstOrDefault(p =>
                    string.Equals(p.TypeName, suggestedType, StringComparison.OrdinalIgnoreCase));

                if (matchingProfile is not null)
                    SelectedProfile = matchingProfile;
            }
        });
    }

    private async void ExecuteAdd()
    {
        if (!CanAdd) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var id = _cameraId.Trim();
            var name = string.IsNullOrWhiteSpace(_cameraName) ? id : _cameraName.Trim();
            var profile = SelectedProfile!;

            var typeDef = _typeLoader.GetType(profile.TypeName);
            var features = CameraFeatures.BuildFromPreset(typeDef?.Features);

            var newCamera = new CameraConfig
            {
                Name = name,
                CameraType = profile.TypeName,
                Enabled = true,
                Features = features,
                FileTypes = CameraFileTypes.BuildFromPreset(typeDef?.DefaultFileTypes),
                BurstDetectionConfig = features.BurstDetection
                    ? new BurstDetectionConfig
                    {
                        Enabled        = true,
                        // Prefer type-preset defaults; fall back to all disk profiles if preset has none.
                        ActiveProfiles = typeDef?.DefaultBurstProfiles is { Count: > 0 }
                            ? new List<string>(typeDef.DefaultBurstProfiles)
                            : _burstProfileLoader?.ListAvailableProfiles() ?? new List<string>(),
                    }
                    : null,
            };

            _configWriter.AddCamera(id, newCamera);

            foreach (var drive in DetectedUnregisteredDrives.Where(d => d.IsChecked))
            {
                var info = await Task.Run(() => VolumeInfoReader.ReadSdCardInfo(drive.DriveLetter, _logger));

                if (string.IsNullOrWhiteSpace(info.VolumeSerial))
                {
                    _logger?.LogWarning("Add Camera: could not read VSN for {Drive} — skipping SD registration", drive.DriveLetter);
                    StatusMessage = string.Format(Strings.AddCamera_CannotReadVsnWarning, drive.DriveLetter);
                    continue;
                }

                var sdCameraId = id;

                var diskSerial = VolumeInfoReader.IsFakeDiskSerial(info.DiskSerial) ? null : info.DiskSerial;
                _configWriter.RegisterSdCard(info.VolumeSerial, SdCardRegistrationHelper.Create(
                    sdCameraId,
                    label: drive.DisplayName,
                    diskSerial: diskSerial,
                    sizeBytes: info.DiskSizeBytes));

                _logger?.LogInformation("Add Camera: SD card {Drive} (VSN: {Vsn}) registered to camera {CameraId}",
                    drive.DriveLetter, info.VolumeSerial, sdCameraId);
            }

            await _configWriter.SaveAsync();

            LoadDetectedDrives();

            _logger?.LogInformation("Camera added: {CameraId} (type: {Type})", id, profile.TypeName);
            StatusMessage = string.Format(Strings.AddCamera_Added, name, profile.TypeName);
            CameraAdded?.Invoke(this, EventArgs.Empty);

            CameraId = string.Empty;
            CameraName = string.Empty;
            _userHasPickedProfile = false;
            foreach (var drive in DetectedUnregisteredDrives)
                drive.IsChecked = false;

            ScheduleClearStatus(4000);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to add camera {CameraId}", _cameraId);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanAdd));
        }
    }

    public void Dispose()
    {
        if (_driveWatcher is not null)
        {
            _driveWatcher.DriveArrived -= OnDriveArrived;
            _driveWatcher.DriveRemoved -= OnDriveRemoved;
        }
        CancelScheduledClear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Lightweight view model for a camera profile entry in the Add Camera dialog dropdown.
/// Implements INotifyPropertyChanged so IsSelected drives the selection highlight via DataTrigger.
/// </summary>
public sealed class CameraProfileItem : ViewModelBase
{
    public string TypeName { get; }
    public string? Color { get; }
    /// <summary>Description shown in the dialog, with a fallback to TypeName when null.</summary>
    public string Description { get; }

    private bool _isSelected;
    /// <summary>True when this profile is the currently selected one. Drives border highlight in XAML.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public CameraProfileItem(string typeName, string? description, string? color)
    {
        TypeName = typeName;
        Color = color;
        Description = description ?? typeName;
    }
}

/// <summary>
/// Represents a currently connected but not-yet-registered removable drive
/// shown in the Add Camera dialog for immediate assignment.
/// Extended with camera detection suggestions from <see cref="ICardDetectionService"/>.
/// </summary>
public sealed class NewCameraDriveItem : ViewModelBase
{
    public string DriveLetter { get; }
    public string DisplayName { get; }
    public string SubInfo { get; }

    /// <summary>
    /// Suggested UMI camera type detected by CardDetectionService (e.g. "drone", "action_cam").
    /// Null when the camera could not be identified automatically.
    /// </summary>
    public string? SuggestedCameraType { get; }

    /// <summary>
    /// Suggested display name for the camera (e.g. "DJI Osmo Action 5 Pro").
    /// Null when the camera could not be identified automatically.
    /// </summary>
    public string? SuggestedDisplayName { get; }

    private bool _isChecked;
    /// <summary>True when the user wants to assign this drive to the new camera.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public NewCameraDriveItem(
        string driveLetter,
        string displayName,
        string subInfo,
        string? suggestedCameraType = null,
        string? suggestedDisplayName = null)
    {
        DriveLetter = driveLetter;
        DisplayName = displayName;
        SubInfo = subInfo;
        SuggestedCameraType = suggestedCameraType;
        SuggestedDisplayName = suggestedDisplayName;
    }
}
