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

namespace UMI.Core.Services;

public class GpuTaskRequest
{
    public required string TaskType { get; init; }
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public string? PayloadJson { get; init; }
    public int Priority { get; init; }
    public int MaxRetries { get; init; } = 3;
    public long FileSize { get; init; }
}

public class GpuQueueStats
{
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Total => Pending + InProgress + Completed + Failed;
}

public class GpuBatchProgress
{
    public string BatchId { get; set; } = "";
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public bool IsFinished => Pending == 0 && InProgress == 0;
}

public class GpuTaskStartedEventArgs : EventArgs
{
    public long TaskId { get; init; }
    public string FileName { get; init; } = "";
    public string? BatchId { get; init; }
    public string TaskType { get; init; } = "";
}

public class GpuTaskProgressEventArgs : EventArgs
{
    public long TaskId { get; init; }
    public string FileName { get; init; } = "";
    public string InputPath { get; init; } = "";
    public string? BatchId { get; init; }
    public double Percent { get; init; }
    public string Eta { get; init; } = "";
    public int CurrentFrame { get; init; }
    public int TotalFrames { get; init; }
}

public class GpuTaskCompletedEventArgs : EventArgs
{
    public long TaskId { get; init; }
    public string FileName { get; init; } = "";
    public string InputPath { get; init; } = "";
    public string? BatchId { get; init; }
    public string OutputPath { get; init; } = "";
}

public class GpuTaskFailedEventArgs : EventArgs
{
    public long TaskId { get; init; }
    public string FileName { get; init; } = "";
    public string? BatchId { get; init; }
    public string Error { get; init; } = "";
}

public class GpuBatchCompletedEventArgs : EventArgs
{
    public string BatchId { get; init; } = "";
    public int Total { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
}

public interface IGpuTaskQueue
{

    Task<long> EnqueueAsync(GpuTaskRequest request, CancellationToken ct = default);
    Task<string> EnqueueBatchAsync(IEnumerable<GpuTaskRequest> requests, CancellationToken ct = default);

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task CancelTaskAsync(long taskId);
    Task CancelAllAsync(CancellationToken ct = default);
    Task RetryTaskAsync(long taskId);
    Task RetryAllFailedAsync();

    Task<GpuQueueStats> GetStatsAsync();
    Task<GpuBatchProgress> GetBatchProgressAsync(string batchId);
    bool IsRunning { get; }

    event EventHandler<GpuTaskStartedEventArgs>? TaskStarted;
    event EventHandler<GpuTaskProgressEventArgs>? TaskProgress;
    event EventHandler<GpuTaskCompletedEventArgs>? TaskCompleted;
    event EventHandler<GpuTaskFailedEventArgs>? TaskFailed;
    event EventHandler<GpuBatchCompletedEventArgs>? BatchCompleted;
    event EventHandler? QueueEmpty;
}
