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
using UMI.GUI.Helpers;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for a single camera type profile card in the Profiles sub-tab.
/// Wraps a CameraTypeDefinition and exposes editable properties with dirty tracking.
/// Save persists via CameraTypeLoader, Revert restores baseline values.
///
/// Schema v2: per-feature objects (available, enabled_by_default, simple_on_card).
/// ProfileFeatureViewModel rows are built from FeatureLabels.All (SSOT canonical order).
///
/// Card UX: Collapsible (IsExpanded) + Edit-mode gating (IsEditing).
/// Collapse while editing → CancelCommand (mirrors BurstProfileCardViewModel pattern).
/// </summary>
public class ProfileViewModel : ViewModelBase
{
    private readonly CameraTypeLoader _typeLoader;
    private readonly Action<ProfileViewModel> _onSaved;

    /// <summary>
    /// Fallback hex color used when a profile has no color set or the color string is invalid.
    /// Reads from Application.Current.Resources (ColorOther) via SSOT helper.
    /// </summary>
    private static string FallbackColor => CameraTypeColors.GetHexColor(null);

    private string _originalName;
    private string _originalDescription;
    private string _originalColor;

    private string _editName;
    /// <summary>Profile type name (e.g. "Action"). Persisted as the file name.</summary>
    public string EditName
    {
        get => _editName;
        set
        {
            if (SetProperty(ref _editName, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private string _editDescription;
    /// <summary>Human-readable description for this camera type.</summary>
    public string EditDescription
    {
        get => _editDescription;
        set
        {
            if (SetProperty(ref _editDescription, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private string _editColor;
    /// <summary>Hex color string for the accent bar and type badge (e.g. "#f97316").</summary>
    public string EditColor
    {
        get => _editColor;
        set
        {
            if (SetProperty(ref _editColor, value))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(ColorPreview));
            }
        }
    }

    /// <summary>
    /// Returns EditColor when it looks like a valid hex color, otherwise a safe fallback.
    /// Consumed by HexColorToBrushConverter in XAML — prevents converter crash on invalid input.
    /// </summary>
    public string ColorPreview => IsValidHexColor(_editColor) ? _editColor : FallbackColor;

    private bool _isExpanded;
    /// <summary>
    /// True when the card body is visible. Setting to false while editing triggers CancelCommand
    /// (mirrors BurstProfileCardViewModel pattern — collapse = implicit cancel).
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && !value && _isEditing)
            {

                ExecuteCancel();
            }
        }
    }

    private bool _isEditing;
    /// <summary>
    /// True when the card is in edit mode (fields are editable, action buttons are visible).
    /// Default false — cards start read-only so accidental edits are prevented.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    /// <summary>All 9 known features as editable rows (Available + Default + On Card toggles).</summary>
    public ObservableCollection<ProfileFeatureViewModel> Features { get; } = new();

    /// <summary>Display names of cameras currently using this profile type.</summary>
    public ObservableCollection<string> AssignedCameraNames { get; } = new();

    /// <summary>True when any editable field or feature row differs from the saved definition.</summary>
    public bool IsDirty =>
        _editName != _originalName ||
        _editDescription != _originalDescription ||
        _editColor != _originalColor ||
        Features.Any(f => f.IsDirty);

    private string? _saveStatus;
    /// <summary>Brief status message after a save attempt ("Saved" / error text).</summary>
    public string? SaveStatus
    {
        get => _saveStatus;
        private set => SetProperty(ref _saveStatus, value);
    }

    /// <summary>Enters edit mode (IsEditing = true, IsExpanded = true).</summary>
    public ICommand EditCommand { get; }

    /// <summary>Reverts all changes and exits edit mode (IsEditing = false).</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Persists the current edits to disk via CameraTypeLoader.SaveType().</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Discards edits and reverts all fields to the last saved values.</summary>
    public ICommand RevertCommand { get; }

    /// <summary>Toggles IsExpanded (collapses/expands the card body).</summary>
    public ICommand ToggleExpandCommand { get; }

    public ProfileViewModel(
        CameraTypeDefinition definition,
        IEnumerable<string> assignedCameraNames,
        CameraTypeLoader typeLoader,
        Action<ProfileViewModel> onSaved)
    {
        _typeLoader = typeLoader;
        _onSaved = onSaved;

        _originalName        = definition.Name;
        _originalDescription = definition.Description ?? string.Empty;
        _originalColor       = definition.Color ?? FallbackColor;

        _editName        = _originalName;
        _editDescription = _originalDescription;
        _editColor       = _originalColor;

        foreach (var entry in FeatureLabels.All)
        {
            var featureDef = definition.Features?.GetValueOrDefault(entry.Key);

            bool isAvailable    = featureDef?.Available        ?? false;
            bool isDefault      = featureDef?.EnabledByDefault ?? false;
            bool isSimpleOnCard = featureDef?.SimpleOnCard     ?? false;

            Features.Add(new ProfileFeatureViewModel(
                featureKey:   entry.Key,
                label:        entry.Label,
                bubbleLabel:  entry.BubbleLabel,
                badgeKey:     entry.BadgeKey,
                isAvailable:  isAvailable,
                isDefault:    isDefault,
                isSimple:     isSimpleOnCard));
        }

        foreach (var feature in Features)
            feature.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsDirty));

        foreach (var name in assignedCameraNames)
            AssignedCameraNames.Add(name);

        EditCommand         = new RelayCommand(ExecuteEdit);
        CancelCommand       = new RelayCommand(ExecuteCancel);
        SaveCommand         = new RelayCommand(ExecuteSave);
        RevertCommand       = new RelayCommand(ExecuteRevert);
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    private void ExecuteEdit()
    {
        IsEditing  = true;
        IsExpanded = true;
    }

    /// <summary>
    /// Reverts all unsaved changes and exits edit mode.
    /// Triggered by the Cancel button or by collapsing the card while editing.
    /// </summary>
    private void ExecuteCancel()
    {
        ExecuteRevert();

    }

    private void ExecuteSave()
    {
        try
        {
            var typeDef = BuildDefinition();
            _typeLoader.SaveType(typeDef);

            _originalName        = _editName;
            _originalDescription = _editDescription;
            _originalColor       = _editColor;

            foreach (var f in Features)
                f.CommitBaseline();

            SaveStatus = Strings.Common_Saved;
            IsEditing  = false;
            OnPropertyChanged(nameof(IsDirty));

            _onSaved(this);

            _ = ClearSaveStatusAsync();
        }
        catch (Exception ex)
        {
            SaveStatus = string.Format(Strings.Common_ErrorFormat, ex.Message);
        }
    }

    private void ExecuteRevert()
    {
        EditName        = _originalName;
        EditDescription = _originalDescription;
        EditColor       = _originalColor;

        var originalDef = _typeLoader.GetType(_originalName);

        foreach (var feature in Features)
        {
            var featureDef = originalDef?.Features?.GetValueOrDefault(feature.FeatureKey);

            feature.IsAvailable = featureDef?.Available        ?? false;
            feature.IsDefault   = featureDef?.EnabledByDefault ?? false;
            feature.IsSimple    = featureDef?.SimpleOnCard     ?? false;
        }

        IsEditing  = false;
        OnPropertyChanged(nameof(IsDirty));
        SaveStatus = null;
    }

    /// <summary>
    /// Builds a CameraTypeDefinition from the current editable state (schema v2).
    /// </summary>
    public CameraTypeDefinition BuildDefinition()
    {
        var featureDict = new Dictionary<string, FeatureDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Features)
        {
            featureDict[f.FeatureKey] = new FeatureDefinition(
                Available:        f.IsAvailable,
                EnabledByDefault: f.IsDefault,
                SimpleOnCard:     f.IsSimple);
        }

        var existing = _typeLoader.GetType(_originalName);

        return new CameraTypeDefinition
        {
            Name             = _editName,
            Description      = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription,
            Color            = _editColor,
            Features         = featureDict,
            SimpleFeatures   = existing?.SimpleFeatures ?? new(),
            DefaultFileTypes = existing?.DefaultFileTypes,
        };
    }

    private static bool IsValidHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return false;
        try
        {
            System.Windows.Media.ColorConverter.ConvertFromString(color);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ClearSaveStatusAsync()
    {
        await Task.Delay(3000);
        SaveStatus = null;
    }
}
