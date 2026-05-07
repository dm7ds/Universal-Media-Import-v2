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

namespace UMI.Core.Configuration;

/// <summary>
/// Post-Processing Konfiguration pro Kamera.
/// Steuert welche PostProcessors nach dem Import ausgeführt werden.
/// </summary>
public class PostProcessingConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("gyroflow")]
    public GyroflowProcessingConfig Gyroflow { get; set; } = new();

    [JsonPropertyName("racerender")]
    public RacerenderProcessingConfig Racerender { get; set; } = new();
}

/// <summary>
/// Konfiguration für den Gyroflow PostProcessor.
/// </summary>
public class GyroflowProcessingConfig
{
    /// <summary>Gyroflow PostProcessor aktivieren.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Stabilisierungsmodus: "automatic" (nur EIS-Off Videos) oder "all".
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "automatic";

    /// <summary>
    /// Auch Videos mit aktivem EIS stabilisieren (nur bei mode=automatic).
    /// </summary>
    [JsonPropertyName("force")]
    public bool Force { get; set; } = false;

    /// <summary>Gyroflow Preset-Datei (optional, überschreibt globales Preset).</summary>
    [JsonPropertyName("preset")]
    public string? Preset { get; set; }
}

/// <summary>
/// Konfiguration für den Racerender PostProcessor (zukünftig).
/// </summary>
public class RacerenderProcessingConfig
{
    /// <summary>Racerender PostProcessor aktivieren.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    /// <summary>Racerender Projektvorlage (optional).</summary>
    [JsonPropertyName("template")]
    public string? Template { get; set; }
}
