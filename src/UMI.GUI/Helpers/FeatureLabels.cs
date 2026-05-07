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

using UMI.Core;
using UMI.Core.Constants;

namespace UMI.GUI.Helpers;

/// <summary>
/// GUI extension of <see cref="FeatureRegistry"/> — adds BadgeKey for theme color lookup.
/// Keys, Labels and BubbleLabels are sourced directly from FeatureRegistry (SSOT).
///
/// BadgeKey maps to StaticResource brush pairs in Default.xaml:
///   "Gps"    → BrushBadgeGps    / BrushBadgeGpsBg
///   "Gyro"   → BrushBadgeGyro   / BrushBadgeGyroBg
///   "Burst"  → BrushBadgeBurst  / BrushBadgeBurstBg
///   "Meta"   → BrushBadgeMeta   / BrushBadgeMetaBg
///   "Eis"    → BrushBadgeEis    / BrushBadgeEisBg
///   "Lens"   → BrushBadgeLens   / BrushBadgeLensBg
///   "Post"   → BrushBadgePost   / BrushBadgePostBg
///   "Rename" → BrushBadgeRename / BrushBadgeRenameBg
///   "GoPro"  → BrushBadgeGoPro  / BrushBadgeGoProBg
///   "Thumbs" → BrushBadgeThumbs / BrushBadgeThumbsBg
/// </summary>
public static class FeatureLabels
{
    /// <summary>
    /// BadgeKey mapping indexed by canonical feature key.
    /// Only GUI-specific data — keys/labels come from FeatureRegistry.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> BadgeKeys =
        new Dictionary<string, string>
        {
            [FeatureKeys.GpsInjection]   = "Gps",
            [FeatureKeys.Gyroflow]       = "Gyro",
            [FeatureKeys.BurstDetection] = "Burst",
            [FeatureKeys.MetadataBackup] = "Meta",
            [FeatureKeys.EisDetection]   = "Eis",
            [FeatureKeys.LensCorrection] = "Lens",
            [FeatureKeys.PostProcess]    = "Post",
            [FeatureKeys.RenameVideos]   = "Rename",
            [FeatureKeys.GoProRename]        = "GoPro",
            [FeatureKeys.GenerateThumbnails] = "Thumbs",
        };

    /// <summary>
    /// All known feature entries with display label, bubble label and badge style key.
    /// Ordered in canonical display order from FeatureRegistry.
    /// </summary>
    public static readonly IReadOnlyList<FeatureEntry> All =
        FeatureRegistry.All
            .Select(fi => new FeatureEntry(
                fi.Key,
                fi.Label,
                fi.ShortLabel,
                BadgeKeys.TryGetValue(fi.Key, out var badge) ? badge : fi.Key))
            .ToList();

    /// <summary>
    /// Lookup by feature key (case-insensitive). Returns null when key is unknown.
    /// </summary>
    public static FeatureEntry? Get(string featureKey)
    {
        foreach (var entry in All)
        {
            if (string.Equals(entry.Key, featureKey, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }
}

/// <summary>
/// Represents a feature's identity for UI display purposes.
/// </summary>
/// <param name="Key">Canonical feature key (e.g. "gps_injection").</param>
/// <param name="Label">Full display label (e.g. "GPS Injection").</param>
/// <param name="BubbleLabel">Short bubble label (e.g. "GPS").</param>
/// <param name="BadgeKey">Theme badge key for color lookup (e.g. "Gps").</param>
public sealed record FeatureEntry(string Key, string Label, string BubbleLabel, string BadgeKey);
