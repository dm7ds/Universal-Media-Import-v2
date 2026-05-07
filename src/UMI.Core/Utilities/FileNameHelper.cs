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

using System.Text.RegularExpressions;

namespace UMI.Core.Utilities;

/// <summary>
/// Utility-Klasse für Dateinamen-Operationen.
/// </summary>
public static class FileNameHelper
{

    private static readonly Regex TimestampPattern = new(
        @"\d{8}[_\-]?\d{6}" +
        @"|" +
        @"\d{4}[-_]\d{2}[-_]\d{2}",
        RegexOptions.Compiled);

    /// <summary>
    /// Prüft ob der Dateiname bereits einen erkennbaren Timestamp enthält.
    /// Wenn ja → NICHT umbenennen.
    /// </summary>
    /// <example>
    /// HasTimestampInName("DJI_20260217_001.mp4") → true
    /// HasTimestampInName("GX010042.MP4") → false
    /// HasTimestampInName("VID_20260217_143022.mp4") → true
    /// HasTimestampInName("MVI_4832.MP4") → false
    /// </example>
    public static bool HasTimestampInName(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        return TimestampPattern.IsMatch(name);
    }

    /// <summary>
    /// Erzeugt den Timestamp-Prefix für Video-Renames: "yyyyMMdd_HHmmss".
    /// </summary>
    public static string BuildTimestampPrefix(DateTime captureDate)
        => captureDate.ToString("yyyyMMdd_HHmmss");
}
