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

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für Config-Profile Management.
/// Profile sind Deltas zur Basis-Config (nur Overrides).
/// </summary>
public class ProfileService
{
    private readonly string _profilesDir;
    private readonly ILogger<ProfileService>? _logger;

    /// <summary>
    /// Pfad zum Profiles-Verzeichnis (Single Source of Truth).
    /// </summary>
    public string ProfilesDirectory => _profilesDir;

    public ProfileService(ConfigPathResolver paths, ILogger<ProfileService>? logger = null)
    {
        _profilesDir = paths.ProfilesDir;
        _logger = logger;

        if (!Directory.Exists(_profilesDir))
        {
            Directory.CreateDirectory(_profilesDir);
            _logger?.LogDebug("Profiles-Verzeichnis erstellt: {Path}", _profilesDir);
        }
    }

    /// <summary>
    /// Listet alle verfügbaren Profile auf.
    /// </summary>
    public List<ProfileInfo> ListProfiles()
    {
        var profiles = new List<ProfileInfo>();

        if (!Directory.Exists(_profilesDir))
            return profiles;

        var files = Directory.GetFiles(_profilesDir, "*.umi")
            .Concat(Directory.GetFiles(_profilesDir, "*.json"))
            .DistinctBy(Path.GetFileNameWithoutExtension);

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var node = JsonNode.Parse(json);

                if (node is not JsonObject obj)
                    continue;

                var name = Path.GetFileNameWithoutExtension(file);
                var description = obj["_description"]?.GetValue<string>();
                var createdStr = obj["_created"]?.GetValue<string>();

                var created = DateTime.Now;
                if (!string.IsNullOrEmpty(createdStr))
                {
                    DateTime.TryParse(createdStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal, out created);
                }

                profiles.Add(new ProfileInfo(name, description, created, file));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Fehler beim Laden von Profil: {File}", file);
            }
        }

        return profiles.OrderBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Lädt ein Profil (nur Delta, keine Deserialisierung).
    /// </summary>
    public JsonNode? LoadProfile(string profileName)
    {
        var profilePath = GetProfilePath(profileName);

        if (!File.Exists(profilePath))
        {
            _logger?.LogWarning("Profil nicht gefunden: {Profile}", profileName);
            return null;
        }

        try
        {
            var json = File.ReadAllText(profilePath);
            var node = JsonNode.Parse(json);

            _logger?.LogDebug("Profil geladen: {Profile}", profileName);
            return node;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Laden von Profil: {Profile}", profileName);
            return null;
        }
    }

    /// <summary>
    /// Speichert ein Profil (Delta-Config als JsonNode).
    /// </summary>
    public void SaveProfile(string profileName, JsonNode overrides, string? description = null)
    {
        if (overrides is not JsonObject obj)
        {
            throw new ArgumentException("Overrides muss ein JsonObject sein", nameof(overrides));
        }

        var profile = new JsonObject();
        profile["_profile"] = profileName;
        profile["_description"] = description ?? $"Custom profile {profileName}";
        profile["_created"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);

        foreach (var prop in obj)
        {

            if (prop.Key.StartsWith("_"))
                continue;

            profile.Add(prop.Key, prop.Value?.DeepClone());
        }

        var profilePath = GetProfilePath(profileName);
        var json = profile.ToJsonString(JsonDefaults.WriteOptions);
        File.WriteAllText(profilePath, json);

        _logger?.LogDebug("Profil gespeichert: {Profile} → {Path}", profileName, profilePath);
    }

    /// <summary>
    /// Löscht ein Profil.
    /// </summary>
    public bool DeleteProfile(string profileName)
    {
        var profilePath = GetProfilePath(profileName);

        if (!File.Exists(profilePath))
        {
            _logger?.LogWarning("Profil nicht gefunden (kann nicht gelöscht werden): {Profile}", profileName);
            return false;
        }

        try
        {
            File.Delete(profilePath);
            _logger?.LogDebug("Profil gelöscht: {Profile}", profileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Löschen von Profil: {Profile}", profileName);
            return false;
        }
    }

    /// <summary>
    /// Prüft ob ein Profil existiert.
    /// </summary>
    public bool ProfileExists(string profileName)
    {
        return File.Exists(GetProfilePath(profileName));
    }

    private string GetProfilePath(string profileName)
    {

        var sanitized = Path.GetFileNameWithoutExtension(profileName);

        var umiPath = Path.Combine(_profilesDir, $"{sanitized}.umi");
        var jsonPath = Path.Combine(_profilesDir, $"{sanitized}.json");

        return File.Exists(umiPath) ? umiPath : jsonPath;
    }
}

/// <summary>
/// Profil-Metadaten.
/// </summary>
public record ProfileInfo(
    string Name,
    string? Description,
    DateTime Created,
    string FilePath);
