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

namespace UMI.GUI.Behaviors;

/// <summary>
/// Attached behavior for TextBox: executes a cancel command when the TextBox loses
/// keyboard focus, UNLESS the new focused element is the confirm button (identified
/// by the ConfirmButton attached property on the TextBox's parent).
/// This prevents LostFocus from cancelling the edit when the user clicks the confirm button.
/// </summary>
public static class CancelEditOnLostFocusBehavior
{

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.RegisterAttached(
            "CancelCommand",
            typeof(ICommand),
            typeof(CancelEditOnLostFocusBehavior),
            new PropertyMetadata(null, OnCancelCommandChanged));

    public static ICommand? GetCancelCommand(DependencyObject obj)
        => (ICommand?)obj.GetValue(CancelCommandProperty);

    public static void SetCancelCommand(DependencyObject obj, ICommand? value)
        => obj.SetValue(CancelCommandProperty, value);

    public static readonly DependencyProperty ConfirmButtonNameProperty =
        DependencyProperty.RegisterAttached(
            "ConfirmButtonName",
            typeof(string),
            typeof(CancelEditOnLostFocusBehavior),
            new PropertyMetadata(null));

    public static string? GetConfirmButtonName(DependencyObject obj)
        => (string?)obj.GetValue(ConfirmButtonNameProperty);

    public static void SetConfirmButtonName(DependencyObject obj, string? value)
        => obj.SetValue(ConfirmButtonNameProperty, value);

    private static void OnCancelCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        textBox.LostKeyboardFocus -= OnLostKeyboardFocus;

        if (e.NewValue is ICommand)
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        var cancelCommand = GetCancelCommand(textBox);
        if (cancelCommand == null) return;

        if (e.NewFocus is DependencyObject newFocused)
        {

            var element = newFocused;
            while (element != null)
            {
                if (element is Button btn && btn.Name == "ConfirmNameButton")
                    return;

                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }
        }

        if (cancelCommand.CanExecute(null))
            cancelCommand.Execute(null);
    }
}
