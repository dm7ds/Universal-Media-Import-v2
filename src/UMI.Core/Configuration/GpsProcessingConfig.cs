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

public class GpsProcessingConfig
{
    [JsonPropertyName("optimization_enabled")]
    public bool OptimizationEnabled { get; set; } = true;

    [JsonPropertyName("time_buffer_seconds")]
    public int TimeBufferSeconds { get; set; } = 30;

    [JsonPropertyName("keep_optimized_gpx")]
    public bool KeepOptimizedGpx { get; set; } = true;

    [JsonPropertyName("min_points_threshold")]
    public int MinPointsThreshold { get; set; } = 10;

    [JsonPropertyName("create_srt_sidecar")]
    public bool CreateSrtSidecar { get; set; } = false;

    [JsonPropertyName("create_racerender_gpx")]
    public bool CreateRaceRenderGpx { get; set; } = false;

    [JsonPropertyName("validation")]
    public GpsValidationConfig Validation { get; set; } = new();
}

public class GpsValidationConfig
{
    [JsonPropertyName("check_coordinates")]
    public bool CheckCoordinates { get; set; } = true;

    [JsonPropertyName("lat_range")]
    public double[] LatRange { get; set; } = new[] { -90.0, 90.0 };

    [JsonPropertyName("lon_range")]
    public double[] LonRange { get; set; } = new[] { -180.0, 180.0 };
}
