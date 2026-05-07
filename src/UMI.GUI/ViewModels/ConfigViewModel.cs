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

using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>ViewModel for the Config tab with sub-tab navigation.</summary>
public class ConfigViewModel : ViewModelBase
{
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
                OnPropertyChanged(nameof(HelpAnchor));
        }
    }

    /// <summary>Context-sensitive help anchor based on the selected settings sub-tab.</summary>
    public string HelpAnchor => _selectedTabIndex switch
    {
        2 => Strings.Help_AnchorDevices,
        3 => Strings.Help_AnchorTools,
        4 => Strings.Help_AnchorBurst,
        _ => Strings.Help_AnchorSettings
    };

    /// <summary>ViewModel for the Profiles sub-tab (index 1).</summary>
    public ProfilesTabViewModel Profiles { get; }

    /// <summary>ViewModel for the Devices sub-tab (index 2).</summary>
    public DevicesTabViewModel Devices { get; }

    /// <summary>ViewModel for the Tools sub-tab (index 3).</summary>
    public ToolsViewModel Tools { get; }

    /// <summary>ViewModel for the Burst sub-tab (index 4).</summary>
    public BurstTabViewModel BurstTab { get; }

    public ConfigViewModel(
        ToolsViewModel toolsViewModel,
        ProfilesTabViewModel profilesViewModel,
        DevicesTabViewModel devicesTabViewModel,
        BurstTabViewModel burstTabViewModel)
    {
        Tools    = toolsViewModel;
        Profiles = profilesViewModel;
        Devices  = devicesTabViewModel;
        BurstTab = burstTabViewModel;
    }
}
