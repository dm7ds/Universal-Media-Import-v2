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

namespace UMI.Core.Services;

/// <summary>
/// Zentrale Pfad-Auflösung für alle Config/Preset-Dateien.
/// Ersetzt duplizierten Pfad-Bau-Code in Services und Commands.
/// </summary>
public class ConfigPathResolver
{
    private readonly string _configRoot;

    /// <summary>
    /// Initialisiert den Resolver.
    /// </summary>
    /// <param name="configRoot">Pfad zum config/ Verzeichnis (optional). Wenn null, wird auto-detected.</param>
    public ConfigPathResolver(string? configRoot = null)
    {
        if (configRoot != null)
        {
            _configRoot = Path.GetFullPath(configRoot);
        }
        else
        {

            var exePath = Environment.ProcessPath;
            var exeDir = !string.IsNullOrEmpty(exePath)
                ? Path.GetDirectoryName(exePath)!
                : Directory.GetCurrentDirectory();

            _configRoot = Path.Combine(exeDir, "config");
        }
    }

    /// <summary>Pfad zum config/ Root-Verzeichnis.</summary>
    public string Root => _configRoot;

    /// <summary>Pfad zu config/config.json.</summary>
    public string ConfigFile => Path.Combine(_configRoot, "config.json");

    /// <summary>Pfad zu config/config.template.json.</summary>
    public string TemplateFile => Path.Combine(_configRoot, "config.template.json");

    /// <summary>Pfad zu config/camera-models.json.</summary>
    public string CameraModelsFile => Path.Combine(_configRoot, "camera-models.json");

    /// <summary>Pfad zu config/presets/.</summary>
    public string PresetsRoot => Path.Combine(_configRoot, "presets");

    /// <summary>Pfad zu config/presets/burst/.</summary>
    public string BurstPresetsDir => Path.Combine(PresetsRoot, "burst");

    /// <summary>Pfad zu config/presets/types/.</summary>
    public string TypePresetsDir => Path.Combine(PresetsRoot, "types");

    /// <summary>Pfad zu config/presets/gyroflow/.</summary>
    public string GyroflowPresetsDir => Path.Combine(PresetsRoot, "gyroflow");

    /// <summary>Pfad zu config/presets/profiles/.</summary>
    public string ProfilesDir => Path.Combine(PresetsRoot, "profiles");

    /// <summary>Pfad zu config/defaults/.</summary>
    public string DefaultsRoot => Path.Combine(_configRoot, "defaults");

    /// <summary>Pfad zu config/defaults/config.default.json.</summary>
    public string DefaultConfigFile => Path.Combine(DefaultsRoot, "config.default.json");

    /// <summary>Pfad zu config/defaults/burst/.</summary>
    public string DefaultBurstDir => Path.Combine(DefaultsRoot, "burst");

    /// <summary>Pfad zu config/defaults/types/.</summary>
    public string DefaultTypesDir => Path.Combine(DefaultsRoot, "types");

    /// <summary>
    /// Standard-Pfad für das gebündelte ExifTool.
    /// SSOT — wird in ServiceCollectionExtensions, ToolsViewModel und ExifToolStep genutzt.
    /// </summary>
    public static string DefaultExifToolPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "exiftool", "exiftool.exe");

    /// <summary>
    /// Prüft ob die Config-Struktur existiert. Erstellt sie bei Bedarf.
    /// </summary>
    public void EnsureStructure()
    {
        Directory.CreateDirectory(_configRoot);
        Directory.CreateDirectory(PresetsRoot);
        Directory.CreateDirectory(BurstPresetsDir);
        Directory.CreateDirectory(TypePresetsDir);
        Directory.CreateDirectory(GyroflowPresetsDir);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(DefaultsRoot);
        Directory.CreateDirectory(DefaultBurstDir);
        Directory.CreateDirectory(DefaultTypesDir);
    }

    /// <summary>
    /// Legacy-Migration: Prüft ob alte Struktur existiert (config.json im ExeDir, Presets/ Ordner).
    /// </summary>
    public bool NeedsMigration()
    {
        var exeDir = Path.GetDirectoryName(_configRoot);
        if (exeDir == null)
            return false;

        var legacyConfig = Path.Combine(exeDir, "config.json");
        var legacyPresets = Path.Combine(exeDir, "Presets");

        return File.Exists(legacyConfig) || Directory.Exists(legacyPresets);
    }
}
