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
using UMI.GUI.ViewModels;

namespace UMI.GUI.Views;

/// <summary>
/// Modal dialog for exporting tagged or rated photos from the Sequence Reviewer.
/// Delegates all logic to <see cref="ExportDialogViewModel"/>.
/// Sets <see cref="Window.DialogResult"/> to true on confirmed export.
/// </summary>
public partial class ExportDialog : Window
{
    /// <summary>Initialises the dialog and wires ViewModel events.</summary>
    public ExportDialog(ExportDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.ExportConfirmed += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        viewModel.CancelRequested += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
    }
}
