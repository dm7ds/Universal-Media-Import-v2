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
/// Wizard-Step fuer den Quell-Pfad — abhaengig vom gewaehlten SourceType.
/// - SdCard:    SD-Backup-Pfad (sd_source)
/// - FixedPath: source_path + flatten_source Toggle
/// - MTP:       Info-Step (kein Pfad noetig)
/// </summary>
public class SourcePathStep : IWizardStep
{
    private const string SdSourceKey      = "sd_source";
    private const string SourcePathKey    = "source_path";
    private const string FlattenSourceKey = "flatten_source";
    private const string MtpInfoKey       = "mtp_info";

    private readonly Func<SourceType> _getSourceType;

    /// <summary>Gesetzter SD-Quell-Pfad nach Apply (nur bei SdCard).</summary>
    public string? SdSource    { get; private set; }

    /// <summary>Gesetzter Quell-Pfad nach Apply (nur bei FixedPath).</summary>
    public string? SourcePath  { get; private set; }

    /// <summary>Flatten-Source Toggle nach Apply (nur bei FixedPath).</summary>
    public bool FlattenSource  { get; private set; } = true;

    /// <summary>
    /// Erstellt den Step.
    /// <paramref name="getSourceType"/> wird zur Laufzeit aufgerufen damit der Typ
    /// aus Step 3 (SourceTypeStep) noch nicht bekannt sein muss.
    /// </summary>
    public SourcePathStep(Func<SourceType> getSourceType)
    {
        _getSourceType = getSourceType;
    }

    /// <inheritdoc/>
    public string Title => _getSourceType() switch
    {
        SourceType.MTP      => "MTP-Geraet (Info)",
        SourceType.FixedPath => "Quell-Ordner",
        _                   => "SD-Karten Quell-Pfad"
    };

    /// <inheritdoc/>
    public string Description => _getSourceType() switch
    {
        SourceType.MTP       => "MTP-Geraete werden automatisch erkannt.",
        SourceType.FixedPath => "Ordner der dauerhaft nach neuen Medien gescannt wird.",
        _                    => "Pfad zum DCIM-Ordner oder SD-Backup-Verzeichnis."
    };

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields => _getSourceType() switch
    {
        SourceType.MTP => BuildMtpFields(),
        SourceType.FixedPath => BuildFixedPathFields(),
        _ => BuildSdCardFields()
    };

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        switch (_getSourceType())
        {
            case SourceType.MTP:
                return Task.FromResult(new WizardStepResult(true));

            case SourceType.FixedPath:
            {
                var path = values.GetValueOrDefault(SourcePathKey) as string;
                if (string.IsNullOrWhiteSpace(path))
                    return Task.FromResult(new WizardStepResult(false, "Quell-Ordner darf nicht leer sein."));

                path = Path.GetFullPath(path);
                if (!Directory.Exists(path))
                    return Task.FromResult(new WizardStepResult(
                        false, $"Quell-Ordner existiert nicht: {path}"));

                return Task.FromResult(new WizardStepResult(true));
            }

            default:
            {
                var path = values.GetValueOrDefault(SdSourceKey) as string;
                if (string.IsNullOrWhiteSpace(path))
                    return Task.FromResult(new WizardStepResult(false, "SD-Karten Quell-Pfad darf nicht leer sein."));

                return Task.FromResult(new WizardStepResult(true));
            }
        }
    }

    /// <inheritdoc/>
    public Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        switch (_getSourceType())
        {
            case SourceType.MTP:

                break;

            case SourceType.FixedPath:
            {
                var path = values.GetValueOrDefault(SourcePathKey) as string ?? string.Empty;
                SourcePath    = Path.GetFullPath(path);
                FlattenSource = values.GetValueOrDefault(FlattenSourceKey) is bool b ? b : true;
                break;
            }

            default:
            {
                var path = values.GetValueOrDefault(SdSourceKey) as string ?? string.Empty;
                SdSource = Path.GetFullPath(path);
                break;
            }
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<WizardField> BuildMtpFields() =>
    [
        new WizardField(
            Key:   MtpInfoKey,
            Label: "MTP-Geraete werden beim ersten Import automatisch erkannt und registriert.\n" +
                   "Kein Pfad noetig — UMI verbindet sich direkt mit dem Geraet.",
            Type:  WizardFieldType.Info)
    ];

    private static IReadOnlyList<WizardField> BuildSdCardFields() =>
    [
        new WizardField(
            Key:      SdSourceKey,
            Label:    "SD-Karten Backup-Pfad (DCIM-Ordner auf der SD-Karte)",
            Type:     WizardFieldType.Path,
            Required: true,
            HelpText: @"z.B. F:\DCIM oder E:\Backup\SD Karten\OA5")
    ];

    private static IReadOnlyList<WizardField> BuildFixedPathFields() =>
    [
        new WizardField(
            Key:      SourcePathKey,
            Label:    "Quell-Ordner (wo die Dateien liegen)",
            Type:     WizardFieldType.Path,
            Required: true,
            HelpText: @"z.B. D:\Dashcam\Kamera1 oder \\NAS\Kamera"),

        new WizardField(
            Key:          FlattenSourceKey,
            Label:        "Unterordner-Namen in Dateinamen einbauen? (verhindert Namenskollisionen)",
            Type:         WizardFieldType.Toggle,
            DefaultValue: true)
    ];
}
