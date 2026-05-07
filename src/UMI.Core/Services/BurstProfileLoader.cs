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
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Lädt Burst-Detection-Profile aus Presets/burst/ Verzeichnis.
/// Profile definieren Erkennungsregeln für Foto-Sequenzen (Sport, Astro, Timelapse, etc.).
/// </summary>
public class BurstProfileLoader
{
    private readonly string _presetsDir;
    private readonly ILogger? _logger;

    public BurstProfileLoader(ConfigPathResolver paths, ILogger? logger = null)
    {
        _presetsDir = paths.BurstPresetsDir;
        _logger = logger;
    }

    /// <summary>
    /// Lädt angeforderte Profile aus Presets/burst/ Verzeichnis.
    /// Profile werden nach Priority sortiert (niedriger = höhere Priorität).
    /// </summary>
    /// <param name="profileNames">Liste der Profile die geladen werden sollen</param>
    /// <returns>Sortierte Liste geladener Profile</returns>
    public List<BurstProfile> LoadProfiles(List<string> profileNames)
    {
        var profiles = new List<BurstProfile>();

        foreach (var name in profileNames)
        {

            var umiPath = Path.Combine(_presetsDir, $"{name}.umi");
            var jsonPath = Path.Combine(_presetsDir, $"{name}.json");
            var path = File.Exists(umiPath) ? umiPath : jsonPath;

            if (!File.Exists(path))
            {
                _logger?.LogWarning("Burst-Profil nicht gefunden: {Path}", path);
                continue;
            }

            try
            {
                var json = File.ReadAllText(path);
                var profile = JsonSerializer.Deserialize<BurstProfile>(json, JsonDefaults.ReadOptions);

                if (profile != null)
                {
                    profiles.Add(profile);
                    _logger?.LogDebug("Burst-Profil geladen: {Name} (Priority {Priority})",
                        profile.Name, profile.Priority);
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning("Burst-Profil fehlerhaft: {Path}: {Error}", path, ex.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Laden von Burst-Profil: {Path}", path);
            }
        }

        return profiles.OrderBy(p => p.Priority).ToList();
    }

    /// <summary>
    /// Speichert ein Profil als .umi-Datei im Presets-Verzeichnis.
    /// Erstellt das Verzeichnis falls nötig.
    /// </summary>
    public async Task SaveProfileAsync(BurstProfile profile)
    {
        Directory.CreateDirectory(_presetsDir);
        var path = Path.Combine(_presetsDir, $"{profile.Name}.umi");
        var json = JsonSerializer.Serialize(profile, JsonDefaults.WriteOptions);
        await File.WriteAllTextAsync(path, json);
        _logger?.LogInformation("Burst-Profil gespeichert: {Path}", path);
    }

    /// <summary>
    /// Löscht ein Profil (.umi und .json Legacy-Fallback).
    /// </summary>
    public void DeleteProfile(string name)
    {
        var umiPath = Path.Combine(_presetsDir, $"{name}.umi");
        var jsonPath = Path.Combine(_presetsDir, $"{name}.json");

        if (File.Exists(umiPath)) File.Delete(umiPath);
        if (File.Exists(jsonPath)) File.Delete(jsonPath);

        _logger?.LogInformation("Burst-Profil gelöscht: {Name}", name);
    }

    /// <summary>
    /// Listet alle verfügbaren Profile im Preset-Verzeichnis.
    /// Nützlich für GUI/CLI zur Auswahl verfügbarer Profile.
    /// </summary>
    /// <returns>Namen aller .umi/.json Dateien (ohne Extension)</returns>
    public List<string> ListAvailableProfiles()
    {
        if (!Directory.Exists(_presetsDir))
        {
            _logger?.LogWarning("Burst-Preset-Verzeichnis existiert nicht: {Path}", _presetsDir);
            return new List<string>();
        }

        return Directory.GetFiles(_presetsDir, "*.umi")
            .Concat(Directory.GetFiles(_presetsDir, "*.json"))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Select(n => n!)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }
}
