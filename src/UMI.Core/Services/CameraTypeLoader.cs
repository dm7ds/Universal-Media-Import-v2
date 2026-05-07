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
using Microsoft.Extensions.Logging;
using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Lädt Kamera-Typ-Definitionen aus config/presets/types/*.umi.
/// Cached die geladenen Typen für Performance.
/// Schema v2: per-feature objects { available, enabled_by_default, simple_on_card }.
/// </summary>
public class CameraTypeLoader
{
    private readonly string _typesDir;
    private readonly ILogger? _logger;
    private Dictionary<string, CameraTypeDefinition>? _cache;

    public CameraTypeLoader(ConfigPathResolver paths, ILogger? logger = null)
    {
        _typesDir = paths.TypePresetsDir;
        _logger = logger;
    }

    /// <summary>
    /// Lädt alle Typ-Definitionen (cached nach erstem Aufruf).
    /// </summary>
    public Dictionary<string, CameraTypeDefinition> LoadAllTypes()
    {
        if (_cache != null) return _cache;

        _cache = new Dictionary<string, CameraTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_typesDir))
        {
            _logger?.LogWarning("Types-Verzeichnis nicht gefunden: {Path}", _typesDir);
            return _cache;
        }

        var files = Directory.GetFiles(_typesDir, "*.umi")
            .Concat(Directory.GetFiles(_typesDir, "*.json"))
            .DistinctBy(Path.GetFileNameWithoutExtension);

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var typeDef = JsonSerializer.Deserialize<CameraTypeDefinition>(json);
                if (typeDef != null)
                {
                    _cache[typeDef.Name] = typeDef;
                    _logger?.LogDebug("Typ geladen: {Name} ({Count} Features)",
                        typeDef.Name, typeDef.Features?.Count ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Typ-Definition fehlerhaft: {File}: {Error}", file, ex.Message);
            }
        }

        _logger?.LogDebug("{Count} Kamera-Typen geladen: {Types}",
            _cache.Count, string.Join(", ", _cache.Keys));

        return _cache;
    }

    /// <summary>
    /// Gibt Typ-Definition für einen Namen zurück (oder null).
    /// </summary>
    public CameraTypeDefinition? GetType(string typeName)
    {
        var types = LoadAllTypes();
        return types.GetValueOrDefault(typeName);
    }

    /// <summary>
    /// Listet alle verfügbaren Typ-Namen.
    /// </summary>
    public List<string> ListAvailableTypes()
    {
        return LoadAllTypes().Keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// Speichert eine Typ-Definition zurück in die .umi Datei (Schema v2).
    /// Aktualisiert das modified-Datum im _umi_header und invalidiert den Cache.
    /// </summary>
    public void SaveType(CameraTypeDefinition typeDef)
    {
        if (string.IsNullOrWhiteSpace(typeDef.Name))
            throw new ArgumentException("CameraTypeDefinition.Name must not be empty.", nameof(typeDef));

        Directory.CreateDirectory(_typesDir);
        var filePath = Path.Combine(_typesDir, $"{typeDef.Name}.umi");

        UmiHeader header;
        if (File.Exists(filePath))
        {
            try
            {
                var existingJson = File.ReadAllText(filePath);
                var existing = JsonSerializer.Deserialize<UmiFileWithHeader>(existingJson);
                header = existing?._umi_header ?? BuildDefaultHeader(typeDef.Name);
            }
            catch
            {
                header = BuildDefaultHeader(typeDef.Name);
            }
        }
        else
        {
            header = BuildDefaultHeader(typeDef.Name);
        }

        header.Modified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var wrapper = new UmiFileWithHeader
        {
            _umi_header    = header,
            Name           = typeDef.Name,
            Description    = typeDef.Description,
            Color          = typeDef.Color,
            Features       = typeDef.Features,
            SimpleFeatures = typeDef.SimpleFeatures.Count > 0 ? typeDef.SimpleFeatures : null,
            DefaultFileTypes = typeDef.DefaultFileTypes,
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var json = JsonSerializer.Serialize(wrapper, options);
        File.WriteAllText(filePath, json);
        _logger?.LogDebug("Typ gespeichert: {Name} → {Path}", typeDef.Name, filePath);

        InvalidateCache();
    }

    /// <summary>
    /// Invalidiert den In-Memory-Cache. Erzwingt beim nächsten Aufruf ein Neuladen vom Dateisystem.
    /// </summary>
    public void InvalidateCache() => _cache = null;

    private static UmiHeader BuildDefaultHeader(string typeName) => new()
    {
        Type          = "camera_type",
        Version       = "2.1.0",
        SchemaVersion = 2,
        Created       = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        Modified      = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        Description   = $"{typeName} Camera Type",
    };

    /// <summary>Wraps a CameraTypeDefinition together with the _umi_header for file I/O.</summary>
    private sealed class UmiFileWithHeader
    {
        [JsonPropertyName("_umi_header")]
        public UmiHeader? _umi_header { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("features")]
        public Dictionary<string, FeatureDefinition>? Features { get; set; }

        [JsonPropertyName("simple_features")]
        public List<string>? SimpleFeatures { get; set; }

        [JsonPropertyName("default_file_types")]
        public Models.DefaultFileTypes? DefaultFileTypes { get; set; }
    }

    private sealed class UmiHeader
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "camera_type";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.1.0";

        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 2;

        [JsonPropertyName("created")]
        public string? Created { get; set; }

        [JsonPropertyName("modified")]
        public string? Modified { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
