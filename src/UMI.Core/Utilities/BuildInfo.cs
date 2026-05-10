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

namespace UMI.Core.Utilities;

/// <summary>
/// Reads the build identity (version, git commit, build date) that the MSBuild
/// target in Directory.Build.props bakes into every assembly. Used by the GUI
/// and CLI startup banners so a log file always identifies which exact build
/// produced it — no more guessing whether a user was running v2.1.1 or
/// v2.1.1-with-yesterday's-fix.
/// </summary>
public static class BuildInfo
{
    private static readonly Assembly Self = typeof(BuildInfo).Assembly;

    /// <summary>
    /// Three-part assembly version from <c>Directory.Build.props</c> as a string,
    /// e.g. <c>"2.1.1"</c>. Falls back to "0.0.0" if the metadata is missing.
    /// </summary>
    public static string Version
    {
        get
        {
            var v = Self.GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>
    /// Full <see cref="AssemblyInformationalVersionAttribute"/> string. Includes
    /// the short git commit suffix (e.g. <c>"2.1.1+a6e5354"</c>) when the build
    /// was produced inside a git working tree. Stripped of any "+sha" prefix when
    /// running an SDK-generated SourceRevisionId.
    /// </summary>
    public static string InformationalVersion =>
        Self.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Version;

    /// <summary>Short git commit hash captured at build time, or "unknown".</summary>
    public static string GitCommit => GetMetadata("GitCommit") ?? "unknown";

    /// <summary>
    /// UTC build timestamp captured at MSBuild evaluation time
    /// (<c>yyyy-MM-ddTHH:mm:ssZ</c>), or "unknown".
    /// </summary>
    public static string BuildDate => GetMetadata("BuildDate") ?? "unknown";

    /// <summary>
    /// One-line summary suitable for a log header, e.g.
    /// <c>"UMI v2.1.1 (commit a6e5354, built 2026-05-10T14:21:09Z)"</c>.
    /// </summary>
    public static string Banner =>
        $"UMI v{Version} (commit {GitCommit}, built {BuildDate})";

    private static string? GetMetadata(string key)
    {
        foreach (var attr in Self.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attr.Key == key)
                return attr.Value;
        }
        return null;
    }
}
