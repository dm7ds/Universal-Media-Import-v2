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
using UMI.Core.Models;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the grouping configuration of a BurstProfile.
/// Exposes MaxGapSeconds, MinCount, AdaptiveThreshold, AdaptiveMultiplier as editable properties.
/// Notifies the parent card when any property changes (for IsDirty tracking and debounced evaluation).
/// </summary>
public class GroupingConfigViewModel : ViewModelBase
{
    public GroupingConfigViewModel()
    {
        StableFields.CollectionChanged += (_, _) => RaiseChanged();
    }

    private double _maxGapSeconds = 3.0;
    /// <summary>Maximum gap in seconds between photos to still be in the same sequence.</summary>
    public double MaxGapSeconds
    {
        get => _maxGapSeconds;
        set
        {
            if (SetProperty(ref _maxGapSeconds, value))
                RaiseChanged();
        }
    }

    private int _minCount = 3;
    /// <summary>Minimum number of photos in a group to count as a sequence.</summary>
    public int MinCount
    {
        get => _minCount;
        set
        {
            if (SetProperty(ref _minCount, value))
                RaiseChanged();
        }
    }

    private bool _adaptiveThreshold;
    /// <summary>When true, the effective threshold is avg gap * AdaptiveMultiplier (capped at MaxGapSeconds).</summary>
    public bool AdaptiveThreshold
    {
        get => _adaptiveThreshold;
        set
        {
            if (SetProperty(ref _adaptiveThreshold, value))
                RaiseChanged();
        }
    }

    private double _adaptiveMultiplier = 2.0;
    /// <summary>Multiplier for adaptive threshold calculation.</summary>
    public double AdaptiveMultiplier
    {
        get => _adaptiveMultiplier;
        set
        {
            if (SetProperty(ref _adaptiveMultiplier, value))
                RaiseChanged();
        }
    }

    /// <summary>EXIF fields that must be constant within a sequence.</summary>
    public ObservableCollection<string> StableFields { get; } = new();

    /// <summary>
    /// Raised when any property changes.
    /// BurstProfileCardViewModel subscribes to trigger debounced Visualizer re-evaluation.
    /// </summary>
    public event EventHandler? Changed;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Loads values from a GroupingConfig model.</summary>
    public void LoadFrom(GroupingConfig model)
    {
        _maxGapSeconds      = model.MaxGapSeconds;
        _minCount           = model.MinCount;
        _adaptiveThreshold  = model.AdaptiveThreshold;
        _adaptiveMultiplier = model.AdaptiveMultiplier;

        StableFields.Clear();
        if (model.StableFields != null)
            foreach (var f in model.StableFields)
                StableFields.Add(f);

        OnPropertyChanged(nameof(MaxGapSeconds));
        OnPropertyChanged(nameof(MinCount));
        OnPropertyChanged(nameof(AdaptiveThreshold));
        OnPropertyChanged(nameof(AdaptiveMultiplier));
    }

    /// <summary>Builds a GroupingConfig model from the current values.</summary>
    public GroupingConfig ToModel() => new GroupingConfig
    {
        MaxGapSeconds      = MaxGapSeconds,
        MinCount           = MinCount,
        AdaptiveThreshold  = AdaptiveThreshold,
        AdaptiveMultiplier = AdaptiveMultiplier,
        StableFields       = StableFields.Count > 0 ? StableFields.ToList() : null
    };
}
