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
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using UMI.Core.Utilities;

namespace UMI.Core.Configuration;

/// <summary>
/// Lädt und validiert die config.json mit Profile-Support.
/// Merge-Kette: config.json → Profile (Delta) → CLI-Flags (höchste Prio)
/// </summary>
public class ConfigLoader
{
    private readonly ILogger<ConfigLoader>? _logger;
    private readonly Services.ProfileService? _profileService;
    private readonly Services.ConfigPathResolver? _configPaths;

    public ConfigLoader(
        ILogger<ConfigLoader>? logger = null,
        Services.ProfileService? profileService = null,
        Services.CameraTypeLoader? typeLoader = null,
        Services.ConfigPathResolver? configPaths = null)
    {
        _logger = logger;
        _profileService = profileService;

        _configPaths = configPaths;
    }

    /// <summary>
    /// Lädt config.json mit optionalem Profil-Override.
    /// Merge-Kette: config.json → Profile (Delta) → CLI-Flags
    /// </summary>
    public async Task<UmiConfig> LoadAsync(string configPath = "config.json", string? profileName = null)
    {

        if (Path.GetFileName(configPath) == configPath)
        {
            configPath = FindConfigFile(configPath) ?? configPath;
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Konfigurationsdatei nicht gefunden: {configPath}");
        }

        _logger?.LogInformation("Lade Konfiguration: {Path}", configPath);

        try
        {

            var baseJson = await File.ReadAllTextAsync(configPath);
            var baseNode = JsonNode.Parse(baseJson);

            if (baseNode == null)
            {
                throw new InvalidOperationException("Config konnte nicht geparsed werden");
            }

            var mergedNode = baseNode;

            if (!string.IsNullOrEmpty(profileName) && _profileService != null)
            {
                var profileOverride = _profileService.LoadProfile(profileName);

                if (profileOverride == null)
                {
                    _logger?.LogWarning("Profil '{Profile}' nicht gefunden, verwende Basis-Config", profileName);
                }
                else
                {
                    _logger?.LogInformation("Wende Profil an: {Profile}", profileName);
                    mergedNode = DeepMerge(mergedNode, profileOverride);
                }
            }

            var modeWasExplicitlySet = mergedNode is JsonObject rootObj
                && rootObj["app_settings"] is JsonObject appSettingsNode
                && appSettingsNode.ContainsKey("mode");

            var config = mergedNode.Deserialize<UmiConfig>(JsonDefaults.ReadOptions);

            if (config == null)
            {
                throw new InvalidOperationException("Config konnte nicht deserialisiert werden");
            }

            config.AppSettings.ApplyLegacyMigration(modeWasExplicitlySet);

            ValidateConfig(config);

            _logger?.LogInformation("Konfiguration geladen: v{Version}, {Count} Kameras, Profil: {Profile}",
                config.Version, config.Cameras.Count, profileName ?? "(keine)");

            return config;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "JSON-Fehler beim Laden der Config");
            throw new InvalidOperationException($"Ungültige JSON-Konfiguration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deep Merge zweier JsonNodes (Override gewinnt).
    /// Überspringt Metadaten-Properties (_profile, _description, _created).
    /// </summary>
    private static JsonNode DeepMerge(JsonNode baseNode, JsonNode overrideNode)
    {

        if (baseNode is JsonObject baseObj && overrideNode is JsonObject overrideObj)
        {
            var merged = baseObj.DeepClone().AsObject();

            foreach (var prop in overrideObj)
            {

                if (prop.Key.StartsWith("_"))
                {
                    continue;
                }

                if (merged.ContainsKey(prop.Key) &&
                    merged[prop.Key] is JsonObject &&
                    prop.Value is JsonObject)
                {

                    merged[prop.Key] = DeepMerge(merged[prop.Key]!, prop.Value);
                }
                else
                {

                    merged.Remove(prop.Key);
                    merged.Add(prop.Key, prop.Value?.DeepClone());
                }
            }

            return merged;
        }

        return overrideNode.DeepClone();
    }

    /// <summary>
    /// Sucht config.json in neuer Struktur (config/config.json) mit Legacy-Fallback (Root/config.json).
    /// Nutzt ConfigPathResolver wenn verfügbar, sonst manuelle Suche.
    /// </summary>
    private string? FindConfigFile(string filename)
    {
        var searchPaths = new List<string>();

        if (_configPaths != null)
        {
            searchPaths.Add(_configPaths.ConfigFile);
        }
        else
        {

            var exePath = Environment.ProcessPath;
            var exeDir = !string.IsNullOrEmpty(exePath)
                ? Path.GetDirectoryName(exePath)!
                : Directory.GetCurrentDirectory();
            searchPaths.Add(Path.Combine(exeDir, "config", filename));
        }

        searchPaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "config", filename));

        var legacyExePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(legacyExePath))
        {
            var legacyExeDir = Path.GetDirectoryName(legacyExePath);
            if (!string.IsNullOrEmpty(legacyExeDir))
            {
                searchPaths.Add(Path.Combine(legacyExeDir, filename));
            }
        }
        searchPaths.Add(Path.Combine(Directory.GetCurrentDirectory(), filename));

        foreach (var path in searchPaths.Distinct())
        {
            if (File.Exists(path))
            {
                _logger?.LogDebug("Config gefunden: {Path}", path);
                return path;
            }
        }

        _logger?.LogDebug("Config nicht gefunden in: {Paths}", string.Join(", ", searchPaths));
        return null;
    }

    private void ValidateConfig(UmiConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.GlobalPaths.Workbench))
            throw new InvalidOperationException("global_paths.workbench fehlt in der Config");

        if (string.IsNullOrWhiteSpace(config.GlobalPaths.Projects))
            throw new InvalidOperationException("global_paths.projects fehlt in der Config");

        if (string.IsNullOrWhiteSpace(config.GlobalPaths.Tools.ExifTool))
            throw new InvalidOperationException("global_paths.tools.exiftool fehlt in der Config");

        if (!File.Exists(config.GlobalPaths.Tools.ExifTool))
            _logger?.LogWarning("ExifTool nicht gefunden: {Path}", config.GlobalPaths.Tools.ExifTool);

        if (!string.IsNullOrWhiteSpace(config.GlobalPaths.Tools.Gyroflow) &&
            !File.Exists(config.GlobalPaths.Tools.Gyroflow))
            _logger?.LogWarning("Gyroflow nicht gefunden: {Path}", config.GlobalPaths.Tools.Gyroflow);

        if (!string.IsNullOrWhiteSpace(config.GlobalPaths.Tools.FFprobe) &&
            !File.Exists(config.GlobalPaths.Tools.FFprobe))
            _logger?.LogWarning("FFprobe nicht gefunden: {Path}", config.GlobalPaths.Tools.FFprobe);
    }

    /// <summary>
    /// Speichert Config als JSON.
    /// ACHTUNG: Verlustbehaftet - unbekannte JSON-Felder gehen verloren!
    /// </summary>
    [Obsolete("Nutze IConfigWriterService.SaveAsync() für verlustfreies Speichern (GUI-ready)")]
    public async Task SaveAsync(UmiConfig config, string configPath = "config.json")
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

        await File.WriteAllTextAsync(configPath, json);
        _logger?.LogInformation("Konfiguration gespeichert: {Path}", configPath);
    }
}

