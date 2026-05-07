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

using System.Windows.Input;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Optional step (Advanced mode only) — GPS source folder.
/// Lets the user point to a folder containing GPX track files for GPS injection.
/// Always valid — GPS is optional.
/// </summary>
public class GpsStepViewModel : WizardStepViewModelBase
{
    private readonly WizardSession _session;

    public override string StepTitle => Strings.Wizard_GpsStepTitle;
    public override string StepDescription => Strings.Wizard_GpsDescription;

    private string _gpsFolder = string.Empty;
    /// <summary>Path to the folder containing GPX tracks. Empty means GPS injection is skipped.</summary>
    public string GpsFolder
    {
        get => _gpsFolder;
        set
        {
            if (SetProperty(ref _gpsFolder, value))
                _session.GpsFolder = value;
        }
    }

    public ICommand BrowseCommand { get; }

    public GpsStepViewModel(WizardSession session)
    {
        _session = session;

        BrowseCommand = new RelayCommand(ExecuteBrowse);

        IsValid = true;
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {

        if (_gpsFolder != _session.GpsFolder)
        {
            _gpsFolder = _session.GpsFolder;
            OnPropertyChanged(nameof(GpsFolder));
        }
        IsValid = true;
        return Task.CompletedTask;
    }

    private void ExecuteBrowse()
    {
        var folder = UMI.GUI.Helpers.DialogHelper.BrowseFolder(Strings.Wizard_GpsSelectFolder, _gpsFolder);
        if (folder is not null)
            GpsFolder = folder;
    }

}
