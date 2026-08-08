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
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>Was mit den aussortierten Fotos geschehen soll.</summary>
public enum DeleteMode
{
    /// <summary>In einen _Trash-Unterordner verschieben, Ordnerstruktur bleibt erhalten.</summary>
    MoveToTrashFolder,

    /// <summary>In den Windows-Papierkorb (ueber den Explorer wiederherstellbar).</summary>
    RecycleBin,

    /// <summary>Endgueltig loeschen — nicht wiederherstellbar.</summary>
    Permanent
}

/// <summary>
/// Abfrage vor dem Aussortieren im Sequenz-Reviewer (I-010).
///
/// Vorher lief der Loeschvorgang ohne jede Rueckfrage und ohne Papierkorb —
/// und erfasste dabei alles, was der aktive Filter zeigte, nicht nur die
/// Muell-Markierten. Diese Abfrage nennt deshalb immer die exakte Anzahl und
/// laesst die Wirkung bewusst waehlen; Vorauswahl ist die schonendste Variante.
/// </summary>
public class DeleteOptionsDialogViewModel : ViewModelBase
{
    /// <summary>Anzahl der betroffenen Fotos — wird im Dialogtext genannt.</summary>
    public int PhotoCount { get; }

    /// <summary>Zielordner der Verschiebe-Variante, damit der User weiss wohin.</summary>
    public string TrashFolderPath { get; }

    public string HeaderText => string.Format(Strings.DeleteOptions_Header, PhotoCount);

    public string MoveDescription => string.Format(Strings.DeleteOptions_MoveDescription, TrashFolderPath);

    private DeleteMode _selectedMode = DeleteMode.MoveToTrashFolder;
    /// <summary>Gewaehlte Wirkung. Default bewusst die schonendste (verschieben).</summary>
    public DeleteMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsMoveToTrashFolder));
                OnPropertyChanged(nameof(IsRecycleBin));
                OnPropertyChanged(nameof(IsPermanent));
                OnPropertyChanged(nameof(IsPermanentWarningVisible));
            }
        }
    }

    // Eigene Bool-Properties statt Enum-Converter im XAML: RadioButton.IsChecked
    // braucht Two-Way-Bindings, und ein Converter pro Wert waere hier reine
    // Zeremonie (NML: die drei Werte stehen bereits im Enum).
    public bool IsMoveToTrashFolder
    {
        get => SelectedMode == DeleteMode.MoveToTrashFolder;
        set { if (value) SelectedMode = DeleteMode.MoveToTrashFolder; }
    }

    public bool IsRecycleBin
    {
        get => SelectedMode == DeleteMode.RecycleBin;
        set { if (value) SelectedMode = DeleteMode.RecycleBin; }
    }

    public bool IsPermanent
    {
        get => SelectedMode == DeleteMode.Permanent;
        set { if (value) SelectedMode = DeleteMode.Permanent; }
    }

    /// <summary>Warnhinweis nur bei der unwiderruflichen Variante.</summary>
    public bool IsPermanentWarningVisible => SelectedMode == DeleteMode.Permanent;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public event EventHandler? Confirmed;
    public event EventHandler? CancelRequested;

    public DeleteOptionsDialogViewModel(int photoCount, string trashFolderPath)
    {
        PhotoCount      = photoCount;
        TrashFolderPath = trashFolderPath;

        ConfirmCommand = new RelayCommand(() => Confirmed?.Invoke(this, EventArgs.Empty));
        CancelCommand  = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
    }
}
