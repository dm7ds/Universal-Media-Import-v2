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
/// Abstrahiert die Darstellung eines Wizards.
/// CLI-Implementierung nutzt Spectre.Console, GUI-Implementierung WPF-Dialoge.
/// </summary>
public interface IWizardRenderer
{
    /// <summary>
    /// Zeigt einen Wizard-Step an und sammelt die Nutzereingaben für alle Felder.
    /// </summary>
    /// <param name="step">Der anzuzeigende Step.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Dictionary mit den gesammelten Werten (Key = WizardField.Key).</returns>
    Task<Dictionary<string, object?>> RenderStepAsync(
        IWizardStep step,
        CancellationToken ct = default);

    /// <summary>
    /// Zeigt eine Zusammenfassung der gesammelten Werte und fragt nach Bestätigung.
    /// </summary>
    /// <param name="title">Überschrift der Zusammenfassung.</param>
    /// <param name="items">Label-Wert-Paare für die Anzeige.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>True wenn der Nutzer bestätigt, false bei Abbruch.</returns>
    Task<bool> ShowSummaryAsync(
        string title,
        IReadOnlyList<(string Label, string Value)> items,
        CancellationToken ct = default);

    /// <summary>
    /// Zeigt eine Fehlermeldung an.
    /// </summary>
    /// <param name="message">Die anzuzeigende Fehlermeldung.</param>
    /// <param name="ct">Abbruch-Token.</param>
    Task ShowErrorAsync(string message, CancellationToken ct = default);

    /// <summary>
    /// Zeigt eine Info- oder Willkommens-Nachricht an.
    /// </summary>
    /// <param name="title">Überschrift der Nachricht.</param>
    /// <param name="message">Anzuzeigender Text.</param>
    /// <param name="ct">Abbruch-Token.</param>
    Task ShowInfoAsync(string title, string message, CancellationToken ct = default);

    /// <summary>
    /// Fragt den Nutzer ob ein optionaler Step uebersprungen werden soll.
    /// Klare Semantik: True = ueberspringen, False = ausfuehren.
    /// </summary>
    /// <param name="stepTitle">Titel des optionalen Steps.</param>
    /// <param name="description">Kurze Beschreibung was konfiguriert wird.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>True wenn der Nutzer den Step ueberspringen moechte.</returns>
    Task<bool> AskSkipAsync(string stepTitle, string description, CancellationToken ct = default);
}
