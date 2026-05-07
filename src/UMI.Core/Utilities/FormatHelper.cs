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

namespace UMI.Core.Utilities;

/// <summary>
/// Utility-Klasse für Formatierungs-Helpers.
/// </summary>
public static class FormatHelper
{
    /// <summary>
    /// Formatiert Byte-Größen für lesbare Ausgabe (GB/MB/KB).
    /// </summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        > 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        > 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        _ => $"{bytes / 1024.0:F1} KB"
    };

    /// <summary>
    /// Formatiert Byte-Größen SI-basiert (1000er-Schritte) für lesbare Ausgabe (GB/MB/KB).
    /// Geeignet für Anzeige von Laufwerks- und Speicherkapazitäten nach SI-Norm.
    /// Returns an empty string for zero or negative values.
    /// </summary>
    public static string FormatBytesSI(long bytes)
    {
        if (bytes <= 0) return "";
        if (bytes >= 1_000_000_000L) return $"{bytes / 1_000_000_000.0:F0} GB";
        if (bytes >= 1_000_000L)     return $"{bytes / 1_000_000.0:F0} MB";
        return $"{bytes / 1024.0:F0} KB";
    }

    /// <summary>
    /// Formatiert Transfer-Geschwindigkeit für lesbare Ausgabe (GB/s, MB/s, KB/s).
    /// </summary>
    public static string FormatSpeed(double bytesPerSecond) => bytesPerSecond switch
    {
        > 1_073_741_824 => $"{(bytesPerSecond / 1_073_741_824.0).ToString("F1", CultureInfo.InvariantCulture)} GB/s",
        > 1_048_576     => $"{(bytesPerSecond / 1_048_576.0).ToString("F1", CultureInfo.InvariantCulture)} MB/s",
        _               => $"{(bytesPerSecond / 1024.0).ToString("F1", CultureInfo.InvariantCulture)} KB/s",
    };
}
