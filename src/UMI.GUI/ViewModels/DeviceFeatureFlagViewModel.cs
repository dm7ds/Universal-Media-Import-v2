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
/// ViewModel for a single feature flag row in the expanded Device card.
/// Shows the feature name and current on/off state for the assigned camera.
/// IsEnabled is editable when the parent DeviceEntryViewModel is in edit mode.
///
/// Read/write lifecycle:
///   - Constructed from the camera's current CameraFeatures state (GetFlag).
///   - IsEnabled setter marks the entry as modified (tracked by DeviceEntryViewModel.IsDirty).
///   - DeviceEntryViewModel.ApplyEdits() writes all flags back to CameraFeatures.
/// </summary>
public class DeviceFeatureFlagViewModel : ViewModelBase
{
    /// <summary>Canonical feature key (e.g. "gps_injection"). SSOT via FeatureLabels.</summary>
    public string FeatureKey { get; }

    /// <summary>Full display label (e.g. "GPS Injection"). From FeatureLabels.All.</summary>
    public string Label { get; }

    private bool _isEnabled;
    /// <summary>Current on/off state for this feature on the assigned camera.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
                IsModified = _isEnabled != _originalEnabled;
        }
    }

    private bool _isModified;
    /// <summary>True when IsEnabled differs from the value at construction time.</summary>
    public bool IsModified
    {
        get => _isModified;
        private set => SetProperty(ref _isModified, value);
    }

    private readonly bool _originalEnabled;

    /// <summary>
    /// True when this feature is classified as "simple" (maps to FeatureDefinition.SimpleOnCard).
    /// Simple features are shown directly in the card body; others go into the collapsible
    /// Advanced section. Set at construction time from the profile definition — read-only.
    /// When no type loader is available (fallback mode), defaults to false (everything in Advanced).
    /// </summary>
    public bool IsSimple { get; }

    public DeviceFeatureFlagViewModel(string featureKey, string label, bool isEnabled, bool isSimple = false)
    {
        FeatureKey       = featureKey;
        Label            = label;
        _isEnabled       = isEnabled;
        _originalEnabled = isEnabled;
        IsSimple         = isSimple;
    }

    /// <summary>Reverts IsEnabled to the original (construction-time) value.</summary>
    public void Revert()
    {
        IsEnabled  = _originalEnabled;
        IsModified = false;
    }
}
