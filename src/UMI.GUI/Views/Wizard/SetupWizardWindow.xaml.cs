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
using UMI.GUI.ViewModels.Wizard;

namespace UMI.GUI.Views.Wizard;

/// <summary>
/// First-run Setup Wizard window.
/// Minimal code-behind — all logic lives in SetupWizardViewModel.
/// Window dragging is handled via beh:WindowDragBehavior on the title-bar Border (no code-behind handler).
/// </summary>
public partial class SetupWizardWindow : Window
{
    private readonly SetupWizardViewModel _viewModel;

    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.WizardCompleted += (_, _) => { DialogResult = true; Close(); };
        viewModel.WizardCancelled += (_, _) => { DialogResult = false; Close(); };

        ShowInTaskbar = Owner == null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
