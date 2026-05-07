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
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Lädt camera-models.json und identifiziert Kameras anhand von Make/Model.
///
/// Lookup-Reihenfolge:
/// 1. known_models — exakter Model-Key (case-insensitiv)
/// 2. make_rules — Make-Vergleich + Regex auf Model
/// 3. null — kein Match
/// </summary>
public sealed class CameraModelDatabase : ICameraModelDatabase
{

    private sealed class CameraModelsFile
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("known_models")]
        public Dictionary<string, KnownModelEntry> KnownModels { get; set; } = new();

        [JsonPropertyName("make_rules")]
        public List<MakeRuleEntry> MakeRules { get; set; } = new();

        [JsonPropertyName("volume_label_rules")]
        public List<VolumeLabelRuleEntry> VolumeLabelRules { get; set; } = new();
    }

    private sealed class KnownModelEntry
    {
        [JsonPropertyName("camera_type")]
        public required string CameraType { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private sealed class MakeRuleEntry
    {
        [JsonPropertyName("make")]
        public required string Make { get; set; }

        [JsonPropertyName("model_pattern")]
        public required string ModelPattern { get; set; }

        [JsonPropertyName("camera_type")]
        public required string CameraType { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private sealed class VolumeLabelRuleEntry
    {
        [JsonPropertyName("pattern")]
        public required string Pattern { get; set; }

        [JsonPropertyName("camera_type")]
        public required string CameraType { get; set; }

        [JsonPropertyName("make")]
        public string? Make { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private readonly record struct CompiledMakeRule(
        string Make,
        Regex ModelRegex,
        string CameraType,
        string? DisplayName);

    private readonly record struct CompiledVolumeLabelRule(
        Regex LabelRegex,
        string CameraType,
        string? Make,
        string? DisplayName);

    private readonly Dictionary<string, KnownModelEntry> _knownModels;
    private readonly IReadOnlyList<CompiledMakeRule> _compiledMakeRules;
    private readonly IReadOnlyList<CompiledVolumeLabelRule> _compiledVolumeLabelRules;
    private readonly ILogger<CameraModelDatabase>? _logger;

    /// <summary>
    /// Erstellt eine neue CameraModelDatabase aus dem angegebenen JSON-Dateipfad.
    /// Wenn die Datei nicht existiert oder fehlerhaft ist, arbeitet die DB mit leerer Datenbasis
    /// (alle Lookups liefern null).
    /// </summary>
    public CameraModelDatabase(string jsonFilePath, ILogger<CameraModelDatabase>? logger = null)
    {
        _logger = logger;

        if (!File.Exists(jsonFilePath))
        {
            _logger?.LogDebug("camera-models.json nicht gefunden: {Path} — DB arbeitet leer", jsonFilePath);
            _knownModels = new Dictionary<string, KnownModelEntry>(StringComparer.OrdinalIgnoreCase);
            _compiledMakeRules = Array.Empty<CompiledMakeRule>();
            _compiledVolumeLabelRules = Array.Empty<CompiledVolumeLabelRule>();
            return;
        }

        try
        {
            var json = File.ReadAllText(jsonFilePath);
            var data = JsonSerializer.Deserialize<CameraModelsFile>(json, JsonDefaults.ReadOptions);

            if (data == null)
            {
                _logger?.LogWarning("camera-models.json ist leer oder konnte nicht deserialisiert werden");
                _knownModels = new Dictionary<string, KnownModelEntry>(StringComparer.OrdinalIgnoreCase);
                _compiledMakeRules = Array.Empty<CompiledMakeRule>();
                _compiledVolumeLabelRules = Array.Empty<CompiledVolumeLabelRule>();
                return;
            }

            _knownModels = new Dictionary<string, KnownModelEntry>(
                data.KnownModels,
                StringComparer.OrdinalIgnoreCase);

            var compiled = new List<CompiledMakeRule>(data.MakeRules.Count);
            foreach (var rule in data.MakeRules)
            {
                try
                {
                    var regex = new Regex(
                        rule.ModelPattern,
                        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100));

                    compiled.Add(new CompiledMakeRule(rule.Make, regex, rule.CameraType, rule.DisplayName));
                }
                catch (ArgumentException ex)
                {
                    _logger?.LogWarning(ex,
                        "Ungültiges Regex-Pattern in make_rules für Make '{Make}': {Pattern}",
                        rule.Make, rule.ModelPattern);
                }
            }

            _compiledMakeRules = compiled;

            var compiledLabelRules = new List<CompiledVolumeLabelRule>(data.VolumeLabelRules.Count);
            foreach (var rule in data.VolumeLabelRules)
            {
                try
                {
                    var regex = new Regex(
                        rule.Pattern,
                        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100));

                    compiledLabelRules.Add(new CompiledVolumeLabelRule(regex, rule.CameraType, rule.Make, rule.DisplayName));
                }
                catch (ArgumentException ex)
                {
                    _logger?.LogWarning(ex,
                        "Ungültiges Regex-Pattern in volume_label_rules: {Pattern}", rule.Pattern);
                }
            }

            _compiledVolumeLabelRules = compiledLabelRules;

            _logger?.LogDebug(
                "CameraModelDatabase geladen: {KnownCount} bekannte Modelle, {RuleCount} Make-Regeln, {LabelRuleCount} VolumeLabel-Regeln",
                _knownModels.Count, _compiledMakeRules.Count, _compiledVolumeLabelRules.Count);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "JSON-Fehler beim Laden von camera-models.json: {Path}", jsonFilePath);
            _knownModels = new Dictionary<string, KnownModelEntry>(StringComparer.OrdinalIgnoreCase);
            _compiledMakeRules = Array.Empty<CompiledMakeRule>();
            _compiledVolumeLabelRules = Array.Empty<CompiledVolumeLabelRule>();
        }
    }

    /// <inheritdoc/>
    public CameraModelMatch? Identify(string make, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        if (_knownModels.TryGetValue(model.Trim(), out var knownEntry))
        {
            return new CameraModelMatch(knownEntry.CameraType, knownEntry.DisplayName);
        }

        var makeTrimmed = make?.Trim() ?? string.Empty;
        var modelTrimmed = model.Trim();

        foreach (var rule in _compiledMakeRules)
        {
            if (!rule.Make.Equals(makeTrimmed, StringComparison.OrdinalIgnoreCase))
                continue;

            if (rule.ModelRegex.IsMatch(modelTrimmed))
            {
                return new CameraModelMatch(rule.CameraType, rule.DisplayName);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public CameraModelMatch? IdentifyByVolumeLabel(string volumeLabel)
    {
        if (string.IsNullOrWhiteSpace(volumeLabel))
            return null;

        var label = volumeLabel.Trim();

        foreach (var rule in _compiledVolumeLabelRules)
        {
            if (rule.LabelRegex.IsMatch(label))
            {

                return new CameraModelMatch(rule.CameraType, DisplayName: rule.DisplayName);
            }
        }

        return null;
    }
}
