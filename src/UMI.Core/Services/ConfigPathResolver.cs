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
///
/// Storage layout (since v2.1.2):
///   - <c>%LOCALAPPDATA%\UMI\config\</c> — user-writable, holds <c>config.json</c>,
///     <c>config.json.bak</c>, <c>presets/profiles/</c>, plus a copy of the
///     bundled templates (camera models, defaults, presets) so the
///     <see cref="BurstProfileLoader"/> et al. can keep reading from a single
///     resolver root.
///   - <c>&lt;install-dir&gt;\config\</c> — the legacy path; the installer still
///     drops the bundled templates there as a one-time bootstrap source.
///     <see cref="BootstrapUserConfigIfNeeded"/> handles the migration.
///
/// The default constructor points <see cref="Root"/> at the per-user dir so
/// all subsequent reads/writes happen there — UnauthorizedAccessException on
/// <c>config.json.bak</c> in <c>Program Files</c> can no longer happen because
/// the .bak now lives under the user profile. Tests and explicit paths still
/// work via the <paramref name="configRoot"/> parameter (no bootstrap then).
/// </summary>
public class ConfigPathResolver
{
    private readonly string _configRoot;
    private readonly bool _isDefaultRoot;

    /// <summary>
    /// Initialisiert den Resolver.
    /// </summary>
    /// <param name="configRoot">
    /// Optional explicit config dir. When null, uses
    /// <c>%LOCALAPPDATA%\UMI\config</c> (per-user, writable) and triggers a
    /// one-time bootstrap from the install dir's <c>config\</c> subtree.
    /// </param>
    public ConfigPathResolver(string? configRoot = null)
    {
        if (configRoot != null)
        {
            _configRoot = Path.GetFullPath(configRoot);
            _isDefaultRoot = false;
            return;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _configRoot = Path.Combine(localAppData, "UMI", "config");
        _isDefaultRoot = true;

        // Best-effort: never throw out of the constructor. Subsequent calls to
        // ConfigFile / TemplateFile / BurstPresetsDir all expect a populated dir.
        try { BootstrapUserConfigIfNeeded(); }
        catch { /* swallow — caller will hit a meaningful error on first read */ }
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
    /// The install-dir's <c>config\</c> subtree. Read-only at runtime — used as
    /// the source for <see cref="BootstrapUserConfigIfNeeded"/> on first launch
    /// and as the migration source for users coming from a pre-2.1.2 install
    /// where <c>config.json</c> lived under <c>Program Files</c>.
    /// </summary>
    public static string InstallDirConfigRoot
    {
        get
        {
            var exePath = Environment.ProcessPath;
            var exeDir = !string.IsNullOrEmpty(exePath)
                ? Path.GetDirectoryName(exePath)!
                : AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Path.Combine(exeDir, "config");
        }
    }

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
    /// One-time bootstrap of the per-user config dir. Triggered automatically
    /// from the default constructor.
    ///
    /// Behaviour:
    ///   1. Make sure the per-user dir tree exists.
    ///   2. If the per-user dir is the same path as the install-dir config
    ///      (dev scenario) — do nothing.
    ///   3. For every file under <see cref="InstallDirConfigRoot"/> that does
    ///      not yet exist under <see cref="Root"/>, copy it across. Existing
    ///      user files are never overwritten.
    ///
    /// Net effect: a fresh install gets the bundled templates copied to the
    /// per-user dir on first launch. A user upgrading from a pre-2.1.2 install
    /// (where <c>config.json</c> lived under <c>Program Files</c>) gets their
    /// existing config + presets + profiles migrated transparently — the legacy
    /// dir is left in place untouched.
    /// </summary>
    public void BootstrapUserConfigIfNeeded()
    {
        EnsureStructure();

        var legacyRoot = InstallDirConfigRoot;
        if (!Directory.Exists(legacyRoot))
            return;

        if (Path.GetFullPath(legacyRoot).Equals(
                Path.GetFullPath(_configRoot), StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var src in Directory.EnumerateFiles(legacyRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(legacyRoot, src);
            var dst = Path.Combine(_configRoot, rel);
            if (File.Exists(dst))
                continue;

            var dstDir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                Directory.CreateDirectory(dstDir);

            try { File.Copy(src, dst, overwrite: false); }
            catch { /* skip files we cannot read/write — bootstrap is best-effort */ }
        }
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

    /// <summary>True when this resolver was created with the auto-detected per-user root.</summary>
    public bool IsDefaultRoot => _isDefaultRoot;
}
