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

namespace UMI.Core.Services;

/// <summary>
/// Erkennt und benennt kryptische GoPro-Dateinamen in ein sortierbares Format um.
///
/// Unterstützte Patterns:
///   Legacy (Hero3–Hero7):
///     GOPR0001.MP4  → einzelnes Video (Kapitel 1), Dateinummer 0001
///     GP010001.MP4  → Kapitel 1, Dateinummer 0001
///     GP020001.MP4  → Kapitel 2, Dateinummer 0001 (Chaptered Recording)
///   Modern (Hero8+):
///     GH010001.MP4  → Hero, Kapitel 1, Dateinummer 0001
///     GX010001.MP4  → MAX/360, Kapitel 1, Dateinummer 0001
///
/// Ausgabeformat: GoPro_{fileNum:D4}_c{chapter:D2}.ext
///   GoPro_0001_c01.MP4
///   GoPro_0001_c02.MP4  (Folge-Kapitel desselben Clips)
///   GoPro_0001_c01.LRV  (Low-Res-Proxy)
///   GoPro_0001_c01.THM  (Thumbnail)
/// </summary>
public class GoProRenameService
{

    private static readonly Regex LegacySingleRegex =
        new(@"^GOPR(\d{4})(\..+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LegacyChapterRegex =
        new(@"^GP(\d{2})(\d{4})(\..+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ModernRegex =
        new(@"^G([HX])(\d{2})(\d{4})(\..+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Gibt true zurück wenn der Dateiname einem bekannten GoPro-Muster entspricht.</summary>
    public bool IsGoProFile(string fileName)
        => ParseFileName(fileName) is not null;

    /// <summary>
    /// Analysiert den Dateinamen und gibt strukturierte GoPro-Metadaten zurück.
    /// Gibt null zurück wenn kein GoPro-Muster erkannt wird.
    /// </summary>
    public GoProFileInfo? ParseFileName(string fileName)
    {

        var m = ModernRegex.Match(fileName);
        if (m.Success)
        {
            var generation = m.Groups[1].Value.Equals("H", StringComparison.OrdinalIgnoreCase)
                ? GoProGeneration.Modern
                : GoProGeneration.ModernX360;

            return new GoProFileInfo(
                Generation: generation,
                FileNumber: int.Parse(m.Groups[3].Value),
                Chapter: int.Parse(m.Groups[2].Value),
                Extension: m.Groups[4].Value);
        }

        m = LegacySingleRegex.Match(fileName);
        if (m.Success)
        {
            return new GoProFileInfo(
                Generation: GoProGeneration.Legacy,
                FileNumber: int.Parse(m.Groups[1].Value),
                Chapter: 1,
                Extension: m.Groups[2].Value);
        }

        m = LegacyChapterRegex.Match(fileName);
        if (m.Success)
        {
            return new GoProFileInfo(
                Generation: GoProGeneration.Legacy,
                FileNumber: int.Parse(m.Groups[2].Value),
                Chapter: int.Parse(m.Groups[1].Value),
                Extension: m.Groups[3].Value);
        }

        return null;
    }

    /// <summary>
    /// Gibt den umbenannten, sortierbaren Dateinamen zurück.
    /// Beispiel: GOPR0001.MP4 → GoPro_0001_c01.MP4
    /// Wenn der Dateiname kein GoPro-Muster hat, wird er unverändert zurückgegeben.
    /// </summary>
    public string GetRenamedFileName(string originalFileName)
    {
        var info = ParseFileName(originalFileName);
        if (info is null)
            return originalFileName;

        return $"GoPro_{info.FileNumber:D4}_c{info.Chapter:D2}{info.Extension}";
    }
}

/// <summary>Strukturierte GoPro-Dateiinformationen aus dem Dateinamen.</summary>
public record GoProFileInfo(
    GoProGeneration Generation,
    int FileNumber,
    int Chapter,
    string Extension);

/// <summary>GoPro Kamera-Generation, abgeleitet aus dem Dateinamen-Präfix.</summary>
public enum GoProGeneration
{
    /// <summary>Legacy-Kameras (Hero3–Hero7): GOPR/GP-Präfix</summary>
    Legacy,

    /// <summary>Moderne Kameras (Hero8+): GH-Präfix</summary>
    Modern,

    /// <summary>GoPro MAX / 360-Kameras: GX-Präfix</summary>
    ModernX360
}
