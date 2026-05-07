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
/// Per-feature availability and display configuration loaded from a .umi preset file.
/// SSOT: Presets are the one defining source for available/enabled/on_card values.
/// </summary>
/// <param name="Available">Whether this feature is visible and configurable for this camera type.</param>
/// <param name="EnabledByDefault">Initial toggle state when the camera is first created.</param>
/// <param name="SimpleOnCard">
/// When true AND the feature is currently enabled, the bubble appears in the
/// collapsed card's simple row. When false or when disabled, it appears in the
/// expanded advanced section instead.
/// </param>
public record FeatureDefinition(
    [property: JsonPropertyName("available")]        bool Available,
    [property: JsonPropertyName("enabled_by_default")] bool EnabledByDefault,
    [property: JsonPropertyName("simple_on_card")]   bool SimpleOnCard);

/// <summary>
/// Default file extensions for a camera type, loaded from the preset's <c>default_file_types</c> section.
/// SSOT: Extensions are defined ONCE in the .umi preset — never duplicated in code.
/// </summary>
/// <param name="Video">Supported video extensions (e.g. [".mp4", ".mov"]).</param>
/// <param name="Photo">Supported photo extensions (e.g. [".jpg", ".jpeg", ".dng"]).</param>
public record DefaultFileTypes(
    [property: JsonPropertyName("video")] string[] Video,
    [property: JsonPropertyName("photo")] string[] Photo);

/// <summary>
/// Definition eines Kamera-Typs mit Feature-Konfiguration.
/// Geladen aus config/presets/types/*.umi für Feature-Vererbung und GUI-Bubble-Placement.
/// </summary>
public class CameraTypeDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Hex-Farbcode für GUI-Darstellung (z.B. "#f97316").
    /// CLI ignoriert diesen Wert.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>
    /// Per-feature availability and placement for this camera type.
    /// Key = canonical feature key (e.g. "gps_injection", "gyroflow").
    /// SSOT: BuildBubbleLists() iterates this dictionary — no hardcoded feature lists.
    /// </summary>
    [JsonPropertyName("features")]
    public Dictionary<string, FeatureDefinition>? Features { get; set; }

    /// <summary>
    /// Feature keys shown in Settings→Cam when app is in Simple Mode.
    /// Features not in this list are hidden in Simple Mode (but still active if enabled).
    /// Empty list = no features shown in Simple Mode.
    /// </summary>
    [JsonPropertyName("simple_features")]
    public List<string> SimpleFeatures { get; set; } = new();

    /// <summary>
    /// Default file extensions for this camera type (video + photo).
    /// SSOT: Loaded from the preset's <c>default_file_types</c> section.
    /// Used by AddCameraDialog to pre-populate FileTypes when creating a new camera.
    /// </summary>
    [JsonPropertyName("default_file_types")]
    public DefaultFileTypes? DefaultFileTypes { get; set; }

}
