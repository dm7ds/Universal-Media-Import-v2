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

using System.Windows;

namespace UMI.GUI.Behaviors;

/// <summary>
/// Attached property that controls whether a camera card renders in "advanced" mode
/// (Settings/Camera tab: Rename, Advanced Features, Camera Type, Enabled) or in
/// "simple" mode (Import tab: Folder Name, Storage, Identity, Save/Revert only).
///
/// Using FrameworkPropertyMetadataOptions.Inherits means the value set on a parent
/// container (e.g. an ItemsControl in ConfigTab) is automatically inherited by all
/// descendant elements, including the CameraCard Border. CameraCard.xaml reads it
/// via RelativeSource FindAncestor.
///
/// Usage:
///   ConfigTab.xaml  → beh:CardMode.ShowAdvanced="True"  on the cameras ItemsControl
///   ImportTab.xaml  → no attribute needed (default is False)
/// </summary>
public static class CardMode
{
    public static readonly DependencyProperty ShowAdvancedProperty =
        DependencyProperty.RegisterAttached(
            "ShowAdvanced",
            typeof(bool),
            typeof(CardMode),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetShowAdvanced(DependencyObject obj)
        => (bool)obj.GetValue(ShowAdvancedProperty);

    public static void SetShowAdvanced(DependencyObject obj, bool value)
        => obj.SetValue(ShowAdvancedProperty, value);
}
