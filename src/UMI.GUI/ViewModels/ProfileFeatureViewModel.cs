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

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for a single feature row in the Profile editor.
/// Tracks whether the feature is available, a default feature, and whether
/// it appears on the collapsed camera card ("simple_on_card").
///
/// Schema v2: all three flags come from the per-feature FeatureDefinition object.
/// </summary>
public class ProfileFeatureViewModel : ViewModelBase
{
    /// <summary>Canonical feature key (e.g. "gps_injection").</summary>
    public string FeatureKey { get; }

    /// <summary>Full display label (e.g. "GPS Injection").</summary>
    public string Label { get; }

    /// <summary>Short bubble label (e.g. "GPS") — shown in camera card bubbles.</summary>
    public string BubbleLabel { get; }

    /// <summary>Badge style key for color resolution (e.g. "Gps").</summary>
    public string BadgeKey { get; }

    private bool _isAvailable;
    /// <summary>Whether this feature is visible and configurable for cameras of this profile type.</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (SetProperty(ref _isAvailable, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private bool _isDefault;
    /// <summary>Whether this feature is enabled by default for cameras of this profile type.</summary>
    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (SetProperty(ref _isDefault, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private bool _isSimple;
    /// <summary>
    /// Whether this feature appears on the collapsed camera card when enabled
    /// (maps to simple_on_card in the .umi file).
    /// </summary>
    public bool IsSimple
    {
        get => _isSimple;
        set
        {
            if (SetProperty(ref _isSimple, value))
                OnPropertyChanged(nameof(IsDirty));
        }
    }

    private bool _originalIsAvailable;
    private bool _originalIsDefault;
    private bool _originalIsSimple;

    /// <summary>True when any flag differs from the persisted definition.</summary>
    public bool IsDirty =>
        _isAvailable != _originalIsAvailable ||
        _isDefault   != _originalIsDefault   ||
        _isSimple    != _originalIsSimple;

    /// <summary>
    /// Commits the current values as the new baseline, so IsDirty returns false.
    /// Called by ProfileViewModel after a successful save.
    /// </summary>
    public void CommitBaseline()
    {
        _originalIsAvailable = _isAvailable;
        _originalIsDefault   = _isDefault;
        _originalIsSimple    = _isSimple;
        OnPropertyChanged(nameof(IsDirty));
    }

    public ProfileFeatureViewModel(
        string featureKey,
        string label,
        string bubbleLabel,
        string badgeKey,
        bool isAvailable,
        bool isDefault,
        bool isSimple)
    {
        FeatureKey  = featureKey;
        Label       = label;
        BubbleLabel = bubbleLabel;
        BadgeKey    = badgeKey;

        _isAvailable = isAvailable;
        _isDefault   = isDefault;
        _isSimple    = isSimple;

        _originalIsAvailable = isAvailable;
        _originalIsDefault   = isDefault;
        _originalIsSimple    = isSimple;
    }
}
