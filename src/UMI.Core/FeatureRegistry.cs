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

using UMI.Core.Constants;
using UMI.Core.Resources;

namespace UMI.Core;

/// <summary>
/// Central registry for all camera feature keys and their localized display labels.
/// SSOT for feature enumeration in Core/CLI context.
/// GUI extends this via FeatureLabels (adds BadgeKey, BubbleLabel).
/// Labels are resolved at access time from CoreStrings resources (culture-aware).
/// </summary>
public static class FeatureRegistry
{
    public record FeatureInfo(string Key, string ShortLabel)
    {
        /// <summary>Localized display label, resolved at access time.</summary>
        public string Label => Key switch
        {
            FeatureKeys.GpsInjection   => CoreStrings.Feature_GpsInjection,
            FeatureKeys.Gyroflow       => CoreStrings.Feature_Gyroflow,
            FeatureKeys.BurstDetection => CoreStrings.Feature_BurstDetection,
            FeatureKeys.MetadataBackup => CoreStrings.Feature_MetadataBackup,
            FeatureKeys.EisDetection   => CoreStrings.Feature_EisDetection,
            FeatureKeys.PostProcess    => CoreStrings.Feature_PostProcess,
            FeatureKeys.RenameVideos   => CoreStrings.Feature_RenameVideos,
            FeatureKeys.GoProRename        => CoreStrings.Feature_GoProRename,
            FeatureKeys.GenerateThumbnails => CoreStrings.Feature_GenerateThumbnails,
            _ => Key
        };

        /// <summary>Localized tooltip description, resolved at access time.</summary>
        public string Description => Key switch
        {
            FeatureKeys.GpsInjection   => CoreStrings.Feature_GpsInjection_Desc,
            FeatureKeys.Gyroflow       => CoreStrings.Feature_Gyroflow_Desc,
            FeatureKeys.BurstDetection => CoreStrings.Feature_BurstDetection_Desc,
            FeatureKeys.MetadataBackup => CoreStrings.Feature_MetadataBackup_Desc,
            FeatureKeys.EisDetection   => CoreStrings.Feature_EisDetection_Desc,
            FeatureKeys.PostProcess    => CoreStrings.Feature_PostProcess_Desc,
            FeatureKeys.RenameVideos   => CoreStrings.Feature_RenameVideos_Desc,
            FeatureKeys.GoProRename        => CoreStrings.Feature_GoProRename_Desc,
            FeatureKeys.GenerateThumbnails => CoreStrings.Feature_GenerateThumbnails_Desc,
            _ => string.Empty
        };
    }

    public static readonly IReadOnlyList<FeatureInfo> All = new[]
    {
        new FeatureInfo(FeatureKeys.GpsInjection,   "GPS"),
        new FeatureInfo(FeatureKeys.Gyroflow,        "Gyroflow"),
        new FeatureInfo(FeatureKeys.BurstDetection,  "Burst"),
        new FeatureInfo(FeatureKeys.MetadataBackup,  "Metadata"),
        new FeatureInfo(FeatureKeys.EisDetection,    "EIS"),
        new FeatureInfo(FeatureKeys.PostProcess,     "PostProc"),
        new FeatureInfo(FeatureKeys.RenameVideos,    "Rename"),
        new FeatureInfo(FeatureKeys.GoProRename,          "GoPro"),
        new FeatureInfo(FeatureKeys.GenerateThumbnails,   "Thumbs"),
    };

    public static readonly IReadOnlyList<string> AllKeys = All.Select(f => f.Key).ToList();

    public static FeatureInfo? Get(string key) => All.FirstOrDefault(f => f.Key == key);
}
