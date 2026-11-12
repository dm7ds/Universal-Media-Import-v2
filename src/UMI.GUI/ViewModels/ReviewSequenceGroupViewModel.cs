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

namespace UMI.GUI.ViewModels;

/// <summary>
/// Eine Gruppe von Serien für die Overview-Grid-Gruppierung (I-017,
/// "Gruppieren nach: Typ"). Wenn <see cref="SequenceReviewerViewModel.GroupMode"/>
/// auf "keine" steht, gibt es genau EINE Gruppe mit leerem
/// <see cref="Name"/> — dieselbe XAML-Struktur (ein ItemsControl von Gruppen)
/// rendert dann flach, ohne Header (DRY: kein zweiter Card-Grid-Zweig in XAML).
/// </summary>
public sealed class ReviewSequenceGroupViewModel
{
    public string Name { get; }
    public IReadOnlyList<ReviewSequenceViewModel> Sequences { get; }
    public int Count => Sequences.Count;

    /// <summary>True wenn diese Gruppe einen sichtbaren Header haben soll (Name gesetzt).</summary>
    public bool HasHeader => !string.IsNullOrEmpty(Name);

    /// <summary>Header-Text "{Name} ({Count})" für die Gruppenüberschrift.</summary>
    public string HeaderText => string.Format(UMI.GUI.Resources.Strings.SequenceReviewer_GroupHeader, Name, Count);

    public ReviewSequenceGroupViewModel(string name, IReadOnlyList<ReviewSequenceViewModel> sequences)
    {
        Name      = name;
        Sequences = sequences;
    }
}
