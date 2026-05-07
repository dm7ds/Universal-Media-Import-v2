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

public class GyroflowConfig
{
    [JsonPropertyName("parallel_enabled")]
    public bool ParallelEnabled { get; set; } = true;

    [JsonPropertyName("parallel_jobs")]
    public int ParallelJobs { get; set; } = 3;

    [JsonPropertyName("auto_detect_cores")]
    public bool AutoDetectCores { get; set; } = true;

    [JsonPropertyName("timeout_minutes")]
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>Veraltet — wird nicht mehr verwendet, bleibt für bestehende config.json-Dateien.</summary>
    [Obsolete("QueueStrategy wird nicht mehr verwendet. Verbleib für Rückwärtskompatibilität mit bestehenden config.json-Dateien.")]
    [JsonPropertyName("queue_strategy")]
    public string QueueStrategy { get; set; } = "largest_first";
}
