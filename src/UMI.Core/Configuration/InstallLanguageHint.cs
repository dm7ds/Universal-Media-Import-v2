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

using System.IO;

namespace UMI.Core.Configuration;

/// <summary>
/// Installer-to-app handoff for the user's language pick.
///
/// The Inno Setup script writes <c>install-language.txt</c> into <c>{app}\config\</c>
/// after the install completes — content is "de" or "en" matching the language the user
/// chose in the setup wizard. UMI reads it on first run (before <c>config.json</c> exists)
/// to bootstrap the GUI language and store it as the persisted default. Once the wizard
/// commits <c>config.json</c>, the hint file is deleted.
/// </summary>
public static class InstallLanguageHint
{
    private const string FileName = "install-language.txt";

    /// <summary>
    /// Returns "de" or "en" if a valid hint file exists in <paramref name="configRoot"/>,
    /// otherwise null.
    /// </summary>
    public static string? Read(string configRoot)
    {
        try
        {
            var path = Path.Combine(configRoot, FileName);
            if (!File.Exists(path)) return null;
            var code = File.ReadAllText(path).Trim().ToLowerInvariant();
            return code is "de" or "en" ? code : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Removes the hint file once the wizard has persisted the language to config.json.
    /// Safe to call when the file does not exist.
    /// </summary>
    public static void Delete(string configRoot)
    {
        try
        {
            var path = Path.Combine(configRoot, FileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Swallow — failing to clean up the hint is harmless: next start will read
            // it again and re-apply the same language already in config.json.
        }
    }
}
