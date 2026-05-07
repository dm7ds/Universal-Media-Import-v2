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
using UMI.Core.Wizards.Steps;

namespace UMI.Core.Wizards;

/// <summary>
/// Wizard fuer die UMI-Ersteinrichtung.
/// Konfiguriert Workbench-Pfad, ExifTool, optional Gyroflow und GPS-Quellordner.
/// </summary>
public class FirstRunWizard(IConfigWriterService configWriter)
{
    private readonly SetupCameraStep _cameraStep = new();

    /// <summary>
    /// True wenn der Nutzer eine Kamera einrichten moechte (wird von SetupCommand ausgewertet).
    /// </summary>
    public bool WantsCameraSetup => _cameraStep.WantsCameraSetup;

    /// <summary>
    /// Gibt die geordnete Liste der Wizard-Steps zurueck.
    /// </summary>
    public IReadOnlyList<IWizardStep> GetSteps() =>
    [
        new WelcomeStep(),
        new WorkbenchPathStep(configWriter),
        new ExifToolStep(configWriter),
        new GyroflowStep(configWriter),
        new GpxSourceStep(configWriter),
        _cameraStep
    ];
}
