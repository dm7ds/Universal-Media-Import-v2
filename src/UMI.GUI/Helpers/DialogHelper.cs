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

using System.IO;

namespace UMI.GUI.Helpers;

/// <summary>
/// Shared UI dialog helpers for ViewModels.
/// </summary>
public static class DialogHelper
{
    /// <summary>
    /// Shows a folder browser dialog and returns the selected path,
    /// or <c>null</c> if the user cancelled or the selection was empty.
    /// </summary>
    /// <param name="title">Dialog window title.</param>
    /// <param name="initialDirectory">
    /// Optional initial directory. Ignored if null, empty, or the path does not exist.
    /// </param>
    public static string? BrowseFolder(string title, string? initialDirectory = null)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
            InitialDirectory = !string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory)
                ? initialDirectory : string.Empty
        };
        var owner = System.Windows.Application.Current.MainWindow;
        return dialog.ShowDialog(owner) == true && !string.IsNullOrWhiteSpace(dialog.FolderName)
            ? dialog.FolderName : null;
    }
}
