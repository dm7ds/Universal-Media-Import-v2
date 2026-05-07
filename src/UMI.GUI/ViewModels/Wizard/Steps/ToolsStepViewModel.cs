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

using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Optional step (Simple + Advanced modes) — Tool paths.
/// Embeds the existing ToolsViewModel so the user can configure
/// ExifTool, Gyroflow and FFprobe paths during the wizard.
/// </summary>
public class ToolsStepViewModel : WizardStepViewModelBase
{
    private readonly WizardSession _session;

    public override string StepTitle => Strings.Wizard_ToolsStepTitle;
    public override string StepDescription => Strings.Wizard_ToolsDescription;

    /// <summary>
    /// The existing ToolsViewModel handles all tool path browsing and validation.
    /// XAML binds directly to Tools.ExifToolPath, Tools.BrowseExifToolCommand, etc.
    /// </summary>
    public ToolsViewModel Tools { get; }

    public string ExifToolDownloadUrl => "https://exiftool.org/";
    public string GyroflowDownloadUrl => ToolsViewModel.GyroflowDownloadUrl;
    public string FFprobeDownloadUrl  => ToolsViewModel.FFprobeDownloadUrl;

    public ICommand OpenUrlCommand { get; }

    public ToolsStepViewModel(
        WizardSession session,
        IConfigWriterService configWriter,
        ILogger<ToolsViewModel>? logger = null)
    {
        _session = session;
        Tools = new ToolsViewModel(configWriter, logger);

        OpenUrlCommand = new RelayCommand<string>(url =>
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception)
            {

            }
        });

        Tools.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToolsViewModel.ExifToolIsValid))
                RefreshValidity();
        };
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {
        Tools.RefreshFromConfig();
        RefreshValidity();
        return Task.CompletedTask;
    }

    private void RefreshValidity()
        => IsValid = Tools.ExifToolIsValid;
}
