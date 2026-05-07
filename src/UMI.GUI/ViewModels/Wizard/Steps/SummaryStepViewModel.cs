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
using UMI.Core.Configuration;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Final step — Summary.
/// Read-only overview of everything configured during the wizard.
/// Always valid — user can click Finish from here.
/// </summary>
public class SummaryStepViewModel : WizardStepViewModelBase
{
    private readonly WizardSession _session;

    public override string StepTitle => Strings.Wizard_SummaryStepTitle;
    public override string StepDescription => Strings.Wizard_SummaryDescription;

    private string _selectedModeName = string.Empty;
    public string SelectedModeName
    {
        get => _selectedModeName;
        private set => SetProperty(ref _selectedModeName, value);
    }

    private string _workbenchPath = string.Empty;
    public string WorkbenchPath
    {
        get => _workbenchPath;
        private set => SetProperty(ref _workbenchPath, value);
    }

    public ObservableCollection<WizardCameraEntry> Cameras { get; } = new();

    private string _gpsFolder = string.Empty;
    public string GpsFolder
    {
        get => _gpsFolder;
        private set => SetProperty(ref _gpsFolder, value);
    }

    private bool _hasGpsFolder;
    public bool HasGpsFolder
    {
        get => _hasGpsFolder;
        private set => SetProperty(ref _hasGpsFolder, value);
    }

    private bool _hasCameras;
    public bool HasCameras
    {
        get => _hasCameras;
        private set => SetProperty(ref _hasCameras, value);
    }

    public SummaryStepViewModel(WizardSession session)
    {
        _session = session;
        IsValid = true;
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {

        SelectedModeName = _session.Mode switch
        {
            AppMode.Dau      => AppModeLabels.Dau,
            AppMode.Simple   => AppModeLabels.Simple,
            AppMode.Advanced => AppModeLabels.Advanced,
            _                => _session.Mode.ToString()
        };

        WorkbenchPath = _session.WorkbenchPath;
        GpsFolder     = _session.GpsFolder;
        HasGpsFolder  = !string.IsNullOrWhiteSpace(_session.GpsFolder);

        Cameras.Clear();
        foreach (var entry in _session.Cameras)
            Cameras.Add(entry);

        HasCameras = Cameras.Count > 0;

        IsValid = true;
        return Task.CompletedTask;
    }
}
