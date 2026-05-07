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
/// Abschluss-Step des Camera-Setup-Wizards.
/// Zeigt Zusammenfassung und schreibt die Kamera via IConfigWriterService.
/// </summary>
public class CameraSummaryStep : IWizardStep
{
    private const string ConfirmKey = "confirm_add";

    private readonly IConfigWriterService _configWriter;
    private readonly Func<CameraConfig> _buildConfig;
    private readonly Func<string> _getCameraId;

    /// <summary>True nach Apply wenn Kamera erfolgreich hinzugefuegt wurde.</summary>
    public bool CameraAdded { get; private set; }

    public CameraSummaryStep(
        IConfigWriterService configWriter,
        Func<string> getCameraId,
        Func<CameraConfig> buildConfig)
    {
        _configWriter = configWriter;
        _getCameraId  = getCameraId;
        _buildConfig  = buildConfig;
    }

    /// <inheritdoc/>
    public string Title => "Zusammenfassung";

    /// <inheritdoc/>
    public string Description => "Pruefe alle Einstellungen und bestatige das Hinzufuegen der Kamera.";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields
    {
        get
        {
            var cameraId = _getCameraId();
            var config   = _buildConfig();

            var summary = BuildSummaryText(cameraId, config);

            return
            [
                new WizardField(
                    Key:   "summary_info",
                    Label: summary,
                    Type:  WizardFieldType.Info),

                new WizardField(
                    Key:          ConfirmKey,
                    Label:        "Kamera hinzufuegen?",
                    Type:         WizardFieldType.Toggle,
                    DefaultValue: true)
            ];
        }
    }

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
        => Task.FromResult(new WizardStepResult(true));

    /// <inheritdoc/>
    public async Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var confirmed = values.GetValueOrDefault(ConfirmKey) is bool b && b;

        if (!confirmed)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Kamera wurde nicht hinzugefuegt.");
            Console.ResetColor();
            CameraAdded = false;
            return;
        }

        var cameraId = _getCameraId();
        var config   = _buildConfig();

        _configWriter.AddCamera(cameraId, config);
        await _configWriter.SaveAsync(ct);

        CameraAdded = true;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Kamera '{cameraId}' wurde erfolgreich hinzugefuegt!");
        Console.WriteLine($"  Import starten mit: umi import --source {cameraId}");
        Console.ResetColor();
    }

    private static string BuildSummaryText(string cameraId, CameraConfig config)
    {
        var folderDisplay = string.IsNullOrEmpty(config.FolderName)
            ? $"{cameraId} (default)"
            : config.FolderName;

        var lines = new List<string>
        {
            $"Kamera-ID:    {cameraId}",
            $"Name:         {config.Name}",
            $"Ordnername:   {folderDisplay}",
            $"Typ:          {config.CameraType}",
            $"Quelle:       {FormatSourceType(config)}",
            $"Videos:       {(config.FileTypes.Video.Length > 0 ? string.Join(", ", config.FileTypes.Video) : "(keine)")}",
            $"Fotos:        {(config.FileTypes.Photo.Length > 0 ? string.Join(", ", config.FileTypes.Photo) : "(keine)")}",
            string.Empty,
            "Features:"
        };

        var activeFeatures = FeatureRegistry.All
            .Where(fi => config.Features.GetByKey(fi.Key))
            .ToList();

        if (activeFeatures.Count > 0)
        {
            foreach (var fi in activeFeatures)
                lines.Add($"  + {fi.Label}");
        }
        else
        {
            lines.Add("  (keine Features aktiviert)");
        }

        return string.Join("\n", lines);
    }

    private static string FormatSourceType(CameraConfig config) =>
        config.SourceType switch
        {
            SourceType.MTP       => "USB/MTP",
            SourceType.FixedPath => $"Fester Ordner: {config.SourcePath ?? "(nicht gesetzt)"}",
            SourceType.SdCard    => $"SD-Karte: {config.Paths.SdSource ?? "(Pfad folgt)"}",
            _                    => config.SourceType.ToString()
        };
}
