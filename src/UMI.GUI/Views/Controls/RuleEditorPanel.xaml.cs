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

using System.Windows.Controls;

namespace UMI.GUI.Views.Controls;

/// <summary>
/// Shared UserControl for the Rule Builder + Grouping editor sections of a BurstProfile.
///
/// DataContext: BurstProfileCardViewModel
///
/// Used by:
///   • BurstTab.xaml          — inside each profile card's expanded view
///   • BurstVisualizerV2.xaml — inline editor when a profile is selected
/// </summary>
public partial class RuleEditorPanel : UserControl
{
    public RuleEditorPanel()
    {
        InitializeComponent();
    }
}
