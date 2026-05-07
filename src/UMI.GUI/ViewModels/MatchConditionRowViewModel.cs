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

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for a single match-condition row in a BurstProfile rule list.
/// Exposes LogicOp, Field, Operator, Value as editable properties.
/// Raises an event on any property change so the parent card can track IsDirty
/// and trigger debounced Visualizer re-evaluation.
/// </summary>
public class MatchConditionRowViewModel : ViewModelBase
{

    /// <summary>Available comparison operators for numeric EXIF fields.</summary>
    public static string[] AvailableOperators { get; } = { ">", ">=", "<", "<=", "==", "!=" };

    /// <summary>Available logic operators for combining rules.</summary>
    public static string[] LogicOperators { get; } = { "AND", "OR" };

    private System.Collections.ObjectModel.ObservableCollection<string>? _availableFields;
    /// <summary>
    /// Available EXIF field names for the field ComboBox.
    /// Set by BurstProfileCardViewModel after construction so each row
    /// binds to this instance property (avoids RelativeSource AncestorType
    /// ambiguity with nested ItemsControls).
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<string>? AvailableFields
    {
        get => _availableFields;
        set => SetProperty(ref _availableFields, value);
    }

    private bool _isEditing;
    /// <summary>
    /// Mirrors BurstProfileCardViewModel.IsEditing — propagated by the parent after every
    /// Add/Remove and on every IsEditing state change.
    /// Exposed as a direct property so XAML can bind without RelativeSource AncestorType
    /// ambiguity caused by nested ItemsControls.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    private string _logicOp = "AND";
    /// <summary>Logic operator connecting this rule to the previous one ("AND" / "OR"). Only relevant for rows beyond index 0.</summary>
    public string LogicOp
    {
        get => _logicOp;
        set
        {
            if (SetProperty(ref _logicOp, value))
                RaiseChanged();
        }
    }

    private bool _isFirstRow;
    /// <summary>
    /// True when this row is the first (index 0) in the parent's Rules collection.
    /// Set by BurstProfileCardViewModel.UpdateFirstRowFlags() after every Add/Remove.
    /// When true, the LogicOp ComboBox is hidden — the first rule has no preceding rule to connect to.
    /// </summary>
    public bool IsFirstRow
    {
        get => _isFirstRow;
        set => SetProperty(ref _isFirstRow, value);
    }

    private string _field = string.Empty;
    /// <summary>EXIF field name to evaluate (e.g. "ContinuousDrive", "ISO").</summary>
    public string Field
    {
        get => _field;
        set
        {
            if (SetProperty(ref _field, value))
                RaiseChanged();
        }
    }

    private string _operator = ">";
    /// <summary>Comparison operator: ">", ">=", "<", "<=", "==", "!=".</summary>
    public string Operator
    {
        get => _operator;
        set
        {
            if (SetProperty(ref _operator, value))
                RaiseChanged();
        }
    }

    private double _value;
    /// <summary>Numeric value to compare the EXIF field against.</summary>
    public double Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
                RaiseChanged();
        }
    }

    /// <summary>Removes this rule from the parent card's Rules collection.</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>
    /// Raised when any editable property changes.
    /// Subscribed by BurstProfileCardViewModel for IsDirty tracking and debounce trigger.
    /// </summary>
    public event EventHandler? Changed;

    public MatchConditionRowViewModel(Action<MatchConditionRowViewModel> removeAction)
    {
        RemoveCommand = new RelayCommand(() => removeAction(this));
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
