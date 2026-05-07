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

using UMI.Core.Services;

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Wizard-Step fuer Kamera-ID und Anzeigename.
/// Validiert ID auf Eindeutigkeit und erlaubte Zeichen.
/// </summary>
public class CameraIdStep(IConfigWriterService configWriter) : IWizardStep
{
    private const string IdKey   = "camera_id";
    private const string NameKey = "camera_name";

    /// <summary>Gesammelte Kamera-ID nach Apply.</summary>
    public string CameraId   { get; private set; } = string.Empty;

    /// <summary>Gesammelter Kamera-Name nach Apply.</summary>
    public string CameraName { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public string Title => "Kamera-ID und Name";

    /// <inheritdoc/>
    public string Description => "Eindeutiges Kuerzel und Anzeigename fuer die neue Kamera.";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields =>
    [
        new WizardField(
            Key:         IdKey,
            Label:       "Kamera-Kuerzel (z.B. GoPro11, MyDSLR, DroneX)",
            Type:        WizardFieldType.Text,
            Required:    true,
            HelpText:    "Kurz und eindeutig — wird als Ordnername und CLI-Parameter verwendet"),

        new WizardField(
            Key:         NameKey,
            Label:       "Kamera-Name (z.B. GoPro Hero 11 Black)",
            Type:        WizardFieldType.Text,
            Required:    true,
            HelpText:    "Anzeigename fuer Ausgaben und Konfiguration")
    ];

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var id   = values.GetValueOrDefault(IdKey)   as string ?? string.Empty;
        var name = values.GetValueOrDefault(NameKey) as string ?? string.Empty;

        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(new WizardStepResult(false, "Kamera-Kuerzel darf nicht leer sein."));

        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(new WizardStepResult(false, "Kamera-Name darf nicht leer sein."));

        if (!IsValidCameraId(id))
            return Task.FromResult(new WizardStepResult(
                false,
                $"'{id}' enthaelt ungueltige Zeichen. Erlaubt: Buchstaben, Ziffern und Unterstrich."));

        var existingCameras = configWriter.Config.Cameras;
        if (existingCameras.ContainsKey(id))
            return Task.FromResult(new WizardStepResult(
                false,
                $"Kamera '{id}' ist bereits in der Config vorhanden."));

        return Task.FromResult(new WizardStepResult(true));
    }

    /// <inheritdoc/>
    public Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        CameraId   = (values.GetValueOrDefault(IdKey)   as string ?? string.Empty).Trim();
        CameraName = (values.GetValueOrDefault(NameKey) as string ?? string.Empty).Trim();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Prueft ob eine Kamera-ID nur erlaubte Zeichen enthaelt (alphanumerisch + Unterstrich).
    /// </summary>
    private static bool IsValidCameraId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        foreach (var ch in id)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                return false;
        }

        return true;
    }
}
