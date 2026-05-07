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

namespace UMI.Core.Models;

/// <summary>
/// Fortschritt beim EXIF-Scan.
/// </summary>
public record ExifScanProgress(int ScannedFiles, int TotalFiles, string CurrentFile);

/// <summary>
/// Ergebnis der EXIF-Analyse: Felder die in 100% der Bilder vorhanden sind.
/// </summary>
public record ExifAnalysisResult
{
    /// <summary>Anzahl gescannter Bilder</summary>
    public int TotalPhotos { get; init; }

    /// <summary>Felder die in 100% der Bilder vorhanden + befüllt sind, kategorisiert</summary>
    public required List<ExifFieldGroup> FieldGroups { get; init; }

    /// <summary>Flache Liste aller Felder (für Suche/Filter)</summary>
    public IEnumerable<ExifFieldInfo> AllFields => FieldGroups.SelectMany(g => g.Fields);
}

/// <summary>
/// Gruppe von EXIF-Feldern nach Kategorie.
/// </summary>
public record ExifFieldGroup
{
    /// <summary>Kategorie: Shooting, Exposure, Focus, Camera, Image, Time, File</summary>
    public required string Category { get; init; }

    /// <summary>Felder in dieser Kategorie</summary>
    public required List<ExifFieldInfo> Fields { get; init; }
}

/// <summary>
/// Information über ein EXIF-Feld das in allen Bildern vorhanden ist.
/// </summary>
public record ExifFieldInfo
{
    /// <summary>EXIF Tag-Name wie er in MetadataExtractor kommt (z.B. "ContinuousDrive")</summary>
    public required string FieldName { get; init; }

    /// <summary>EXIF Directory (z.B. "Canon Camera Settings", "Exif IFD0")</summary>
    public required string Directory { get; init; }

    /// <summary>Kategorie (redundant zu Group, für Flat-Zugriff)</summary>
    public required string Category { get; init; }

    /// <summary>Beispiel-Wert vom ersten Bild (String-Darstellung)</summary>
    public required string SampleValue { get; init; }

    /// <summary>Numerischer Wert wenn möglich (für Operator-Matching), sonst null</summary>
    public double? NumericValue { get; init; }

    /// <summary>Ist in ALLEN Bildern vorhanden + befüllt</summary>
    public bool IsPresentInAll { get; init; } = true;
}
