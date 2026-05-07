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
/// Wizard-Step fuer die Quelle der Kamera-Dateien.
/// Mapped Anzeige-Texte auf SourceType enum.
/// </summary>
public class SourceTypeStep : IWizardStep
{
    private const string FieldKey = "source_type";

    private static readonly IReadOnlyList<string> Options =
    [
        "SD-Karte",
        "USB/MTP (direkt per Kabel)",
        "Fester Ordner (NAS, Dashcam)"
    ];

    /// <summary>Gewaehlter SourceType nach Apply.</summary>
    public SourceType SelectedSourceType { get; private set; } = SourceType.SdCard;

    /// <inheritdoc/>
    public string Title => "Quelle";

    /// <inheritdoc/>
    public string Description => "Wie werden die Dateien dieser Kamera uebertragen?";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields =>
    [
        new WizardField(
            Key:      FieldKey,
            Label:    "Quelle der Kamera-Dateien",
            Type:     WizardFieldType.Selection,
            Options:  Options,
            Required: true,
            HelpText: "Bestimmt wie UMI die Dateien findet und importiert")
    ];

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
        => Task.FromResult(new WizardStepResult(true));

    /// <inheritdoc/>
    public Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var selected = values.GetValueOrDefault(FieldKey) as string ?? string.Empty;
        SelectedSourceType = ParseSourceType(selected);
        return Task.CompletedTask;
    }

    private static SourceType ParseSourceType(string displayText) =>
        displayText switch
        {
            "USB/MTP (direkt per Kabel)"    => SourceType.MTP,
            "Fester Ordner (NAS, Dashcam)"  => SourceType.FixedPath,
            _                               => SourceType.SdCard
        };
}
