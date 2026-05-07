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
/// Konfiguration für Burst-Detection (Foto-Sequenzen).
/// Profil-basiert: Verweist auf Preset-Dateien statt hardcodierte Regeln.
/// </summary>
public class BurstDetectionConfig
{
    /// <summary>
    /// Burst-Detection aktiviert/deaktiviert.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Liste der aktiven Profile (Referenzen auf Presets/burst/{name}.json).
    /// Profile werden in dieser Reihenfolge geprüft (zusätzlich sortiert nach Priority).
    /// </summary>
    [JsonPropertyName("active_profiles")]
    public List<string> ActiveProfiles { get; set; } = new();

    /// <summary>
    /// Fallback max_gap_seconds wenn kein Profil matcht.
    /// </summary>
    [JsonPropertyName("fallback_max_gap_seconds")]
    public double FallbackMaxGapSeconds { get; set; } = 3.0;

    /// <summary>
    /// Fallback min_count wenn kein Profil matcht.
    /// </summary>
    [JsonPropertyName("fallback_min_count")]
    public int FallbackMinCount { get; set; } = 3;

    /// <summary>
    /// Stunden (pro Monat) in denen Sonnenuntergang ist.
    /// Index = Monat (0=Jan, 11=Dez), Wert = Stunde (UTC).
    /// Optional für Tag/Nacht min_count Unterscheidung.
    /// </summary>
    [JsonPropertyName("sunset_hours")]
    public int[]? SunsetHours { get; set; }

    /// <summary>
    /// Stunden (pro Monat) in denen Sonnenaufgang ist.
    /// Optional für Tag/Nacht min_count Unterscheidung.
    /// </summary>
    [JsonPropertyName("sunrise_hours")]
    public int[]? SunriseHours { get; set; }

    /// <summary>
    /// Geladene Profile (zur Laufzeit befüllt durch BurstProfileLoader).
    /// NICHT aus JSON gelesen - wird programmatisch gesetzt.
    /// </summary>
    [JsonIgnore]
    public List<BurstProfile> LoadedProfiles { get; set; } = new();
}
