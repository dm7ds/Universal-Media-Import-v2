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
using System.Windows.Controls;

namespace UMI.GUI.Views.Controls;

/// <summary>
/// ToolStatusIcon: 16×16 status indicator used in the Tools Config tab.
/// Shows a green checkmark when the tool is valid, a red X when it is
/// missing or not found.
///
/// Usage:
///   &lt;controls:ToolStatusIcon
///       IsValid="{Binding ExifToolIsValid}"
///       IsEmpty="{Binding ExifToolIsEmpty}"
///       Status="{Binding ExifToolStatus}" /&gt;
///
/// For rows that only have a checkmark (e.g. GPS Tracks):
///   &lt;controls:ToolStatusIcon
///       IsValid="{Binding GpsTrackFolderExists}"
///       ShowErrorIcon="False" /&gt;
/// </summary>
public partial class ToolStatusIcon : UserControl
{

    public static readonly DependencyProperty IsValidProperty =
        DependencyProperty.Register(
            nameof(IsValid),
            typeof(bool),
            typeof(ToolStatusIcon),
            new PropertyMetadata(false));

    public bool IsValid
    {
        get => (bool)GetValue(IsValidProperty);
        set => SetValue(IsValidProperty, value);
    }

    public static readonly DependencyProperty IsEmptyProperty =
        DependencyProperty.Register(
            nameof(IsEmpty),
            typeof(bool),
            typeof(ToolStatusIcon),
            new PropertyMetadata(false));

    public bool IsEmpty
    {
        get => (bool)GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    /// <summary>
    /// String status value. "NotFound" triggers the red X icon.
    /// Matches the string values used in ToolsViewModel (ExifToolStatus, GyroflowStatus, FFprobeStatus).
    /// </summary>
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status),
            typeof(string),
            typeof(ToolStatusIcon),
            new PropertyMetadata(string.Empty));

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>
    /// When False the red X is never shown (e.g. GPS Tracks row which only
    /// needs a checkmark, not an error indicator).
    /// Default: True.
    /// </summary>
    public static readonly DependencyProperty ShowErrorIconProperty =
        DependencyProperty.Register(
            nameof(ShowErrorIcon),
            typeof(bool),
            typeof(ToolStatusIcon),
            new PropertyMetadata(true));

    public bool ShowErrorIcon
    {
        get => (bool)GetValue(ShowErrorIconProperty);
        set => SetValue(ShowErrorIconProperty, value);
    }

    public ToolStatusIcon()
    {
        InitializeComponent();
    }
}
