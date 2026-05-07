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
/// Modal dialog for detecting and registering storage devices.
/// Delegates all logic to AddDeviceDialogViewModel.
/// Dialog stays open for batch registration; user closes explicitly.
/// </summary>
public partial class AddDeviceDialog : Window
{
    private readonly AddDeviceDialogViewModel _viewModel;

    public AddDeviceDialog(AddDeviceDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += (_, _) => Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _viewModel.StartDetection();
    }

    protected override void OnClosed(EventArgs e)
    {

        _viewModel.StopDetection();
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
