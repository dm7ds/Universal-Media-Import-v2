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
using System.Windows.Input;
using System.Windows.Media;
using UMI.GUI.ViewModels;

namespace UMI.GUI.Views;

/// <summary>
/// Code-behind for the Devices sub-tab.
/// All logic is in DevicesTabViewModel — this file is intentionally minimal.
/// </summary>
public partial class DevicesTab : UserControl
{
    public DevicesTab()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Click on blank area in the expanded panel → collapse the tile.
    /// Click on interactive elements (TextBox, ComboBox, Button) → let them handle it normally.
    /// </summary>
    private void ExpandedPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {

        var current = e.OriginalSource as DependencyObject;
        var panel = sender as DependencyObject;

        while (current is not null && current != panel)
        {
            if (current is TextBox or ComboBox or Button or System.Windows.Controls.Primitives.ToggleButton
                or CheckBox or Slider or ComboBoxItem)
            {

                e.Handled = true;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        var vm = FindDeviceVm(sender as DependencyObject);
        if (vm is not null)
            vm.IsExpanded = false;

        e.Handled = true;
    }

    /// <summary>
    /// Enter → Save, Escape → Revert for the device edit form.
    ///
    /// PreviewKeyDown is a tunneling event: it travels from the root (Window) down
    /// to the currently focused element. This handler is wired to the expanded StackPanel,
    /// which lies on the tunnel path for any focused TextBox or ComboBox inside it.
    ///
    /// We walk up the visual tree from the original source to find the first ancestor
    /// FrameworkElement that carries a DeviceEntryViewModel DataContext. This handles
    /// the case where sender is an intermediate layout element in the DataTemplate.
    /// </summary>
    private void ExpandedPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key != Key.Enter && key != Key.Escape)
            return;

        var vm = FindDeviceVm(e.OriginalSource as DependencyObject)
              ?? FindDeviceVm(sender as DependencyObject);

        System.Diagnostics.Debug.WriteLine(
            $"[DevicesTab] PreviewKeyDown key={key} vm={vm?.Label ?? "null"} isEditing={vm?.IsEditing}");

        if (vm is null || !vm.IsEditing)
            return;

        if (key == Key.Enter)
        {
            vm.SaveCommand.Execute(null);
            e.Handled = true;
        }
        else if (key == Key.Escape)
        {
            vm.RevertCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Walks up the visual tree from <paramref name="obj"/> looking for a
    /// <see cref="FrameworkElement"/> whose DataContext is a <see cref="DeviceEntryViewModel"/>.
    /// </summary>
    private static DeviceEntryViewModel? FindDeviceVm(DependencyObject? obj)
    {
        var current = obj;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is DeviceEntryViewModel vm)
                return vm;

            current = VisualTreeHelper.GetParent(current)
                   ?? (current is FrameworkContentElement fce ? fce.Parent : null);
        }
        return null;
    }
}
