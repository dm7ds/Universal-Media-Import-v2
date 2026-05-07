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

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Erster Schritt des First-Run-Wizards: Begruessung und Uebersicht.
/// Reine Info-Anzeige, keine Eingabe.
/// </summary>
public class WelcomeStep : IWizardStep
{
    private const string WelcomeText =
        "Willkommen bei UMI - Universal Media Import!\n\n" +
        "Dieser Assistent richtet die Grundkonfiguration ein.\n" +
        "Du brauchst:\n" +
        "  * Einen Ordner fuer importierte Medien (Workbench)\n" +
        "  * Optional: Gyroflow fuer Video-Stabilisierung\n" +
        "  * Optional: Einen Ordner mit GPS-Tracks\n\n" +
        "Die Konfiguration wird in config/config.json gespeichert.\n" +
        "Du kannst den Wizard jederzeit mit 'umi setup' erneut starten.";

    /// <inheritdoc/>
    public string Title => "Willkommen bei UMI";

    /// <inheritdoc/>
    public string Description => "Ersteinrichtung - Grundkonfiguration";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields =>
    [
        new WizardField(
            Key: "welcome_info",
            Label: WelcomeText,
            Type: WizardFieldType.Info)
    ];

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
        => Task.FromResult(new WizardStepResult(IsValid: true));

    /// <inheritdoc/>
    public Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
        => Task.CompletedTask;
}
