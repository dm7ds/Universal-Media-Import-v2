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

using System.Text.Json;
using UMI.Core.Models;

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Wizard-Step fuer die Kamera-Typ-Auswahl.
/// Laedt Typ-Definitionen aus config/presets/types/*.umi.
/// Fallback auf hardcoded Typen wenn keine Dateien vorhanden.
/// </summary>
public class CameraTypeStep : IWizardStep
{
    private const string FieldKey = "camera_type";

    private static readonly string[] HardcodedTypes =
        ["Action", "Drone", "Mirrorless", "Compact", "Other"];

    private readonly IReadOnlyList<string> _availableTypes;
    private readonly Dictionary<string, CameraTypeDefinition> _typeDefinitions;

    /// <summary>Gewaehlter Kamera-Typ nach Apply.</summary>
    public string SelectedType { get; private set; } = string.Empty;

    /// <summary>Feature-Defaults des gewaehlten Typs (nach Apply).</summary>
    public CameraTypeDefinition? SelectedDefinition { get; private set; }

    public CameraTypeStep()
    {
        _typeDefinitions = LoadTypeDefinitions();
        _availableTypes  = _typeDefinitions.Keys.Count > 0
            ? [.. _typeDefinitions.Keys.OrderBy(k => k)]
            : HardcodedTypes;
    }

    /// <inheritdoc/>
    public string Title => "Kamera-Typ";

    /// <inheritdoc/>
    public string Description => "Waehle den Typ — er bestimmt sinnvolle Default-Einstellungen.";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields =>
    [
        new WizardField(
            Key:      FieldKey,
            Label:    "Kamera-Typ",
            Type:     WizardFieldType.Selection,
            Options:  _availableTypes,
            Required: true,
            HelpText: "Der Typ bestimmt sinnvolle Default-Einstellungen fuer Features und Dateiendungen")
    ];

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var type = values.GetValueOrDefault(FieldKey) as string;

        if (string.IsNullOrWhiteSpace(type))
            return Task.FromResult(new WizardStepResult(false, "Bitte einen Kamera-Typ waehlen."));

        return Task.FromResult(new WizardStepResult(true));
    }

    /// <inheritdoc/>
    public Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        SelectedType = values.GetValueOrDefault(FieldKey) as string ?? string.Empty;

        _typeDefinitions.TryGetValue(SelectedType, out var definition);
        SelectedDefinition = definition;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Laedt Typ-Definitionen aus config/presets/types/*.umi.
    /// Gibt leeres Dictionary bei Fehlern oder fehlenden Dateien zurueck.
    /// </summary>
    private static Dictionary<string, CameraTypeDefinition> LoadTypeDefinitions()
    {
        var result = new Dictionary<string, CameraTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        try
        {

            var searchDirs = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config", "presets", "types"),
                Path.Combine(AppContext.BaseDirectory, "config", "defaults", "types"),
                Path.Combine(Directory.GetCurrentDirectory(), "config", "presets", "types"),
                Path.Combine(Directory.GetCurrentDirectory(), "config", "defaults", "types")
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                foreach (var file in Directory.EnumerateFiles(dir, "*.umi"))
                {
                    try
                    {
                        var json    = File.ReadAllText(file);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var def     = JsonSerializer.Deserialize<CameraTypeDefinition>(json, options);

                        if (def?.Name != null && !result.ContainsKey(def.Name))
                            result[def.Name] = def;
                    }
                    catch
                    {

                    }
                }

                if (result.Count > 0)
                    break;
            }
        }
        catch
        {

        }

        if (result.Count > 0)
        {
            if (!result.ContainsKey("Compact"))
                result["Compact"] = new CameraTypeDefinition { Name = "Compact" };
            if (!result.ContainsKey("Other"))
                result["Other"] = new CameraTypeDefinition { Name = "Other" };
        }

        return result;
    }

    /// <summary>
    /// Gibt Feature-Defaults fuer einen bestimmten Typ zurueck.
    /// Liest <c>enabled_by_default</c> aus dem Preset-Feature-Dictionary (SSOT).
    /// Fallback: leere Features wenn keine Definition oder kein Features-Dictionary vorhanden.
    /// </summary>
    public static CameraFeatures GetDefaultFeatures(string cameraType, CameraTypeDefinition? definition)
    {

        return CameraFeatures.BuildFromPreset(definition?.Features);
    }

    /// <summary>
    /// Gibt Video-Extensions fuer einen bestimmten Typ zurueck.
    /// Liest aus dem Preset wenn eine Definition vorhanden ist.
    /// Fallback auf hardcoded Defaults wenn kein Preset oder kein default_file_types vorhanden.
    /// </summary>
    public static string GetDefaultVideoExtensions(string cameraType, CameraTypeDefinition? definition = null)
    {
        if (definition?.DefaultFileTypes?.Video is { Length: > 0 } presetVideo)
            return string.Join(", ", presetVideo);

        return cameraType.ToLowerInvariant() switch
        {
            "compact" => ".mp4, .mov, .avi",
            _         => ".mp4, .mov"
        };
    }

    /// <summary>
    /// Gibt Foto-Extensions fuer einen bestimmten Typ zurueck.
    /// Liest aus dem Preset wenn eine Definition vorhanden ist.
    /// Fallback auf hardcoded Defaults wenn kein Preset oder kein default_file_types vorhanden.
    /// </summary>
    public static string GetDefaultPhotoExtensions(string cameraType, CameraTypeDefinition? definition = null)
    {
        if (definition?.DefaultFileTypes?.Photo is { Length: > 0 } presetPhoto)
            return string.Join(", ", presetPhoto);

        return cameraType.ToLowerInvariant() switch
        {
            "mirrorless" => ".jpg, .jpeg, .cr3, .cr2, .arw, .nef",
            "drone"      => ".jpg, .jpeg, .dng",
            _            => ".jpg, .jpeg"
        };
    }
}
