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
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Step 5 — More cameras prompt.
/// Shows the list of already registered cameras and offers to add another one
/// (loopback to SdInsert) or continue to the next step.
/// </summary>
public class MoreCamerasStepViewModel : WizardStepViewModelBase
{
    private readonly WizardSession _session;

    public override string StepTitle => Strings.Wizard_MoreCamerasTitle;
    public override string StepDescription => Strings.Wizard_MoreCamerasDescription;

    /// <summary>Live snapshot of cameras registered in this wizard run.</summary>
    public ObservableCollection<WizardCameraEntry> RegisteredCameras { get; } = new();

    /// <summary>
    /// Invoked when the user wants to register another camera.
    /// SetupWizardViewModel wires this callback to its loopback logic.
    /// </summary>
    public Action? RequestLoopBack { get; set; }

    /// <summary>Triggers loopback to SdInsert step to register another camera.</summary>
    public ICommand AddMoreCommand { get; }

    /// <summary>Proceeds to the next step without adding more cameras.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Removes a registered camera from the session and the list.</summary>
    public ICommand RemoveCameraCommand { get; }

    public MoreCamerasStepViewModel(WizardSession session)
    {
        _session = session;

        AddMoreCommand = new RelayCommand(ExecuteAddMore);
        ContinueCommand = new RelayCommand(ExecuteContinue);
        RemoveCameraCommand = new RelayCommand<WizardCameraEntry>(ExecuteRemoveCamera);

        IsValid = true;
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {

        RegisteredCameras.Clear();
        foreach (var entry in _session.Cameras)
            RegisteredCameras.Add(entry);

        IsValid = true;
        return Task.CompletedTask;
    }

    private void ExecuteAddMore()
        => RequestLoopBack?.Invoke();

    private void ExecuteContinue()
        => IsValid = true;

    private void ExecuteRemoveCamera(WizardCameraEntry? entry)
    {
        if (entry is null) return;
        _session.Cameras.Remove(entry);
        RegisteredCameras.Remove(entry);
    }
}
