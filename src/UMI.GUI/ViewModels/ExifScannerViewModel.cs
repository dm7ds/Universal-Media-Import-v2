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
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for a single EXIF field shown in the scanner result list.
/// Only fields with a numeric value can be added to rules.
/// </summary>
public class ExifFieldViewModel : ViewModelBase
{
    public string FieldName   { get; }
    public string Category    { get; }
    public string SampleValue { get; }
    public double? NumericValue { get; }
    public bool IsNumeric => NumericValue.HasValue;

    /// <summary>
    /// Optional callback fired when the field is checked (IsSelected becomes true) AND is numeric.
    /// Used to instantly add the field as a rule without requiring "Add to Rules".
    /// </summary>
    public Action<ExifFieldViewModel>? OnChecked { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value && IsNumeric)
                OnChecked?.Invoke(this);
        }
    }

    public ExifFieldViewModel(ExifFieldInfo info)
    {
        FieldName    = info.FieldName;
        Category     = info.Category;
        SampleValue  = info.SampleValue;
        NumericValue = info.NumericValue;
    }
}

/// <summary>
/// ViewModel for a category group shown in the scanner result tree.
/// </summary>
public class ExifFieldGroupViewModel : ViewModelBase
{
    public string Category { get; }
    public ObservableCollection<ExifFieldViewModel> Fields { get; } = new();

    public ExifFieldGroupViewModel(string category)
    {
        Category = category;
    }
}

/// <summary>
/// ViewModel for the EXIF scanner panel inside a BurstProfileCard.
/// Scans a folder to discover which numeric EXIF fields are present in all photos,
/// then allows the user to select fields and add them as match-condition rules.
/// </summary>
public class ExifScannerViewModel : ViewModelBase
{
    private readonly IExifFieldAnalyzerService _analyzerService;

    private string _folderPath = string.Empty;
    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (SetProperty(ref _folderPath, value))
                OnPropertyChanged(nameof(CanScan));
        }
    }

    private bool _isEditing;
    /// <summary>
    /// Propagated from BurstProfileCardViewModel. When false, Scan is disabled.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
                OnPropertyChanged(nameof(CanScan));
        }
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            SetProperty(ref _isScanning, value);
            OnPropertyChanged(nameof(CanScan));
        }
    }

    private string _scanStatus = string.Empty;
    /// <summary>Status message shown below the scan button (e.g. "Found 42 fields in 15 photos").</summary>
    public string ScanStatus
    {
        get => _scanStatus;
        private set => SetProperty(ref _scanStatus, value);
    }

    public bool CanScan => IsEditing && !IsScanning && !string.IsNullOrWhiteSpace(FolderPath);

    /// <summary>Categorized EXIF field groups found by the last scan.</summary>
    public ObservableCollection<ExifFieldGroupViewModel> FieldGroups { get; } = new();

    public ICommand ScanCommand          { get; }
    public ICommand BrowseFolderCommand  { get; }
    public ICommand AddSelectedToRulesCommand { get; }

    /// <summary>
    /// Set by BurstProfileCardViewModel.
    /// Called when "Add to Rules" is executed with the list of selected numeric fields.
    /// </summary>
    public Action<List<ExifFieldInfo>>? OnFieldsSelectedForRules { get; set; }

    private CancellationTokenSource? _scanCts;

    public ExifScannerViewModel(IExifFieldAnalyzerService analyzerService)
    {
        _analyzerService = analyzerService;

        ScanCommand               = new RelayCommand(ExecuteScan,    () => CanScan);
        BrowseFolderCommand       = new RelayCommand(ExecuteBrowseFolder);
        AddSelectedToRulesCommand = new RelayCommand(ExecuteAddSelectedToRules);
    }

    private void ExecuteBrowseFolder()
    {
        var folder = Helpers.DialogHelper.BrowseFolder(Strings.ExifScanner_SelectFolder);
        if (folder is not null)
            FolderPath = folder;
    }

    private async void ExecuteScan()
    {
        if (string.IsNullOrWhiteSpace(FolderPath)) return;

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        ScanStatus = Strings.ExifScanner_Scanning;
        FieldGroups.Clear();

        try
        {
            var progress = new Progress<ExifScanProgress>(p =>
                ScanStatus = string.Format(Strings.ExifScanner_ScanProgress, p.ScannedFiles, p.TotalFiles, p.CurrentFile));

            var result = await _analyzerService.AnalyzeFolderAsync(FolderPath, progress, ct);

            if (ct.IsCancellationRequested) return;

            int totalFields = result.FieldGroups.Sum(g => g.Fields.Count);
            int numericFields = result.FieldGroups.Sum(g => g.Fields.Count(f => f.NumericValue.HasValue));

            foreach (var group in result.FieldGroups)
            {
                var groupVm = new ExifFieldGroupViewModel(group.Category);
                foreach (var field in group.Fields)
                {
                    var fieldVm = new ExifFieldViewModel(field);
                    fieldVm.OnChecked = OnFieldChecked;
                    groupVm.Fields.Add(fieldVm);
                }
                FieldGroups.Add(groupVm);
            }

            ScanStatus = string.Format(Strings.ExifScanner_FoundFields, totalFields, numericFields, result.TotalPhotos);
        }
        catch (OperationCanceledException)
        {
            ScanStatus = Strings.ExifScanner_ScanCancelled;
        }
        catch (Exception ex)
        {
            ScanStatus = string.Format(Strings.Common_ErrorFormat, ex.Message);
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Callback for instant rule-add when a numeric field's checkbox is clicked.
    /// </summary>
    private void OnFieldChecked(ExifFieldViewModel field)
    {
        if (OnFieldsSelectedForRules == null) return;

        var info = new ExifFieldInfo
        {
            FieldName    = field.FieldName,
            Directory    = string.Empty,
            Category     = field.Category,
            SampleValue  = field.SampleValue,
            NumericValue = field.NumericValue
        };

        OnFieldsSelectedForRules(new List<ExifFieldInfo> { info });

        field.IsSelected = false;
    }

    private void ExecuteAddSelectedToRules()
    {
        if (OnFieldsSelectedForRules == null) return;

        var selected = FieldGroups
            .SelectMany(g => g.Fields)
            .Where(f => f.IsSelected && f.IsNumeric)
            .Select(f => new ExifFieldInfo
            {
                FieldName    = f.FieldName,
                Directory    = string.Empty,
                Category     = f.Category,
                SampleValue  = f.SampleValue,
                NumericValue = f.NumericValue
            })
            .ToList();

        if (selected.Count == 0) return;

        OnFieldsSelectedForRules(selected);

        foreach (var group in FieldGroups)
            foreach (var field in group.Fields)
                field.IsSelected = false;
    }
}
