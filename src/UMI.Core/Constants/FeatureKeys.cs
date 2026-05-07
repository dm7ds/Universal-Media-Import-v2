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

namespace UMI.Core.Constants;

/// <summary>Feature key identifiers used across config, presets, and GUI.</summary>
public static class FeatureKeys
{
    /// <summary>GPS injection feature key.</summary>
    public const string GpsInjection   = "gps_injection";

    /// <summary>Gyroflow stabilization feature key.</summary>
    public const string Gyroflow       = "gyroflow";

    /// <summary>Burst detection feature key.</summary>
    public const string BurstDetection = "burst_detection";

    /// <summary>Metadata backup feature key.</summary>
    public const string MetadataBackup = "metadata_backup";

    /// <summary>EIS (Electronic Image Stabilization) detection feature key.</summary>
    public const string EisDetection   = "eis_detection";

    /// <summary>Lens correction feature key.</summary>
    public const string LensCorrection = "lens_correction";

    /// <summary>Post-processing feature key.</summary>
    public const string PostProcess    = "post_process";

    /// <summary>Rename videos feature key.</summary>
    public const string RenameVideos   = "rename_videos";

    /// <summary>GoPro rename feature key.</summary>
    public const string GoProRename    = "gopro_rename";

    /// <summary>Generate thumbnails for RAW files after import feature key.</summary>
    public const string GenerateThumbnails = "generate_thumbnails";
}
