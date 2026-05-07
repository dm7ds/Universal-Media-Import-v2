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
/// Wizard-Step fuer den Workbench-Pfad (Zielordner fuer importierte Medien).
/// Erstellt den Ordner nach Benutzerbestaetigung falls er nicht existiert.
/// </summary>
public class WorkbenchPathStep(IConfigWriterService configWriter) : IWizardStep
{
    private const string FieldKey = "workbench";

    /// <inheritdoc/>
    public string Title => "Workbench-Ordner";

    /// <inheritdoc/>
    public string Description => "Wohin sollen importierte Medien kopiert werden?";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields =>
    [
        new WizardField(
            Key: FieldKey,
            Label: "Workbench-Ordner (hierhin werden Medien importiert)",
            Type: WizardFieldType.Path,
            Required: true,
            HelpText: "z.B. E:\\Workbench oder D:\\Media\\Import")
    ];

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var raw = values.GetValueOrDefault(FieldKey) as string;

        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult(new WizardStepResult(false, "Workbench-Pfad darf nicht leer sein."));

        var path = Path.GetFullPath(raw);

        if (Directory.Exists(path))
            return Task.FromResult(new WizardStepResult(true));

        var parent = Path.GetDirectoryName(path);
        if (parent != null && Directory.Exists(parent))
            return Task.FromResult(new WizardStepResult(true));

        return Task.FromResult(new WizardStepResult(
            false,
            $"Pfad '{path}' existiert nicht und der Elternordner ist nicht erreichbar."));
    }

    /// <inheritdoc/>
    public async Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var raw = values.GetValueOrDefault(FieldKey) as string ?? string.Empty;
        var path = Path.GetFullPath(raw);

        if (!Directory.Exists(path))
        {
            await Task.Run(() => Directory.CreateDirectory(path), ct);
        }

        configWriter.SetWorkbenchPath(path);
    }
}
