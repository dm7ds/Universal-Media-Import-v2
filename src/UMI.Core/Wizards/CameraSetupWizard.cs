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

using System.Runtime.InteropServices;
using UMI.Core.Services;
using UMI.Core.Wizards.Steps;

namespace UMI.Core.Wizards;

/// <summary>
/// Wizard zum Anlegen einer neuen Kamera-Konfiguration.
/// Aufrufbar via:
///   - umi setup camera
///   - Aus dem First-Run-Wizard (Step 6 "Erste Kamera?")
///   - Spaeter aus der WPF GUI
/// </summary>
public class CameraSetupWizard
{
    private readonly IConfigWriterService _configWriter;

    private readonly CameraIdStep      _idStep;
    private readonly CameraTypeStep    _typeStep;
    private readonly SourceTypeStep    _sourceTypeStep;
    private readonly SourcePathStep    _sourcePathStep;
    private readonly FileTypesStep     _fileTypesStep;
    private readonly FeaturesStep      _featuresStep;
    private readonly ScanCardStep?     _scanCardStep;
    private readonly CameraSummaryStep _summaryStep;

    public CameraSetupWizard(IConfigWriterService configWriter)
    {
        _configWriter   = configWriter;

        _idStep         = new CameraIdStep(configWriter);
        _typeStep       = new CameraTypeStep();
        _sourceTypeStep = new SourceTypeStep();

        _sourcePathStep = new SourcePathStep(() => _sourceTypeStep.SelectedSourceType);

        _fileTypesStep  = new FileTypesStep(
            getCameraType: () => _typeStep.SelectedType,
            getTypeStep:   () => _typeStep);

        _featuresStep   = new FeaturesStep(
            getCameraType: () => _typeStep.SelectedType,
            getTypeStep:   () => _typeStep);

        _scanCardStep   = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ScanCardStep(configWriter, () => _idStep.CameraId)
            : null;

        _summaryStep    = new CameraSummaryStep(
            configWriter: configWriter,
            getCameraId:  () => _idStep.CameraId,
            buildConfig:  BuildCameraConfig);
    }

    /// <summary>
    /// True wenn Kamera erfolgreich hinzugefuegt wurde (nach WizardRunner.RunAsync).
    /// </summary>
    public bool CameraAdded => _summaryStep.CameraAdded;

    /// <summary>
    /// Gibt die geordnete Liste der Wizard-Steps zurueck.
    /// Steps 1-3 sind immer enthalten.
    /// Step 4 (SourcePath) ist dynamisch — haengt von Step 3 ab (aber immer im Step-Flow!
    ///   der Step zeigt nur bei MTP ein Info-Feld statt Pfad-Felder).
    /// Step 7 (ScanCard) wird nur bei SdCard UND Windows hinzugefuegt.
    /// Da WizardRunner die Liste einmalig bekommt, ist die SdCard-Bedingung zur Laufzeit.
    /// Loesung: ScanCardStep wird immer eingefuegt, aber CanSkip + Apply prueft SourceType.
    /// Einfacher: Wir bauen die Liste dynamisch aber NACH Step 3 — geht nicht mit statischer Liste.
    /// Beste Loesung: ScanCardStep nur wenn SdCard (via Func<bool> am CanSkip).
    /// Da IWizardStep.CanSkip kein Func ist: Nutze Wrapper-Pattern.
    /// </summary>
    public IReadOnlyList<IWizardStep> GetSteps()
    {
        var steps = new List<IWizardStep>
        {
            _idStep,
            _typeStep,
            _sourceTypeStep,
            _sourcePathStep,
            _fileTypesStep,
            _featuresStep,
        };

        if (_scanCardStep != null)
            steps.Add(new ConditionalScanCardWrapper(_scanCardStep, () => _sourceTypeStep.SelectedSourceType));

        steps.Add(_summaryStep);

        return steps;
    }

    /// <summary>
    /// Baut CameraConfig aus den gesammelten Step-Daten.
    /// Wird vom SummaryStep aufgerufen.
    /// </summary>
    private CameraConfig BuildCameraConfig()
    {
        var config = new CameraConfig
        {
            Name       = _idStep.CameraName,
            CameraType = _typeStep.SelectedType,
            Enabled    = true,
            SourceType = _sourceTypeStep.SelectedSourceType,
            Features   = _featuresStep.SelectedFeatures,
            FileTypes  = new CameraFileTypes
            {
                Video = _fileTypesStep.VideoExtensions,
                Photo = _fileTypesStep.PhotoExtensions
            },
            Paths = new CameraPaths
            {
                SdSource = _sourcePathStep.SdSource
            }
        };

        if (_sourceTypeStep.SelectedSourceType == SourceType.FixedPath)
        {
            config.SourcePath    = _sourcePathStep.SourcePath;
            config.FlattenSource = _sourcePathStep.FlattenSource;
        }

        return config;
    }
}

/// <summary>
/// Wrapper fuer ScanCardStep: Zeigt den Step nur wenn SourceType = SdCard.
/// Bei anderen Source-Typen wird CanSkip=true gesetzt und der Step ignoriert.
/// </summary>
internal class ConditionalScanCardWrapper(IWizardStep inner, Func<SourceType> getSourceType) : IWizardStep
{
    public string Title       => inner.Title;
    public string Description => inner.Description;

    /// <summary>
    /// CanSkip ist true wenn SourceType != SdCard — Step wird dann uebersprungen.
    /// Bei SdCard: CanSkip aus dem inneren Step (true = Nutzer kann selbst entscheiden).
    /// </summary>
    public bool CanSkip => getSourceType() != SourceType.SdCard || inner.CanSkip;

    public IReadOnlyList<WizardField> Fields => inner.Fields;

    public Task<WizardStepResult> ValidateAsync(Dictionary<string, object?> values, CancellationToken ct = default)
        => inner.ValidateAsync(values, ct);

    public Task ApplyAsync(Dictionary<string, object?> values, CancellationToken ct = default)
        => inner.ApplyAsync(values, ct);
}
