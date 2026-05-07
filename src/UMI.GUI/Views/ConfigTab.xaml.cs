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
/// Code-behind for ConfigTab.
/// DataContext flows from MainWindow (MainViewModel) — no own DataContext set here.
/// </summary>
public partial class ConfigTab : UserControl
{
    public ConfigTab()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Enter → Save, Escape → Cancel for the camera edit form.
    ///
    /// PreviewKeyDown is a tunneling event: it travels from the root (Window) down
    /// to the currently focused element. This handler is wired to the camera cards
    /// ItemsControl, which lies on the tunnel path for any focused TextBox inside it.
    ///
    /// We walk up the visual tree from the original source to find the first ancestor
    /// FrameworkElement that carries a CameraViewModel DataContext.
    /// </summary>
    private void CameraCards_PreviewKeyDown(object sender, KeyEventArgs e)
    {

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key != Key.Enter && key != Key.Escape)
            return;

        var cameraVm = FindCameraVm(e.OriginalSource as DependencyObject)
                    ?? FindCameraVm(sender as DependencyObject);

        if (cameraVm is null || !cameraVm.IsEditing)
            return;

        var mainVm = FindMainVm();
        if (mainVm is null) return;

        if (key == Key.Enter)
        {
            mainVm.SaveCameraCommand.Execute(cameraVm);
            e.Handled = true;
        }
        else if (key == Key.Escape)
        {
            mainVm.CancelCameraEditCommand.Execute(cameraVm);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Walks up the visual tree from <paramref name="obj"/> looking for a
    /// <see cref="FrameworkElement"/> whose DataContext is a <see cref="CameraViewModel"/>.
    /// </summary>
    private static CameraViewModel? FindCameraVm(DependencyObject? obj)
    {
        var current = obj;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is CameraViewModel vm)
                return vm;

            current = VisualTreeHelper.GetParent(current)
                   ?? (current is FrameworkContentElement fce ? fce.Parent : null);
        }
        return null;
    }

    /// <summary>
    /// Finds the MainViewModel from the Window's DataContext.
    /// </summary>
    private MainViewModel? FindMainVm()
    {
        var window = Window.GetWindow(this);
        return window?.DataContext as MainViewModel;
    }
}
