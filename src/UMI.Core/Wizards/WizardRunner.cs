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
/// Orchestriert eine geordnete Liste von Wizard-Steps.
/// Kümmert sich um Validierungsschleifen, optionale Steps und Abbruch-Handling.
/// </summary>
public class WizardRunner(IWizardRenderer renderer)
{
    private readonly IWizardRenderer _renderer = renderer;

    /// <summary>
    /// Führt alle Steps der Reihe nach durch.
    /// Bei Validierungsfehlern wird der Step wiederholt.
    /// Bei Ctrl+C / OperationCanceledException wird false zurückgegeben.
    /// </summary>
    /// <param name="steps">Geordnete Liste der auszuführenden Steps.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>True wenn der Wizard vollständig durchlaufen wurde, false bei Abbruch.</returns>
    public async Task<bool> RunAsync(
        IReadOnlyList<IWizardStep> steps,
        CancellationToken ct = default)
    {
        try
        {
            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();

                var completed = await RunStepAsync(step, ct).ConfigureAwait(false);
                if (!completed)
                    return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<bool> RunStepAsync(IWizardStep step, CancellationToken ct)
    {

        if (step.CanSkip)
        {
            var skip = await _renderer.AskSkipAsync(step.Title, step.Description, ct)
                .ConfigureAwait(false);

            if (skip)
                return true;
        }

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var values = await _renderer.RenderStepAsync(step, ct).ConfigureAwait(false);

            var result = await step.ValidateAsync(values, ct).ConfigureAwait(false);

            if (!result.IsValid)
            {

                var errorMsg = result.ErrorMessage ?? "Ungültige Eingabe. Bitte korrigieren.";
                await _renderer.ShowErrorAsync(errorMsg, ct).ConfigureAwait(false);
                continue;
            }

            await step.ApplyAsync(values, ct).ConfigureAwait(false);
            return true;
        }
    }
}
