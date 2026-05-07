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
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Profiles sub-tab in Settings.
/// Loads all CameraTypeDefinitions and exposes them as editable ProfileViewModels.
/// Refresh propagates type definition changes to live CameraViewModels.
/// </summary>
public class ProfilesTabViewModel : ViewModelBase
{
    private readonly CameraTypeLoader _typeLoader;
    private readonly ProfileService _profileService;

    /// <summary>All loaded profiles — one card per .umi file.</summary>
    public ObservableCollection<ProfileViewModel> Profiles { get; } = new();

    /// <summary>
    /// Reference to all live camera ViewModels.
    /// Set by MainViewModel after initial config load so profile saves can refresh cameras.
    /// </summary>
    public ObservableCollection<CameraViewModel>? Cameras { get; set; }

    /// <summary>
    /// Reference to the config writer — used to check which cameras use a given profile
    /// before allowing deletion.
    /// </summary>
    public IConfigWriterService? ConfigWriter { get; set; }

    /// <summary>Deletes a profile after checking that no cameras are linked to it.</summary>
    public ICommand DeleteProfileCommand { get; }

    public ProfilesTabViewModel(CameraTypeLoader typeLoader, ProfileService profileService)
    {
        _typeLoader = typeLoader;
        _profileService = profileService;
        DeleteProfileCommand = new RelayCommand<ProfileViewModel>(ExecuteDeleteProfile);
    }

    /// <summary>
    /// Loads all profiles from CameraTypeLoader and builds ProfileViewModels.
    /// Called from MainViewModel.LoadAsync() after cameras are populated.
    /// Can be called again after a Save to refresh from disk.
    /// </summary>
    public void Initialize()
    {
        Profiles.Clear();

        var types = _typeLoader.LoadAllTypes();

        foreach (var kvp in types.OrderBy(t => t.Key))
        {

            var assignedNames = Cameras?
                .Where(c => c.CameraType.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Name)
                .OrderBy(n => n)
                .ToList() ?? [];

            var profileVm = new ProfileViewModel(
                definition: kvp.Value,
                assignedCameraNames: assignedNames,
                typeLoader: _typeLoader,
                onSaved: OnProfileSaved);

            Profiles.Add(profileVm);
        }
    }

    /// <summary>
    /// Called when a ProfileViewModel saves successfully.
    /// Refreshes the type color and feature bubbles on all CameraViewModels
    /// that belong to this profile type, so the camera cards update live.
    /// </summary>
    private void OnProfileSaved(ProfileViewModel savedProfile)
    {
        if (Cameras == null) return;

        var typeName = savedProfile.EditName;

        foreach (var camera in Cameras)
        {
            if (!camera.CameraType.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                continue;

            camera.RefreshFromTypeDefinition();
        }
    }

    /// <summary>
    /// Deletes a profile. Guards against deletion if cameras are still linked to it.
    /// </summary>
    private void ExecuteDeleteProfile(ProfileViewModel? profile)
    {
        if (profile == null) return;

        var profileName = profile.EditName;

        if (ConfigWriter != null)
        {
            var linked = ConfigWriter.Config.Cameras
                .Where(kv => kv.Value.CameraType.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToList();

            if (linked.Count > 0)
            {
                MessageBox.Show(
                    string.Format(Strings.Profile_CannotDeleteMessage, profileName, string.Join(", ", linked)),
                    Strings.Profile_CannotDeleteTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        var result = MessageBox.Show(
            string.Format(Strings.Profile_DeleteConfirm, profileName),
            Strings.Profile_DeleteTitle,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        _profileService.DeleteProfile(profileName);

        Initialize();
    }
}
