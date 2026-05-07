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

using System.Text.Json.Serialization;

namespace UMI.Core.Models;

/// <summary>
/// Ein Burst-Detection-Profil definiert Regeln zur Erkennung von Foto-Sequenzen.
/// Profile sind benannt, datengetrieben und erweiterbar ohne Code-Änderungen.
/// </summary>
public class BurstProfile
{
    /// <summary>
    /// Eindeutiger Name des Profils (z.B. "Sport", "Astro", "Timelapse").
    /// Wird als Ordnername für erkannte Sequenzen verwendet.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Beschreibung des Profils (für GUI/Dokumentation).
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Priorität (niedriger = wird zuerst geprüft).
    /// Profile werden in Prioritäts-Reihenfolge geprüft, erstes Match gewinnt.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Color palette index (0–7) for visual identification in the Sequence Reviewer.
    /// -1 means auto-assignment (default for profiles without explicit color choice).
    /// </summary>
    [JsonPropertyName("color_index")]
    public int ColorIndex { get; set; } = -1;

    /// <summary>
    /// Bedingungen die erfüllt sein müssen damit dieses Profil matcht.
    /// Unterstützt AND/OR-Logik und Verschachtelung über ConditionGroup.
    /// </summary>
    [JsonPropertyName("match_conditions")]
    public ConditionGroup MatchConditions { get; set; } = new();

    /// <summary>
    /// Gruppierungs-Konfiguration: Wie werden Fotos zu Sequenzen zusammengefasst?
    /// </summary>
    [JsonPropertyName("grouping")]
    public GroupingConfig Grouping { get; set; } = new();
}

/// <summary>
/// Eine Bedingung die ein EXIF-Feld prüft.
/// </summary>
public class MatchCondition
{
    /// <summary>
    /// EXIF-Feldname (z.B. "ExposureTime", "ContinuousDrive", "ISO").
    /// Muss in PhotoMetadata existieren.
    /// </summary>
    [JsonPropertyName("field")]
    public required string Field { get; set; }

    /// <summary>
    /// Vergleichs-Operator: ">", ">=", "<", "<=", "==", "!="
    /// </summary>
    [JsonPropertyName("operator")]
    public required string Operator { get; set; }

    /// <summary>
    /// Wert zum Vergleichen (numerisch).
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }
}

/// <summary>
/// Eine Gruppe von Bedingungen, verknüpft mit AND oder OR.
/// Unterstützt Verschachtelung: Gruppen können Sub-Gruppen enthalten.
/// </summary>
public class ConditionGroup
{
    /// <summary>
    /// Verknüpfungs-Operator: "AND" oder "OR".
    /// AND: Alle Elemente (Conditions + Groups) müssen true sein.
    /// OR: Mindestens ein Element muss true sein.
    /// </summary>
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "AND";

    /// <summary>
    /// Einfache Feld-Bedingungen in dieser Gruppe.
    /// </summary>
    [JsonPropertyName("conditions")]
    public List<MatchCondition>? Conditions { get; set; }

    /// <summary>
    /// Sub-Gruppen für verschachtelte AND/OR-Logik.
    /// </summary>
    [JsonPropertyName("groups")]
    public List<ConditionGroup>? Groups { get; set; }
}

/// <summary>
/// Gruppierungs-Konfiguration für ein Burst-Profil.
/// </summary>
public class GroupingConfig
{
    /// <summary>
    /// Maximale Zeit-Lücke zwischen Fotos (in Sekunden).
    /// Größere Lücken starten eine neue Gruppe.
    /// </summary>
    [JsonPropertyName("max_gap_seconds")]
    public double MaxGapSeconds { get; set; } = 3.0;

    /// <summary>
    /// Minimale Anzahl Fotos damit eine Gruppe als Sequenz gilt.
    /// Kleinere Gruppen werden als "Single Shots" klassifiziert.
    /// </summary>
    [JsonPropertyName("min_count")]
    public int MinCount { get; set; } = 3;

    /// <summary>
    /// Adaptive Threshold: Threshold basierend auf durchschnittlichem Gap berechnen?
    /// Falls true: threshold = avgGap * adaptive_multiplier (capped bei max_gap_seconds).
    /// </summary>
    [JsonPropertyName("adaptive_threshold")]
    public bool AdaptiveThreshold { get; set; } = false;

    /// <summary>
    /// Multiplikator für adaptive Threshold-Berechnung.
    /// Nur relevant wenn adaptive_threshold = true.
    /// </summary>
    [JsonPropertyName("adaptive_multiplier")]
    public double AdaptiveMultiplier { get; set; } = 2.0;

    /// <summary>
    /// EXIF fields that must be constant within a sequence.
    /// After time-based grouping, sequences are split further where stable field values change.
    /// Example: ["FocalLength"] means all photos in a sequence must have the same focal length.
    /// </summary>
    [JsonPropertyName("stable_fields")]
    public List<string>? StableFields { get; set; }
}
