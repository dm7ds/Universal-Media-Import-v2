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
using UMI.Core.Services;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels;
using UMI.GUI.Views;

namespace UMI.GUI.Services;

/// <summary>
/// Zeigt die Rueckfrage "Beenden bei laufendem Import?" (I-028) als <see cref="ConfirmDialog"/>,
/// modal ueber dem Hauptfenster. Sichere Antwort ("Nicht beenden") ist Default und Esc.
/// </summary>
public sealed class WpfCloseConfirmation(Window owner) : ICloseConfirmation
{
    public Task<bool> ConfirmQuitWhileImportingAsync()
    {
        var viewModel = new ConfirmDialogViewModel(
            Strings.CloseGuard_Title,
            Strings.CloseGuard_Message,
            confirmText: Strings.CloseGuard_Quit,
            cancelText: Strings.CloseGuard_Keep);

        var dialog = new ConfirmDialog(viewModel) { Owner = owner };
        return Task.FromResult(dialog.ShowDialog() == true);
    }
}
