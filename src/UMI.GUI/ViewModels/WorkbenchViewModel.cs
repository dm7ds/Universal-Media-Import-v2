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
using Microsoft.Extensions.Logging;
using UMI.Core.Constants;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the WorkbenchSection control.
/// Exposes the workbench path and sort-order toggle, and saves changes immediately.
/// </summary>
public class WorkbenchViewModel : ViewModelBase
{
    private readonly IConfigWriterService _configWriter;
    private readonly ILogger<WorkbenchViewModel>? _logger;

    private string _workbenchPath = string.Empty;
    /// <summary>The workbench path as shown in the UI.</summary>
    public string WorkbenchPath
    {
        get => _workbenchPath;
        set
        {
            if (SetProperty(ref _workbenchPath, value))
            {
                ValidatePath();
                _ = SaveWorkbenchPathAsync(value);
            }
        }
    }

    private bool _isCameraFirst = true;
    /// <summary>True = camera_first, False = type_first.</summary>
    public bool IsCameraFirst
    {
        get => _isCameraFirst;
        set
        {
            if (SetProperty(ref _isCameraFirst, value))
            {
                OnPropertyChanged(nameof(SortOrderLabel));
                OnPropertyChanged(nameof(SortOrderDescription));
                _ = SaveSortOrderAsync();
            }
        }
    }

    /// <summary>Human-readable label for the current sort order.</summary>
    public string SortOrderLabel => _isCameraFirst ? Strings.Workbench_CameraFirst : Strings.Workbench_TypeFirst;

    /// <summary>Short description of the folder structure produced by the current sort order.</summary>
    public string SortOrderDescription => _isCameraFirst
        ? Strings.Workbench_SortCameraFirst
        : Strings.Workbench_SortTypeFirst;

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    private bool _isStatusError;
    /// <summary>True when StatusMessage represents an error. Controls foreground color in the view.</summary>
    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    private bool _pathExists;
    /// <summary>True when the workbench directory exists on disk.</summary>
    public bool PathExists
    {
        get => _pathExists;
        private set => SetProperty(ref _pathExists, value);
    }

    /// <summary>Opens a FolderBrowserDialog and saves the new path.</summary>
    public ICommand BrowseCommand { get; }

    /// <summary>Toggles the sort order between camera_first and type_first.</summary>
    public ICommand ToggleSortOrderCommand { get; }

    public WorkbenchViewModel(IConfigWriterService configWriter, ILogger<WorkbenchViewModel>? logger = null)
    {
        _configWriter = configWriter;
        _logger = logger;

        BrowseCommand = new RelayCommand(ExecuteBrowse);
        ToggleSortOrderCommand = new RelayCommand(ExecuteToggleSortOrder);
    }

    /// <summary>
    /// Initializes properties from the loaded config.
    /// Call this after IConfigWriterService.LoadAsync() has been completed.
    /// Sets the backing fields directly to avoid triggering a save-back on load.
    /// </summary>
    public void Initialize()
    {
        try
        {
            var config = _configWriter.Config;

            _workbenchPath = config.GlobalPaths.Workbench ?? string.Empty;
            OnPropertyChanged(nameof(WorkbenchPath));
            IsCameraFirst = config.Layout.SortOrder != SortOrder.TypeFirst;
            ValidatePath();
        }
        catch (InvalidOperationException)
        {

            _workbenchPath = string.Empty;
            OnPropertyChanged(nameof(WorkbenchPath));
            IsCameraFirst = true;
        }
    }

    private void ExecuteBrowse()
    {
        var newPath = Helpers.DialogHelper.BrowseFolder(Strings.Workbench_SelectFolder, WorkbenchPath);
        if (newPath is null)
            return;

        WorkbenchPath = newPath;
    }

    private void ExecuteToggleSortOrder()
    {
        IsCameraFirst = !IsCameraFirst;
    }

    private async Task SaveWorkbenchPathAsync(string path)
    {
        // I-005: Ein Workbench INNERHALB von .umi legt saemtliche ordnerbasierten
        // Foto-Funktionen still — jeder Scan filtert seine eigenen Dateien per
        // IsInternalPath() wieder weg (BurstVisualizerService), und abgeleitete
        // Pfade verdoppeln das Segment (.umi\.umi\...db). Fuer den User nicht
        // diagnostizierbar, deshalb hier hart ablehnen statt zulassen.
        if (FolderNameConstants.IsInternalDirectory(path))
        {
            IsStatusError = true;
            StatusMessage = Strings.Workbench_InternalPathRejected;
            _logger?.LogWarning("Workbench path rejected (inside internal UMI directory): {Path}", path);

            // Der Setter hat den abgelehnten Pfad bereits ins Backing-Field
            // geschrieben. Ohne Rueckstellung zeigt die UI dauerhaft einen Wert,
            // der so gar nicht in der Config steht (Audit F-02).
            _workbenchPath = _configWriter.Config.GlobalPaths.Workbench ?? string.Empty;
            OnPropertyChanged(nameof(WorkbenchPath));
            ValidatePath();
            return;
        }

        IsSaving = true;
        StatusMessage = null;
        IsStatusError = false;

        try
        {
            _configWriter.Config.GlobalPaths.Workbench = path;
            await _configWriter.SaveAsync();
            IsStatusError = false;
            StatusMessage = Strings.Common_Saved;
            _logger?.LogInformation("Workbench path saved: {Path}", path);
            ScheduleClearStatus(2500);
        }
        catch (Exception ex)
        {
            IsStatusError = true;
            StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to save workbench path");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveSortOrderAsync()
    {
        IsSaving = true;
        StatusMessage = null;
        IsStatusError = false;

        try
        {
            var value = _isCameraFirst ? SortOrder.CameraFirst : SortOrder.TypeFirst;
            _configWriter.Config.Layout.SortOrder = value;
            await _configWriter.SaveAsync();
            IsStatusError = false;
            StatusMessage = Strings.Common_Saved;
            _logger?.LogInformation("Sort order saved: {Value}", value);
            ScheduleClearStatus(2500);
        }
        catch (Exception ex)
        {
            IsStatusError = true;
            StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to save sort order");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ValidatePath()
    {
        PathExists = !string.IsNullOrWhiteSpace(WorkbenchPath) && Directory.Exists(WorkbenchPath);
    }
}
