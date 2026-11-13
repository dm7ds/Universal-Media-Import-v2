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

using System.Windows.Input;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Inhalt einer Ja/Nein-Rueckfrage im Stil der App-Dialoge (siehe <c>ConfirmDialog</c>).
/// Die sichere Antwort ("Abbrechen") ist im Dialog Default- und Esc-Taste; die riskante
/// Antwort ("Bestaetigen") muss bewusst gewaehlt werden.
/// </summary>
public class ConfirmDialogViewModel : ViewModelBase
{
    public string Title { get; }
    public string Message { get; }

    /// <summary>Beschriftung der riskanten Antwort (fuehrt die Aktion aus).</summary>
    public string ConfirmText { get; }

    /// <summary>Beschriftung der sicheren Antwort (Default-Taste, Esc).</summary>
    public string CancelText { get; }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public event EventHandler? Confirmed;
    public event EventHandler? CancelRequested;

    public ConfirmDialogViewModel(string title, string message, string confirmText, string cancelText)
    {
        Title       = title;
        Message     = message;
        ConfirmText = confirmText;
        CancelText  = cancelText;

        ConfirmCommand = new RelayCommand(() => Confirmed?.Invoke(this, EventArgs.Empty));
        CancelCommand  = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
    }
}
