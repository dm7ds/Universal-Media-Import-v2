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

namespace UMI.Data.Models;

public enum GpuTaskStatus { Pending = 0, InProgress = 1, Completed = 2, Failed = 3, Cancelled = 4 }

public class GpuTask
{
    public long Id { get; set; }
    public string TaskType { get; set; } = "";
    public int Status { get; set; }
    public int Priority { get; set; }
    public string InputPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string? PayloadJson { get; set; }
    public long FileSize { get; set; }
    public double ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? BatchId { get; set; }
    public string CreatedAt { get; set; } = "";
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }

    public GpuTaskStatus TaskStatus => (GpuTaskStatus)Status;
}

public class GyroflowTaskPayload
{
    public string? PresetPath { get; set; }
    public string GpuDevice { get; set; } = "nvidia";
    public string? CameraId { get; set; }
    /// <summary>
    /// Wenn true, überspringt GpuTaskQueue den PostStabilize-Workflow.
    /// Wird gesetzt wenn der Batch vom GyroflowPostProcessor gesteuert wird
    /// (der Orchestrator übernimmt dann Metadata-Restore + GPS-Inject).
    /// </summary>
    public bool SkipPostStabilize { get; set; }
}

public class GpuQueueStats
{
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Cancelled { get; set; }
    public int Total => Pending + InProgress + Completed + Failed + Cancelled;
}
