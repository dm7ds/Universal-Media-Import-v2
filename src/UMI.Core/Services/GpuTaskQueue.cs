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

using System.Threading.Channels;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Data;
using UMI.Data.Models;

namespace UMI.Core.Services;

public class GpuTaskQueue : IGpuTaskQueue, IDisposable
{
    private readonly GpuTaskDatabase _db;
    private readonly IGyroflowService _gyroflowService;
    private readonly IPostProcessingService _postProcessingService;
    private readonly IProcessHistoryService _processHistory;
    private readonly GpuQueueConfig _config;
    private readonly ILogger<GpuTaskQueue>? _logger;

    private Channel<long> _channel;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _currentTaskCts;
    private long _currentTaskId;
    private readonly List<Task> _workers = new();

    private DateTime _lastProgressUpdate = DateTime.MinValue;
    private static readonly TimeSpan ProgressThrottle = TimeSpan.FromSeconds(1);

    public bool IsRunning { get; private set; }

    public event EventHandler<GpuTaskStartedEventArgs>? TaskStarted;
    public event EventHandler<GpuTaskProgressEventArgs>? TaskProgress;
    public event EventHandler<GpuTaskCompletedEventArgs>? TaskCompleted;
    public event EventHandler<GpuTaskFailedEventArgs>? TaskFailed;
    public event EventHandler<GpuBatchCompletedEventArgs>? BatchCompleted;
    public event EventHandler? QueueEmpty;

    public GpuTaskQueue(
        GpuTaskDatabase db,
        IGyroflowService gyroflowService,
        IPostProcessingService postProcessingService,
        IProcessHistoryService processHistory,
        GpuQueueConfig config,
        ILogger<GpuTaskQueue>? logger = null)
    {
        _db = db;
        _gyroflowService = gyroflowService;
        _postProcessingService = postProcessingService;
        _processHistory = processHistory;
        _config = config;
        _logger = logger;
        _channel = CreateChannel();
    }

    private static Channel<long> CreateChannel()
        => Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = false });

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning)
            return;

        _channel = CreateChannel();

        await _db.InitializeAsync();
        await _db.ResetStaleInProgressAsync();
        await _db.PurgeCompletedAsync(DateTime.UtcNow.AddHours(-_config.PurgeCompletedAfterHours));

        var pendingIds = await _db.GetPendingIdsAsync();
        foreach (var id in pendingIds)
            await _channel.Writer.WriteAsync(id, ct);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        for (var i = 0; i < _config.MaxWorkers; i++)
        {
            var workerCt = _cts.Token;
            _workers.Add(Task.Run(() => WorkerLoopAsync(workerCt)));
        }

        IsRunning = true;
        _logger?.LogInformation("GPU Task Queue started with {Workers} workers, {Pending} pending tasks",
            _config.MaxWorkers, pendingIds.Count);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (!IsRunning)
            return;

        _cts?.Cancel();

        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {

        }

        _workers.Clear();
        IsRunning = false;
        _logger?.LogInformation("GPU Task Queue stopped");
    }

    public async Task<long> EnqueueAsync(GpuTaskRequest request, CancellationToken ct = default)
    {
        if (!IsRunning)
            await StartAsync(ct);

        var task = new GpuTask
        {
            TaskType = request.TaskType,
            InputPath = request.InputPath,
            OutputPath = request.OutputPath,
            PayloadJson = request.PayloadJson,
            Priority = request.Priority,
            MaxRetries = request.MaxRetries,
            FileSize = request.FileSize,
            Status = (int)GpuTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            ProgressPercent = 0,
            RetryCount = 0,
        };

        var id = await _db.InsertTaskAsync(task);
        await _channel.Writer.WriteAsync(id, ct);
        _logger?.LogInformation("Enqueued GPU task {Id}: {TaskType} — {FileName}",
            id, request.TaskType, Path.GetFileName(request.InputPath));
        return id;
    }

    public async Task<string> EnqueueBatchAsync(IEnumerable<GpuTaskRequest> requests, CancellationToken ct = default)
    {
        if (!IsRunning)
            await StartAsync(ct);

        var batchId = Guid.NewGuid().ToString("N")[..12];
        var requestList = requests.ToList();

        var tasks = requestList.Select(r => new GpuTask
        {
            TaskType = r.TaskType,
            InputPath = r.InputPath,
            OutputPath = r.OutputPath,
            PayloadJson = r.PayloadJson,
            Priority = r.Priority,
            MaxRetries = r.MaxRetries,
            FileSize = r.FileSize,
            Status = (int)GpuTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            ProgressPercent = 0,
            RetryCount = 0,
            BatchId = batchId,
        }).ToList();

        await _db.InsertBatchAsync(tasks);

        var insertedTasks = await _db.GetByBatchAsync(batchId);
        foreach (var inserted in insertedTasks)
            await _channel.Writer.WriteAsync(inserted.Id, ct);

        _logger?.LogInformation("Enqueued batch {BatchId}: {Count} GPU tasks", batchId, insertedTasks.Count);
        return batchId;
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        await foreach (var signalId in _channel.Reader.ReadAllAsync(ct))
        {

            var task = await _db.GetNextPendingAsync();
            if (task == null)
                continue;

            await ProcessTaskAsync(task, ct);

            var dbStats = await _db.GetStatsAsync();
            if (dbStats.Pending == 0 && dbStats.InProgress == 0)
                QueueEmpty?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ProcessTaskAsync(GpuTask task, CancellationToken ct)
    {
        await _db.MarkInProgressAsync(task.Id);
        _currentTaskId = task.Id;
        _currentTaskCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        TaskStarted?.Invoke(this, new GpuTaskStartedEventArgs
        {
            TaskId = task.Id,
            FileName = Path.GetFileName(task.InputPath),
            BatchId = task.BatchId,
            TaskType = task.TaskType,
        });

        try
        {
            switch (task.TaskType)
            {
                case GpuTaskTypes.Gyroflow:
                    await ExecuteGyroflowAsync(task, _currentTaskCts.Token);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported GPU task type: {task.TaskType}");
            }

            await _db.MarkCompletedAsync(task.Id);

            if (task.TaskType == GpuTaskTypes.Gyroflow)
            {
                var payload = task.PayloadJson != null
                    ? JsonSerializer.Deserialize<GyroflowTaskPayload>(task.PayloadJson)
                    : null;

                if (payload?.SkipPostStabilize != true)
                {
                    try
                    {
                        await RunPostStabilizeAsync(task);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Post-stabilize failed for task {Id}: {File}", task.Id, Path.GetFileName(task.InputPath));

                    }
                }
            }

            await _processHistory.WriteEntryAsync(task.InputPath, ProcessSteps.GyroflowDone,
                new Dictionary<string, string> { ["output"] = task.OutputPath });

            TaskCompleted?.Invoke(this, new GpuTaskCompletedEventArgs
            {
                TaskId = task.Id,
                FileName = Path.GetFileName(task.InputPath),
                InputPath = task.InputPath,
                BatchId = task.BatchId,
                OutputPath = task.OutputPath,
            });
        }
        catch (OperationCanceledException)
        {
            await _db.MarkCancelledAsync(task.Id);
            _logger?.LogWarning("GPU task {Id} cancelled: {FileName}", task.Id, Path.GetFileName(task.InputPath));

            TaskFailed?.Invoke(this, new GpuTaskFailedEventArgs
            {
                TaskId = task.Id,
                FileName = Path.GetFileName(task.InputPath),
                BatchId = task.BatchId,
                Error = "Cancelled by user",
            });
        }
        catch (Exception ex)
        {
            if (task.RetryCount < task.MaxRetries)
            {
                await _db.RequeueFailedAsync(task.Id);
                _logger?.LogWarning("Task {Id} failed, requeueing (retry {RetryCount}): {Error}",
                    task.Id, task.RetryCount + 1, ex.Message);

                await _channel.Writer.WriteAsync(task.Id);
            }
            else
            {
                await _db.MarkFailedAsync(task.Id, ex.Message);

                TaskFailed?.Invoke(this, new GpuTaskFailedEventArgs
                {
                    TaskId = task.Id,
                    FileName = Path.GetFileName(task.InputPath),
                    BatchId = task.BatchId,
                    Error = ex.Message,
                });
            }
        }
        finally
        {
            _currentTaskId = 0;
            _currentTaskCts?.Dispose();
            _currentTaskCts = null;
        }

        await CheckBatchCompletionAsync(task.BatchId);
    }

    private async Task ExecuteGyroflowAsync(GpuTask task, CancellationToken ct)
    {
        var payload = task.PayloadJson != null
            ? JsonSerializer.Deserialize<GyroflowTaskPayload>(task.PayloadJson)
            : new GyroflowTaskPayload();

        payload ??= new GyroflowTaskPayload();

        var progress = new Progress<GyroflowRenderProgress>(p =>
        {

            var now = DateTime.UtcNow;
            if (now - _lastProgressUpdate >= ProgressThrottle)
            {
                _lastProgressUpdate = now;
                _ = _db.UpdateProgressAsync(task.Id, p.Percent);
            }

            TaskProgress?.Invoke(this, new GpuTaskProgressEventArgs
            {
                TaskId = task.Id,
                FileName = Path.GetFileName(task.InputPath),
                InputPath = task.InputPath,
                BatchId = task.BatchId,
                Percent = p.Percent,
                Eta = p.Eta,
                CurrentFrame = p.CurrentFrame,
                TotalFrames = p.TotalFrames,
            });
        });

        var success = await _gyroflowService.StabilizeVideoAsync(
            task.InputPath,
            task.OutputPath,
            payload.PresetPath,
            payload.GpuDevice,
            progress,
            ct);

        if (!success)
            throw new InvalidOperationException("Gyroflow stabilization failed for " + Path.GetFileName(task.InputPath));
    }

    private async Task RunPostStabilizeAsync(GpuTask task)
    {

        var payload = task.PayloadJson != null
            ? System.Text.Json.JsonSerializer.Deserialize<GyroflowTaskPayload>(task.PayloadJson)
            : null;

        var job = new VideoStabilizationJob
        {
            InputPath = task.InputPath,
            OutputPath = task.OutputPath,
            PresetPath = payload?.PresetPath,
        };

        var options = new PostProcessingOptions
        {
            Workbench = FindWorkbenchFromPath(task.InputPath),
            Source = payload?.CameraId ?? "ALL",
            Mode = "automatic",
        };

        await _postProcessingService.PostStabilizeWorkflowAsync(new List<VideoStabilizationJob> { job }, options);

        _logger?.LogInformation("Post-stabilize completed for {File}", Path.GetFileName(task.InputPath));
    }

    private static string FindWorkbenchFromPath(string filePath)
    {

        var dir = Path.GetDirectoryName(filePath);
        while (dir != null)
        {
            var folderName = Path.GetFileName(dir);
            if (folderName != null && System.Text.RegularExpressions.Regex.IsMatch(folderName, @"^\d{4}-\d{2}-\d{2}"))
            {
                return Path.GetDirectoryName(dir) ?? dir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(filePath))) ?? filePath;
    }

    private async Task CheckBatchCompletionAsync(string? batchId)
    {
        if (batchId is null)
            return;

        var batchTasks = await _db.GetByBatchAsync(batchId);

        var allTerminal = batchTasks.All(t =>
            t.Status == (int)GpuTaskStatus.Completed
            || t.Status == (int)GpuTaskStatus.Failed
            || t.Status == (int)GpuTaskStatus.Cancelled);

        if (!allTerminal)
            return;

        BatchCompleted?.Invoke(this, new GpuBatchCompletedEventArgs
        {
            BatchId = batchId,
            Total = batchTasks.Count,
            Succeeded = batchTasks.Count(t => t.Status == (int)GpuTaskStatus.Completed),
            Failed = batchTasks.Count(t => t.Status == (int)GpuTaskStatus.Failed),
        });
    }

    public async Task CancelTaskAsync(long taskId)
    {
        if (_currentTaskId == taskId && _currentTaskCts != null)
        {
            _currentTaskCts.Cancel();
        }
        else
        {
            await _db.MarkCancelledAsync(taskId);
        }
    }

    public async Task CancelAllAsync(CancellationToken ct = default)
    {
        _logger?.LogWarning("CancelAllAsync: killing current task + purging pending queue");

        _currentTaskCts?.Cancel();

        await _db.CancelAllPendingAsync();

        await StopAsync(ct);
    }

    public async Task RetryTaskAsync(long taskId)
    {
        await _db.RequeueFailedAsync(taskId);
        await _channel.Writer.WriteAsync(taskId);
    }

    public async Task RetryAllFailedAsync()
    {
        var failedIds = await _db.GetFailedTaskIdsAsync();
        foreach (var id in failedIds)
        {
            await _db.RequeueFailedAsync(id);
            await _channel.Writer.WriteAsync(id);
        }

        _logger?.LogInformation("RetryAllFailed: requeued {Count} failed tasks", failedIds.Count);
    }

    public async Task<GpuQueueStats> GetStatsAsync()
    {
        var dbStats = await _db.GetStatsAsync();
        return new GpuQueueStats
        {
            Pending = dbStats.Pending,
            InProgress = dbStats.InProgress,
            Completed = dbStats.Completed,
            Failed = dbStats.Failed,
        };
    }

    public async Task<GpuBatchProgress> GetBatchProgressAsync(string batchId)
    {
        var tasks = await _db.GetByBatchAsync(batchId);
        return new GpuBatchProgress
        {
            BatchId = batchId,
            Total = tasks.Count,
            Completed = tasks.Count(t => t.Status == (int)GpuTaskStatus.Completed),
            Failed = tasks.Count(t => t.Status == (int)GpuTaskStatus.Failed),
            Pending = tasks.Count(t => t.Status == (int)GpuTaskStatus.Pending),
            InProgress = tasks.Count(t => t.Status == (int)GpuTaskStatus.InProgress),
        };
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _currentTaskCts?.Dispose();
    }
}
