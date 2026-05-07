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

using System.Reflection;

namespace UMI.CLI.Helpers;

/// <summary>
/// Build-Informationen (Git-Hash, Build-Datum) aus Assembly-Metadaten.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    /// Git-Hash (kurz) zur Build-Zeit.
    /// </summary>
    public static string GitHash
    {
        get
        {
            var assembly = typeof(BuildInfo).Assembly;
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (string.IsNullOrEmpty(infoVersion))
                return "unknown";

            var plusIndex = infoVersion.IndexOf('+');
            if (plusIndex > 0 && plusIndex < infoVersion.Length - 1)
                return infoVersion.Substring(plusIndex + 1);

            return "unknown";
        }
    }

    /// <summary>
    /// Build-Datum (aus Executable-Erstellungszeit).
    /// </summary>
    public static string BuildDate
    {
        get
        {

            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            if (File.Exists(exePath))
            {
                var buildDate = File.GetLastWriteTime(exePath);
                return buildDate.ToString("yyyy-MM-dd");
            }
            return DateTime.Now.ToString("yyyy-MM-dd");
        }
    }

    /// <summary>
    /// Versionsnummer aus AssemblyInformationalVersionAttribute.
    /// Format: "2.1.0+abc1234" → "2.1.0". Fallback auf AssemblyVersion.
    /// </summary>
    public static string Version
    {
        get
        {
            var assembly = typeof(BuildInfo).Assembly;
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrEmpty(infoVersion))
            {

                var plusIndex = infoVersion.IndexOf('+');
                if (plusIndex > 0)
                    return infoVersion.Substring(0, plusIndex);

                return infoVersion;
            }

            var assemblyVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyVersionAttribute>()?.Version;
            return assemblyVersion ?? "0.0.0";
        }
    }

    /// <summary>
    /// Formatierte Version+Build-Zeile: "v2.1.0 · Build abc1234 (2026-02-15)"
    /// </summary>
    public static string FormattedVersionLine => $"v{Version} · {FormattedBuildInfo}";

    /// <summary>
    /// Formatierte Build-Info für Banner: "Build abc1234 (2026-02-15)"
    /// </summary>
    public static string FormattedBuildInfo => $"Build {GitHash} ({BuildDate})";
}
