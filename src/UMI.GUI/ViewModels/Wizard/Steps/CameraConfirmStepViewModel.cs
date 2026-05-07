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
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Step 4 — Camera confirmation.
/// Simplified: Camera Type → Profile auto-selected → Camera Name + Import Folder pre-filled.
/// Camera ID is hidden from the user; it is auto-generated from the Camera Name.
/// </summary>
public class CameraConfirmStepViewModel : WizardStepViewModelBase
{
    private readonly WizardSession _session;
    private readonly IConfigWriterService _configWriter;
    private readonly CameraTypeLoader _typeLoader;

    /// <summary>All profiles loaded from CameraTypeLoader. Never modified after construction.</summary>
    private readonly List<CameraProfileItem> _allProfiles;

    /// <summary>Tracks whether the user has manually edited the Import Folder field.</summary>
    private bool _userEditedFolderName;

    /// <summary>
    /// The Camera ID that was already added to the session when the user navigated forward once.
    /// Excluded from duplicate validation so that back/forward navigation does not produce a
    /// false "already added" error for the very camera being edited.
    /// Null on the first visit; set to the camera's current ID after the first OnLeaveAsync.
    /// </summary>
    private string? _editingCameraId;

    public override string StepTitle => Strings.Wizard_CameraConfirmTitle;
    public override string StepDescription => Strings.Wizard_CameraConfirmDescription;

    private string _displayName = string.Empty;
    /// <summary>Human-readable camera name. Drives auto-generation of CameraId and FolderName.</summary>
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
                OnDisplayNameChanged();
        }
    }

    private string _cameraId = string.Empty;
    /// <summary>
    /// Config-safe unique identifier. Auto-generated from DisplayName; never shown in UI.
    /// </summary>
    private string CameraId
    {
        get => _cameraId;
        set
        {
            if (SetProperty(ref _cameraId, value))
            {
                OnPropertyChanged(nameof(CameraNameError));
                RefreshValidity();
            }
        }
    }

    /// <summary>
    /// Null when the auto-generated ID is valid.
    /// Non-null error message shown below the Camera Name field when ID would be duplicate.
    /// The currently-edited camera (_editingCameraId) is excluded from the duplicate check so
    /// that back/forward navigation does not trigger a false "already added" error.
    /// </summary>
    public string? CameraNameError =>
        CameraSetupHelpers.ValidateCameraId(
            _cameraId,
            _configWriter,
            _session.Cameras
                .Where(c => c.CameraId != _editingCameraId)
                .Select(c => c.CameraId));

    private string _folderName = string.Empty;
    /// <summary>
    /// Subfolder name inside the workbench. Auto-derived from DisplayName unless the user edits it.
    /// When the UI binding sets this property, <see cref="_userEditedFolderName"/> is marked true
    /// so that subsequent DisplayName changes no longer overwrite the user's choice.
    /// Auto-generation from <see cref="OnDisplayNameChanged"/> bypasses this setter to avoid
    /// setting the flag programmatically (it writes <see cref="_folderName"/> directly).
    /// </summary>
    public string FolderName
    {
        get => _folderName;
        set
        {
            if (SetProperty(ref _folderName, value))
            {
                _userEditedFolderName = true;
                RefreshValidity();
            }
        }
    }

    /// <summary>
    /// Distinct camera type names derived from all profiles (SSOT: CameraTypeLoader).
    /// </summary>
    public ObservableCollection<string> AvailableCameraTypes { get; } = new();

    private string? _selectedCameraType;
    /// <summary>
    /// The currently selected camera type. Changing it auto-selects the first matching profile.
    /// </summary>
    public string? SelectedCameraType
    {
        get => _selectedCameraType;
        set
        {
            if (SetProperty(ref _selectedCameraType, value))
                OnCameraTypeChanged();
        }
    }

    private CameraProfileItem? _selectedProfile;
    private CameraProfileItem? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                foreach (var p in _allProfiles)
                    p.IsSelected = ReferenceEquals(p, value);

                RefreshValidity();
            }
        }
    }

    /// <summary>True when all required fields are valid and the user can proceed.</summary>
    public bool CanConfirm => IsValid;

    public CameraConfirmStepViewModel(
        WizardSession session,
        IConfigWriterService configWriter,
        CameraTypeLoader typeLoader)
    {
        _session = session;
        _configWriter = configWriter;
        _typeLoader = typeLoader;

        _allProfiles = CameraSetupHelpers.LoadProfiles(_typeLoader);
        PopulateCameraTypes();
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {
        _userEditedFolderName = false;

        var fp    = _session.DetectedFingerprint;
        var match = _session.DetectedModel;

        var wouldBeId = CameraSetupHelpers.GenerateCameraId(
            match?.DisplayName ?? fp?.Model ?? Strings.Wizard_CameraDefaultName);
        _editingCameraId = _session.Cameras.Any(c => c.CameraId == wouldBeId)
            ? wouldBeId
            : null;

        var detectedName = match?.DisplayName
                           ?? fp?.Model
                           ?? Strings.Wizard_CameraDefaultName;

        DisplayName = detectedName;

        if (!string.IsNullOrWhiteSpace(match?.CameraType))
        {
            var detectedType = AvailableCameraTypes
                .FirstOrDefault(t => string.Equals(t, match.CameraType, StringComparison.OrdinalIgnoreCase));

            SelectedCameraType = detectedType ?? AvailableCameraTypes.FirstOrDefault();
        }
        else
        {
            SelectedCameraType = AvailableCameraTypes.FirstOrDefault();
        }

        RefreshValidity();
        return Task.CompletedTask;
    }

    public override Task OnLeaveAsync(CancellationToken ct = default)
    {
        if (!IsValid) return Task.CompletedTask;

        var id          = _cameraId.Trim();
        var folderName  = string.IsNullOrWhiteSpace(FolderName) ? id : FolderName.Trim();
        var displayName = string.IsNullOrWhiteSpace(DisplayName) ? id : DisplayName.Trim();

        var entry = new WizardCameraEntry
        {
            CameraId    = id,
            DisplayName = displayName,
            CameraType  = SelectedProfile?.TypeName ?? Strings.Wizard_CameraDefaultProfile,
            ProfileName = SelectedProfile?.TypeName ?? Strings.Wizard_CameraDefaultProfile,
            FolderName  = folderName,
        };

        if (!string.IsNullOrWhiteSpace(_session.DetectedDriveLetter))
        {
            entry.Sources.Add(new SourceAssignment
            {
                DriveLetter = _session.DetectedDriveLetter,
                SourceType  = "sd"
            });
        }

        var existing = _session.Cameras.FirstOrDefault(c => c.CameraId == id);
        if (existing is null && _editingCameraId is not null)
        {

            existing = _session.Cameras.FirstOrDefault(c => c.CameraId == _editingCameraId);
        }
        if (existing != null)
            _session.Cameras.Remove(existing);

        _session.Cameras.Add(entry);

        _editingCameraId = id;

        return Task.CompletedTask;
    }

    private void PopulateCameraTypes()
    {
        AvailableCameraTypes.Clear();
        foreach (var typeName in _allProfiles
            .Select(p => p.TypeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t))
        {
            AvailableCameraTypes.Add(typeName);
        }
    }

    private void OnCameraTypeChanged()
    {
        var filtered = string.IsNullOrEmpty(_selectedCameraType)
            ? _allProfiles
            : _allProfiles.Where(p =>
                string.Equals(p.TypeName, _selectedCameraType, StringComparison.OrdinalIgnoreCase));

        SelectedProfile = filtered.FirstOrDefault();
    }

    private void OnDisplayNameChanged()
    {

        CameraId = CameraSetupHelpers.GenerateCameraId(_displayName);

        if (!_userEditedFolderName)
        {

            _folderName = CameraSetupHelpers.GenerateFolderName(_displayName);
            OnPropertyChanged(nameof(FolderName));
        }

        RefreshValidity();
    }

    private void RefreshValidity()
    {
        var id        = _cameraId.Trim();
        var idOk      = !string.IsNullOrEmpty(id)
                        && CameraSetupHelpers.ValidateCameraId(id, _configWriter,
                               _session.Cameras
                                   .Where(c => c.CameraId != _editingCameraId)
                                   .Select(c => c.CameraId)) is null;
        var profileOk = _selectedProfile is not null;

        IsValid = idOk && profileOk;
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(CameraNameError));
    }
}
