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
using System.Text.RegularExpressions;

namespace UMI.Core.Utilities;

/// <summary>
/// Gemeinsame Hilfsmethoden für das Parsen von EXIF-Wertstrings.
/// Einzige Quelle der Wahrheit für numerische EXIF-Wert-Extraktion (DRY).
/// </summary>
public static class ExifValueParser
{
    private static readonly Regex LeadingNumber = new(@"^([\d.]+)", RegexOptions.Compiled);

    /// <summary>
    /// Versucht einen numerischen Wert aus einem EXIF-String zu extrahieren.
    /// Unterstützt Dezimal-, Rational- (1/500) und Einheiten-Format (100 mm).
    /// </summary>
    /// <param name="value">EXIF-Wertstring (z.B. "1/500", "100 mm", "3.5").</param>
    /// <returns>Numerischer Wert oder null wenn das Parsen fehlschlägt.</returns>
    public static double? TryParseNumericValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var directValue))
            return directValue;

        if (value.Contains('/'))
        {
            var parts = value.Split('/');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var numerator) &&
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var denominator) &&
                denominator != 0)
            {
                return numerator / denominator;
            }
        }

        var match = LeadingNumber.Match(value);
        if (match.Success &&
            double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var extractedValue))
        {
            return extractedValue;
        }

        return null;
    }
}
