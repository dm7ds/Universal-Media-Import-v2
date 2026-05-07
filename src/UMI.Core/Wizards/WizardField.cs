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
/// Definiert den Eingabetyp eines Wizard-Feldes.
/// </summary>
public enum WizardFieldType
{
    /// <summary>Freitext-Eingabe.</summary>
    Text,

    /// <summary>Pfad-Eingabe mit Validierung (Pfad existiert oder wird erstellt).</summary>
    Path,

    /// <summary>Auswahl genau eines Wertes aus einer Liste.</summary>
    Selection,

    /// <summary>Mehrfachauswahl aus einer Liste (0..n Werte).</summary>
    MultiSelection,

    /// <summary>Ja/Nein-Umschalter.</summary>
    Toggle,

    /// <summary>Reine Anzeige — keine Eingabe.</summary>
    Info
}

/// <summary>
/// Beschreibt ein einzelnes Eingabefeld in einem Wizard-Step.
/// </summary>
/// <param name="Key">Eindeutiger Schlüssel im Values-Dictionary.</param>
/// <param name="Label">Beschriftung die dem Nutzer angezeigt wird.</param>
/// <param name="Type">Art der Eingabe.</param>
/// <param name="DefaultValue">Voreingestellter Wert (string, bool oder List&lt;string&gt; je nach Type).</param>
/// <param name="Options">Auswahl-Optionen für Selection und MultiSelection.</param>
/// <param name="Required">True wenn das Feld nicht leer bleiben darf.</param>
/// <param name="HelpText">Optionaler Hilfetext der unter dem Label angezeigt wird.</param>
public record WizardField(
    string Key,
    string Label,
    WizardFieldType Type,
    object? DefaultValue = null,
    IReadOnlyList<string>? Options = null,
    bool Required = false,
    string? HelpText = null);

/// <summary>
/// Ergebnis der Validierung eines Wizard-Steps.
/// </summary>
/// <param name="IsValid">True wenn alle Eingaben valide sind.</param>
/// <param name="ErrorMessage">Fehlermeldung bei ungültigen Eingaben, sonst null.</param>
public record WizardStepResult(
    bool IsValid,
    string? ErrorMessage = null);
