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

using System.Diagnostics;

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Gemeinsame Hilfsklasse fuer Tool-Erkennung via PATH (where.exe).
/// </summary>
internal static class ToolDetectionHelper
{
    /// <summary>
    /// Ruft "where.exe {toolName}" auf und gibt den ersten gefundenen Pfad zurueck.
    /// Gibt null zurueck wenn das Tool nicht im PATH liegt oder ein Fehler auftritt.
    /// </summary>
    public static string? FindViaWhere(string toolName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(toolName);

            using var process = Process.Start(startInfo);
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadLine();
            process.WaitForExit(3000);

            if (!string.IsNullOrWhiteSpace(output) && File.Exists(output.Trim()))
                return Path.GetFullPath(output.Trim());

            return null;
        }
        catch
        {
            return null;
        }
    }
}
