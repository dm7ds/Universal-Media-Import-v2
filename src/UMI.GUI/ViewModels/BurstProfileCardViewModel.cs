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

file static class BuiltInExifFields
{

    public static readonly IReadOnlyList<string> All = AutoPresetGenerator.RelevantFields.ToList();
}

/// <summary>
/// ViewModel for a single burst-detection profile card in the Burst sub-tab.
/// Wraps a BurstProfile and exposes editable properties with dirty-tracking.
/// Contains sub-VMs for the EXIF Scanner, Grouping Config, and Visualizer.
///
/// Rule changes and grouping changes trigger a debounced (300 ms) Visualizer re-evaluation
/// so the user sees live feedback without hammering the evaluation on every keystroke.
/// </summary>
public class BurstProfileCardViewModel : ViewModelBase
{
    private readonly BurstProfileLoader _profileLoader;

    private string _originalName;
    private string _originalDescription;
    private int    _originalPriority;
    private int    _originalColorIndex;

    private string _name;
    /// <summary>Profile name. Used as the .umi file name on disk.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private string _description;
    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private int _priority;
    /// <summary>Lower number = checked first. Profiles are evaluated in priority order.</summary>
    public int Priority
    {
        get => _priority;
        set
        {
            if (SetProperty(ref _priority, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private int _colorIndex;
    /// <summary>Color palette index (0–7). -1 = auto-assigned.</summary>
    public int ColorIndex
    {
        get => _colorIndex;
        set
        {
            if (SetProperty(ref _colorIndex, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private bool _isDropTargetAbove;
    /// <summary>True while a card is being dragged over the upper half of this card — shows a top insertion line.</summary>
    public bool IsDropTargetAbove
    {
        get => _isDropTargetAbove;
        set => SetProperty(ref _isDropTargetAbove, value);
    }

    private bool _isDropTargetBelow;
    /// <summary>True while a card is being dragged over the lower half of this card — shows a bottom insertion line.</summary>
    public bool IsDropTargetBelow
    {
        get => _isDropTargetBelow;
        set => SetProperty(ref _isDropTargetBelow, value);
    }

    private bool _isExpanded;
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
    /// Propagated to every MatchConditionRowViewModel so XAML rows can bind directly
    /// to IsEditing without RelativeSource AncestorType ambiguity.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {

                foreach (var row in Rules)
                    row.IsEditing = value;

                Scanner.IsEditing    = value;
                Visualizer.IsEditing = value;
            }
        }
    }

    private bool _isDirty;
    /// <summary>True when any editable field, rule, or grouping differs from the saved profile.</summary>
    public bool IsDirty
    {
        get => _isDirty
               || _name        != _originalName
               || _description != _originalDescription
               || _priority    != _originalPriority
               || _colorIndex  != _originalColorIndex;
        set => SetProperty(ref _isDirty, value);
    }

    /// <summary>
    /// Save/error status message shown on the burst profile card.
    /// Delegates to <see cref="ViewModelBase.StatusMessage"/> so
    /// <see cref="ViewModelBase.ScheduleClearStatus"/> clears it without a duplicate CTS.
    /// </summary>
    public string? SaveStatus
    {
        get => StatusMessage;
        private set
        {
            StatusMessage = value;
            OnPropertyChanged(nameof(SaveStatus));
        }
    }

    /// <summary>
    /// Distinct list of selectable EXIF field names for the rule-row field ComboBox.
    /// Sources (merged, deduplicated, sorted):
    ///   1. Built-in fields (BuiltInExifFields.All)
    ///   2. Fields currently used in rules
    ///   3. Numeric fields found by the EXIF Scanner
    /// Updated via UpdateAvailableExifFields() on Scanner completion and rule changes.
    /// </summary>
    public ObservableCollection<string> AvailableExifFields { get; } = new();

    /// <summary>Match-condition rule rows for this profile.</summary>
    public ObservableCollection<MatchConditionRowViewModel> Rules { get; } = new();

    /// <summary>Grouping configuration (MaxGap, MinCount, Adaptive…).</summary>
    public GroupingConfigViewModel Grouping { get; } = new();

    /// <summary>EXIF field scanner that populates selectable fields for new rules.</summary>
    public ExifScannerViewModel Scanner { get; }

    /// <summary>Burst sequence visualizer (live preview).</summary>
    public VisualizerViewModel Visualizer { get; }

    public ICommand SaveCommand         { get; }
    public ICommand RevertCommand       { get; }
    public ICommand AddRuleCommand      { get; }
    public ICommand ToggleExpandCommand { get; }

    /// <summary>Enters edit mode (IsEditing = true).</summary>
    public ICommand EditCommand   { get; }

    /// <summary>Reverts changes and exits edit mode (IsEditing = false).</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Sets ColorIndex from a string CommandParameter (used by XAML color-palette buttons).</summary>
    public ICommand SetColorIndexCommand { get; }

    /// <summary>Adds a stable field to the grouping config.</summary>
    public ICommand AddStableFieldCommand    { get; }

    /// <summary>Removes a stable field from the grouping config.</summary>
    public ICommand RemoveStableFieldCommand { get; }

    private readonly Dictionary<MatchConditionRowViewModel, EventHandler> _ruleHandlers = new();

    private CancellationTokenSource? _debounceCts;

    public BurstProfileCardViewModel(
        BurstProfile profile,
        BurstProfileLoader profileLoader,
        IBurstVisualizerService visualizerService,
        IExifFieldAnalyzerService scannerService,
        BurstMatchingEngine matchingEngine,
        Func<string, IProgress<ExifScanProgress>?, CancellationToken, Task<VisualizerData>> loadFolderCached)
    {
        _profileLoader = profileLoader;

        _originalName        = profile.Name;
        _originalDescription = profile.Description ?? string.Empty;
        _originalPriority    = profile.Priority;
        _originalColorIndex  = profile.ColorIndex;

        _name        = _originalName;
        _description = _originalDescription;
        _priority    = _originalPriority;
        _colorIndex  = _originalColorIndex;

        Scanner    = new ExifScannerViewModel(scannerService);
        Visualizer = new VisualizerViewModel(visualizerService, loadFolderCached);

        Scanner.OnFieldsSelectedForRules = AddRulesFromFields;

        LoadRulesFromProfile(profile);

        Grouping.LoadFrom(profile.Grouping);

        Grouping.Changed += (_, _) => OnRuleOrGroupingChanged();

        Visualizer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VisualizerViewModel.Data) && Visualizer.Data != null)
                OnRuleOrGroupingChanged();
        };

        SaveCommand         = new RelayCommand(ExecuteSave);
        RevertCommand       = new RelayCommand(ExecuteRevert);
        AddRuleCommand      = new RelayCommand(ExecuteAddEmptyRule);
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        EditCommand         = new RelayCommand(() => IsEditing = true);
        CancelCommand       = new RelayCommand(ExecuteCancel);
        SetColorIndexCommand = new RelayCommand<object>(param =>
        {
            if (param is int idx)
                ColorIndex = idx;
            else if (param is string s && int.TryParse(s, out var parsed))
                ColorIndex = parsed;
        });
        AddStableFieldCommand = new RelayCommand<string>(fieldName =>
        {
            if (!string.IsNullOrWhiteSpace(fieldName) && !Grouping.StableFields.Contains(fieldName))
            {
                Grouping.StableFields.Add(fieldName);
                MarkDirty();
            }
        });
        RemoveStableFieldCommand = new RelayCommand<string>(fieldName =>
        {
            if (Grouping.StableFields.Remove(fieldName!))
                MarkDirty();
        });

        UpdateAvailableExifFields();

        Scanner.FieldGroups.CollectionChanged += (_, _) => UpdateAvailableExifFields();
    }

    private void ExecuteAddEmptyRule()
    {

        AddRuleRow(new MatchConditionRowViewModel(RemoveRule)
        {
            LogicOp  = MatchConditionRowViewModel.LogicOperators[0],
            Field    = string.Empty,
            Operator = MatchConditionRowViewModel.AvailableOperators[0],
            Value    = 0
        });
    }

    private void AddRulesFromFields(List<ExifFieldInfo> fields)
    {
        foreach (var field in fields)
        {

            AddRuleRow(new MatchConditionRowViewModel(RemoveRule)
            {
                LogicOp  = MatchConditionRowViewModel.LogicOperators[0],
                Field    = field.FieldName,
                Operator = MatchConditionRowViewModel.AvailableOperators[0],
                Value    = field.NumericValue ?? 0
            });
        }
    }

    private void AddRuleRow(MatchConditionRowViewModel row)
    {

        row.AvailableFields = AvailableExifFields;

        row.IsEditing = _isEditing;

        EventHandler handler = (_, _) => OnRuleOrGroupingChanged();
        _ruleHandlers[row] = handler;
        row.Changed += handler;
        Rules.Add(row);
        UpdateFirstRowFlags();
        UpdateAvailableExifFields();
        MarkDirty();
        OnRuleOrGroupingChanged();
    }

    private void RemoveRule(MatchConditionRowViewModel row)
    {

        if (_ruleHandlers.TryGetValue(row, out var handler))
        {
            row.Changed -= handler;
            _ruleHandlers.Remove(row);
        }
        Rules.Remove(row);
        UpdateFirstRowFlags();
        UpdateAvailableExifFields();
        MarkDirty();
        OnRuleOrGroupingChanged();
    }

    /// <summary>
    /// Sets IsFirstRow on each rule row based on its index.
    /// Called after every Add/Remove so the LogicOp ComboBox visibility stays correct.
    /// </summary>
    private void UpdateFirstRowFlags()
    {
        for (int i = 0; i < Rules.Count; i++)
            Rules[i].IsFirstRow = (i == 0);
    }

    /// <summary>
    /// Rebuilds AvailableExifFields from three merged sources (SSOT):
    ///   1. Built-in fields (BuiltInExifFields.All)
    ///   2. Fields currently used in existing rule rows
    ///   3. Numeric fields found by the EXIF Scanner (FieldGroups → numeric only)
    /// Result is deduplicated, sorted, and replaces the current collection contents.
    /// </summary>
    public void UpdateAvailableExifFields()
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in BuiltInExifFields.All)
            merged.Add(f);

        foreach (var rule in Rules)
            if (!string.IsNullOrWhiteSpace(rule.Field))
                merged.Add(rule.Field);

        foreach (var group in Scanner.FieldGroups)
            foreach (var field in group.Fields.Where(f => f.IsNumeric))
                merged.Add(field.FieldName);

        var sorted = merged.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        for (int i = AvailableExifFields.Count - 1; i >= 0; i--)
            if (!merged.Contains(AvailableExifFields[i]))
                AvailableExifFields.RemoveAt(i);

        foreach (var field in sorted)
            if (!AvailableExifFields.Contains(field))
                AvailableExifFields.Add(field);
    }

    private void LoadRulesFromProfile(BurstProfile profile)
    {

        foreach (var (row, handler) in _ruleHandlers)
            row.Changed -= handler;
        _ruleHandlers.Clear();
        Rules.Clear();

        if (profile.MatchConditions == null) return;

        FlattenGroup(profile.MatchConditions, parentOperator: "AND");
        UpdateFirstRowFlags();
    }

    /// <summary>
    /// Recursively flattens a nested ConditionGroup into flat rule rows.
    /// The first condition of a new group (when rows already exist) gets parentOperator
    /// as its LogicOp — this represents the connection BETWEEN groups (e.g. OR).
    /// Subsequent conditions within the same group get group.Operator (e.g. AND).
    /// </summary>
    private void FlattenGroup(ConditionGroup group, string parentOperator)
    {

        if (group.Conditions is { Count: > 0 })
        {
            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var condition = group.Conditions[i];

                string logicOp = (i == 0 && Rules.Count > 0) ? parentOperator : group.Operator;

                var row = new MatchConditionRowViewModel(RemoveRule)
                {
                    LogicOp         = logicOp,
                    Field           = condition.Field,
                    Operator        = condition.Operator,
                    Value           = condition.Value,
                    AvailableFields = AvailableExifFields,
                    IsEditing       = _isEditing
                };
                EventHandler handler = (_, _) => OnRuleOrGroupingChanged();
                _ruleHandlers[row] = handler;
                row.Changed += handler;
                Rules.Add(row);
            }
        }

        if (group.Groups is { Count: > 0 })
        {
            foreach (var subGroup in group.Groups)
                FlattenGroup(subGroup, group.Operator);
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }

    private async void ExecuteSave()
    {
        try
        {
            var profile = BuildProfile();
            await _profileLoader.SaveProfileAsync(profile);

            _originalName        = _name;
            _originalDescription = _description;
            _originalPriority    = _priority;
            _originalColorIndex  = _colorIndex;
            _isDirty             = false;

            OnPropertyChanged(nameof(IsDirty));
            SaveStatus = Strings.Common_Saved;
            IsEditing  = false;

            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            SaveStatus = string.Format(Strings.Common_ErrorFormat, ex.Message);
        }
    }

    private void ExecuteRevert()
    {
        Name        = _originalName;
        Description = _originalDescription;
        Priority    = _originalPriority;
        ColorIndex  = _originalColorIndex;

        var names   = new List<string> { _originalName };
        var loaded  = _profileLoader.LoadProfiles(names);
        var profile = loaded.FirstOrDefault();

        if (profile != null)
        {
            LoadRulesFromProfile(profile);
            Grouping.LoadFrom(profile.Grouping);
        }

        _isDirty = false;
        OnPropertyChanged(nameof(IsDirty));
        SaveStatus = null;
        IsEditing  = false;
    }

    /// <summary>
    /// Reverts all unsaved changes and exits edit mode.
    /// Triggered by the Cancel button or by collapsing the card while editing.
    /// </summary>
    private void ExecuteCancel()
    {
        ExecuteRevert();

    }

    /// <summary>Builds a BurstProfile from the current ViewModel state (for saving).</summary>
    public BurstProfile BuildProfile()
    {
        return new BurstProfile
        {
            Name             = Name,
            Description      = string.IsNullOrWhiteSpace(Description) ? null : Description,
            Priority         = Priority,
            ColorIndex       = ColorIndex,
            MatchConditions  = BuildConditionGroup(),
            Grouping         = Grouping.ToModel()
        };
    }

    internal ConditionGroup BuildConditionGroup()
    {
        if (Rules.Count == 0)
            return new ConditionGroup { Operator = "AND" };

        var groups = new List<List<MatchConditionRowViewModel>>();
        var current = new List<MatchConditionRowViewModel>();

        foreach (var rule in Rules)
        {
            if (rule.LogicOp == "OR" && current.Count > 0)
            {
                groups.Add(current);
                current = new List<MatchConditionRowViewModel>();
            }
            current.Add(rule);
        }
        groups.Add(current);

        if (groups.Count == 1)
        {
            return new ConditionGroup
            {
                Operator   = "AND",
                Conditions = groups[0].Select(r => new MatchCondition
                {
                    Field = r.Field, Operator = r.Operator, Value = r.Value
                }).ToList()
            };
        }

        return new ConditionGroup
        {
            Operator = "OR",
            Groups = groups.Select(g => new ConditionGroup
            {
                Operator   = "AND",
                Conditions = g.Select(r => new MatchCondition
                {
                    Field = r.Field, Operator = r.Operator, Value = r.Value
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Auto-evaluates this profile card with externally loaded data.
    /// Called by BurstTabViewModel when Burst Studio loads a folder.
    /// Reuses <see cref="BuildConditionGroup"/> and <see cref="VisualizerViewModel.SetDataAndEvaluateAsync"/>.
    /// </summary>
    public async Task AutoEvaluateAsync(VisualizerData data)
    {
        var conditions = BuildConditionGroup();
        var grouping   = Grouping.ToModel();
        await Visualizer.SetDataAndEvaluateAsync(data, conditions, grouping);
    }

    private async void OnRuleOrGroupingChanged()
    {

        MarkDirty();

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested && Visualizer.Data != null)
            {
                var conditions = BuildConditionGroup();
                var grouping   = Grouping.ToModel();
                await Visualizer.EvaluateAsync(conditions, grouping);
            }
        }
        catch (TaskCanceledException) { }
    }

}
