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

using UMI.Core.Constants;
using UMI.Core.Services;

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Optionaler Wizard-Step fuer Gyroflow-Erkennung und -Konfiguration.
/// Kein Versions-Check (Gyroflow hat kein --version Flag).
/// </summary>
public class GyroflowStep(IConfigWriterService configWriter) : IWizardStep
{
    private const string FieldKey = "gyroflow_path";
    private const string ToggleKey = "gyroflow_use_found";

    private readonly string? _detectedPath = DetectGyroflow();

    /// <inheritdoc/>
    public string Title => "Gyroflow (optional)";

    /// <inheritdoc/>
    public string Description => "Gyroflow wird fuer Video-Stabilisierung benoetigt (optional).";

    /// <inheritdoc/>
    public bool CanSkip => true;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields
    {
        get
        {
            if (_detectedPath != null)
            {

                return
                [
                    new WizardField(
                        Key: ToggleKey,
                        Label: $"Gyroflow gefunden unter:\n  {_detectedPath}\n\nVerwenden?",
                        Type: WizardFieldType.Toggle,
                        DefaultValue: true)
                ];
            }

            return
            [
                new WizardField(
                    Key: FieldKey,
                    Label: "Pfad zu Gyroflow.exe (leer lassen zum Ueberspringen)",
                    Type: WizardFieldType.Path,
                    Required: false,
                    HelpText: "Gyroflow nicht gefunden. Download: https://gyroflow.xyz/")
            ];
        }
    }

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        if (_detectedPath != null)
        {

            return Task.FromResult(new WizardStepResult(true));
        }

        var path = values.GetValueOrDefault(FieldKey) as string;
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(new WizardStepResult(true));

        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            return Task.FromResult(new WizardStepResult(false, $"Gyroflow nicht gefunden: {path}"));

        return Task.FromResult(new WizardStepResult(true));
    }

    /// <inheritdoc/>
    public async Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        string? finalPath = null;

        if (_detectedPath != null)
        {

            var use = values.GetValueOrDefault(ToggleKey) is bool b && b;
            if (use)
                finalPath = _detectedPath;
        }
        else
        {
            var manualPath = values.GetValueOrDefault(FieldKey) as string;
            if (!string.IsNullOrWhiteSpace(manualPath))
                finalPath = Path.GetFullPath(manualPath);
        }

        if (!string.IsNullOrEmpty(finalPath))
            configWriter.SetToolPath(ToolKeys.Gyroflow, finalPath);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Auto-Detect Gyroflow:
    /// 1. where gyroflow (PATH-Scan)
    /// 2. Bekannte Installations-Pfade
    /// </summary>
    private static string? DetectGyroflow()
    {

        var fromPath = ToolDetectionHelper.FindViaWhere("gyroflow");
        if (fromPath != null)
            return fromPath;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] knownPaths =
        [
            @"C:\Program Files\Gyroflow\Gyroflow.exe",
            Path.Combine(localAppData, "Gyroflow", "Gyroflow.exe")
        ];

        foreach (var candidate in knownPaths)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

}
