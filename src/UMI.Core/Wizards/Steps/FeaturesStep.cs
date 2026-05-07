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
/// Wizard-Step fuer die Feature-Auswahl der Kamera.
/// Defaults haengen vom gewaehlten Kamera-Typ ab.
/// Feature-Labels kommen aus <see cref="FeatureRegistry"/> (SSOT).
/// </summary>
public class FeaturesStep : IWizardStep
{
    private const string FieldKey = "features";

    private static readonly IReadOnlyList<string> AllFeatures =
        FeatureRegistry.All.Select(f => f.Label).ToList();

    private readonly Func<string> _getCameraType;
    private readonly Func<CameraTypeStep?> _getTypeStep;

    /// <summary>Konfigurierte CameraFeatures nach Apply.</summary>
    public CameraFeatures SelectedFeatures { get; private set; } = new();

    /// <summary>
    /// Erstellt den Step.
    /// <paramref name="getCameraType"/> liefert den Kamera-Typ aus Step 2.
    /// <paramref name="getTypeStep"/> liefert den CameraTypeStep fuer Definition-Zugriff.
    /// </summary>
    public FeaturesStep(Func<string> getCameraType, Func<CameraTypeStep?> getTypeStep)
    {
        _getCameraType = getCameraType;
        _getTypeStep   = getTypeStep;
    }

    /// <inheritdoc/>
    public string Title => "Features";

    /// <inheritdoc/>
    public string Description => "Welche Features sollen fuer diese Kamera aktiviert werden?";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields
    {
        get
        {
            var defaults = GetDefaultSelections();

            return
            [
                new WizardField(
                    Key:          FieldKey,
                    Label:        "Aktivierte Features",
                    Type:         WizardFieldType.MultiSelection,
                    Options:      AllFeatures,
                    DefaultValue: defaults,
                    Required:     false,
                    HelpText:     "Leertaste zum An-/Abwaehlen, Enter zum Bestaetigen")
            ];
        }
    }

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
        var selected = values.GetValueOrDefault(FieldKey) as List<string> ?? [];

        var features = new CameraFeatures();
        foreach (var fi in FeatureRegistry.All)
        {
            features.SetByKey(fi.Key, selected.Contains(fi.Label));
        }

        SelectedFeatures = features;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Bestimmt die vorausgewaehlten Features basierend auf dem Kamera-Typ.
    /// </summary>
    private List<string> GetDefaultSelections()
    {
        var cameraType = _getCameraType();
        var typeStep   = _getTypeStep();
        var features   = CameraTypeStep.GetDefaultFeatures(cameraType, typeStep?.SelectedDefinition);

        return FeatureRegistry.All
            .Where(fi => features.GetByKey(fi.Key))
            .Select(fi => fi.Label)
            .ToList();
    }
}
