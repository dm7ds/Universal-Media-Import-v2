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

using System.IO;
using System.Windows.Input;
using UMI.Core.Utilities;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Step 2 — Workbench path selection.
/// User picks the root folder where imported media will be organized.
/// </summary>
public class WorkbenchStepViewModel : WizardStepViewModelBase
{
    private readonly WizardSession _session;

    public override string StepTitle => Strings.Wizard_WorkbenchTitle;
    public override string StepDescription => Strings.Wizard_WorkbenchDescription;

    private string _workbenchPath = string.Empty;
    /// <summary>The workbench root folder path. Validated on set.</summary>
    public string WorkbenchPath
    {
        get => _workbenchPath;
        set
        {
            if (SetProperty(ref _workbenchPath, value))
            {
                _session.WorkbenchPath = value;
                ValidatePath();
            }
        }
    }

    private string? _pathError;
    /// <summary>Null when path is valid. Non-null contains an error message.</summary>
    public string? PathError
    {
        get => _pathError;
        private set => SetProperty(ref _pathError, value);
    }

    public ICommand BrowseCommand { get; }

    public WorkbenchStepViewModel(WizardSession session)
    {
        _session = session;
        BrowseCommand = new RelayCommand(ExecuteBrowse);
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {

        if (!string.IsNullOrWhiteSpace(_session.WorkbenchPath) && _workbenchPath != _session.WorkbenchPath)
        {
            _workbenchPath = _session.WorkbenchPath;
            OnPropertyChanged(nameof(WorkbenchPath));
            ValidatePath();
        }
        return Task.CompletedTask;
    }

    private void ExecuteBrowse()
    {
        var folder = UMI.GUI.Helpers.DialogHelper.BrowseFolder(Strings.Wizard_WorkbenchSelectFolder, _workbenchPath);
        if (folder is not null)
            WorkbenchPath = folder;
    }

    private void ValidatePath()
    {
        var path = _workbenchPath?.Trim();

        if (string.IsNullOrWhiteSpace(path))
        {
            PathError = Strings.Wizard_PathErrorEmpty;
            IsValid = false;
            return;
        }

        try
        {
            var invalidChars = Path.GetInvalidPathChars();
            if (path.Any(c => invalidChars.Contains(c)))
            {
                PathError = Strings.Wizard_PathErrorInvalidChars;
                IsValid = false;
                return;
            }

            if (!Path.IsPathRooted(path))
            {
                PathError = Strings.Wizard_PathErrorNotAbsolute;
                IsValid = false;
                return;
            }

            // I-005: Ein Workbench innerhalb von .umi macht saemtliche Foto-Scans
            // blind — der Ordner wirkt dauerhaft leer. Schon hier abfangen.
            if (FolderNameConstants.IsInternalDirectory(path))
            {
                PathError = Strings.Wizard_PathErrorInternal;
                IsValid = false;
                return;
            }
        }
        catch (Exception)
        {
            PathError = Strings.Wizard_PathErrorInvalidFormat;
            IsValid = false;
            return;
        }

        PathError = null;
        IsValid = true;
    }
}
