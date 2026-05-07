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
/// Lightweight ViewModel for a single feature bubble (clickable toggle pill).
///
/// BadgeKey maps to StaticResource brush pairs in Default.xaml:
///   "Gps"    → BrushBadgeGps    / BrushBadgeGpsBg
///   "Gyro"   → BrushBadgeGyro   / BrushBadgeGyroBg
///   "Burst"  → BrushBadgeBurst  / BrushBadgeBurstBg
///   "Meta"   → BrushBadgeMeta   / BrushBadgeMetaBg
///   "Eis"    → BrushBadgeEis    / BrushBadgeEisBg
///   "Lens"   → BrushBadgeLens   / BrushBadgeLensBg
///   "Post"   → BrushBadgePost   / BrushBadgePostBg
///   "Rename" → BrushBadgeRename / BrushBadgeRenameBg
///   "GoPro"  → BrushBadgeGoPro  / BrushBadgeGoProBg
///
/// The active/inactive Background and Foreground are resolved in CameraCard.xaml
/// via DataTrigger on IsEnabled — all from StaticResource keys, zero hex literals.
/// </summary>
public class FeatureBubbleViewModel : ViewModelBase
{
    /// <summary>
    /// Canonical feature key (e.g. "gps_injection").
    /// Used by CameraViewModel.GetEditFlag / SetEditFlag as the SSOT key for state lookup.
    /// </summary>
    public string FeatureKey { get; }

    /// <summary>Display text shown on the bubble pill (e.g. "GPS", "Burst").</summary>
    public string Label { get; }

    /// <summary>
    /// Badge style family key. Combined with "BrushBadge" prefix to resolve theme brushes.
    /// E.g. "Gps" → BrushBadgeGps (foreground) and BrushBadgeGpsBg (background).
    /// </summary>
    public string BadgeKey { get; }

    /// <summary>Tooltip text shown on hover.</summary>
    public string Tooltip { get; }

    /// <summary>Command executed when the bubble is clicked (toggles IsEnabled).</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>
    /// Command executed in Advanced section to toggle SimpleOnCard placement for this feature.
    /// Promotes (true) or demotes (false) the feature to/from the collapsed card's simple row.
    /// Wired by CameraViewModel — null only during design-time or unit test construction.
    /// </summary>
    public ICommand? ToggleSimpleOnCardCommand { get; set; }

    private bool _isEnabled;
    /// <summary>Whether this feature is currently enabled for import (active styling when True).</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private bool _isSimpleOnCard;
    /// <summary>
    /// Whether this feature should appear in the collapsed card's simple bubble row.
    /// True = pinned to simple view; False = Advanced section only.
    /// Reflects the effective value after per-camera overrides are applied.
    /// </summary>
    public bool IsSimpleOnCard
    {
        get => _isSimpleOnCard;
        set => SetProperty(ref _isSimpleOnCard, value);
    }

    public FeatureBubbleViewModel(
        string featureKey,
        string label,
        string badgeKey,
        string tooltip,
        ICommand toggleCommand,
        bool initiallyEnabled,
        bool isSimpleOnCard = false)
    {
        FeatureKey     = featureKey;
        Label          = label;
        BadgeKey       = badgeKey;
        Tooltip        = tooltip;
        ToggleCommand  = toggleCommand;
        _isEnabled     = initiallyEnabled;
        _isSimpleOnCard = isSimpleOnCard;
    }
}
