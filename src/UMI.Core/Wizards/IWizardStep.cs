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

namespace UMI.Core.Wizards;

/// <summary>
/// Repräsentiert einen einzelnen Schritt in einem Wizard-Dialog.
/// Implementierungen kapseln Felder, Validierung und die Anwendungslogik.
/// </summary>
public interface IWizardStep
{
    /// <summary>Titel des Steps (wird als Überschrift angezeigt).</summary>
    string Title { get; }

    /// <summary>Kurze Beschreibung was in diesem Step konfiguriert wird.</summary>
    string Description { get; }

    /// <summary>Liste der Eingabefelder die in diesem Step abgefragt werden.</summary>
    IReadOnlyList<WizardField> Fields { get; }

    /// <summary>True wenn der Nutzer diesen Step überspringen darf.</summary>
    bool CanSkip { get; }

    /// <summary>
    /// Validiert die gesammelten Eingaben. Wird VOR <see cref="ApplyAsync"/> aufgerufen.
    /// Bei ungültigen Eingaben wird der Step wiederholt.
    /// </summary>
    /// <param name="values">Eingabewerte (Key = WizardField.Key).</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Validierungsergebnis mit optionaler Fehlermeldung.</returns>
    Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default);

    /// <summary>
    /// Wendet die validierten Eingaben an (Config schreiben, Verzeichnisse anlegen, etc.).
    /// Wird nur aufgerufen wenn <see cref="ValidateAsync"/> IsValid = true zurückgibt.
    /// </summary>
    /// <param name="values">Validierte Eingabewerte (Key = WizardField.Key).</param>
    /// <param name="ct">Abbruch-Token.</param>
    Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default);
}
