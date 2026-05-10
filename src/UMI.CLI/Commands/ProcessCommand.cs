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

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.CLI.Commands;

/// <summary>
/// Post-Processing Command für nachträgliche Feature-Anwendung.
/// </summary>
public static class ProcessCommand
{
    /// <summary>Sort-Modus für --sort Option.</summary>
    private enum SortMode
    {
        /// <summary>Sortiert nach <c>yyyy-MM-dd/{Camera}/{Type}/</c> (Standard-UMI-Layout).</summary>
        Full,
        /// <summary>Sortiert nach <c>yyyy-MM-dd/</c> und behält Camera/Type-Substruktur bei.</summary>
        Date,
    }

    public static Command Create()
    {
        var command = new Command("process", "Process already imported videos (Gyroflow, sorting)");

        var sourceOption = new Option<string>(
            "--source",
            getDefaultValue: () => "ALL",
            "Camera ID(s) comma-separated (GoPro11, MyDSLR, ALL)");

        var stabilizeOption = new Option<bool>(
            "--stabilize",
            "Stabilize videos with Gyroflow");

        var modeOption = new Option<string>(
            "--mode",
            getDefaultValue: () => "manual",
            "Mode: 'manual' (from Gyroflow folder) or 'automatic' (EIS detection)");

        var forceOption = new Option<bool>(
            "--force",
            "Also stabilize videos WITH EIS (only with automatic)");

        var dateOption = new Option<string?>(
            "--date",
            "Process specific date only (yyyy-MM-dd)");

        var pathOption = new Option<string?>(
            "--path",
            "Alternative working directory instead of workbench from config");

        var sortOption = new Option<string?>(
            "--sort",
            "Sort files by EXIF date: 'full' (yyyy-MM-dd/Camera/Type/) or 'date' (yyyy-MM-dd/ with substructure)");

        command.AddOption(sourceOption);
        command.AddOption(stabilizeOption);
        command.AddOption(modeOption);
        command.AddOption(forceOption);
        command.AddOption(dateOption);
        command.AddOption(pathOption);
        command.AddOption(sortOption);

        command.SetHandler(async (context) =>
        {
            var source   = context.ParseResult.GetValueForOption(sourceOption)!;
            var stabilize = context.ParseResult.GetValueForOption(stabilizeOption);
            var mode     = context.ParseResult.GetValueForOption(modeOption)!;
            var force    = context.ParseResult.GetValueForOption(forceOption);
            var date     = context.ParseResult.GetValueForOption(dateOption);
            var dryRun   = context.ParseResult.GetValueForOption(Program.DryRunOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var profile  = context.ParseResult.GetValueForOption(Program.ProfileOption);
            var path     = context.ParseResult.GetValueForOption(pathOption);
            var sort     = context.ParseResult.GetValueForOption(sortOption);

            await ExecuteProcessAsync(source, stabilize, mode, force, date, dryRun, configPath, profile, path, sort);
        });

        return command;
    }

    /// <summary>
    /// Öffentliche Variante für CLI-Aufrufe (baut eigenen ServiceProvider).
    /// </summary>
    public static async Task ExecuteProcessAsync(
        string source,
        bool stabilize,
        string mode,
        bool force,
        string? date,
        bool dryRun,
        string configPath,
        string? profile = null,
        string? path = null,
        string? sort = null)
    {
        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath, profile);
        await ExecuteProcessInternalAsync(serviceProvider, source, stabilize, mode, force, date, dryRun, profile, path, sort);
    }

    /// <summary>
    /// Interne Variante für ImportCommand (nutzt existierenden ServiceProvider).
    /// </summary>
    internal static async Task ExecuteProcessAsync(
        ServiceProvider serviceProvider,
        string source,
        bool stabilize,
        string mode,
        bool force,
        string? date,
        bool dryRun,
        string? profile = null,
        string? path = null,
        string? sort = null)
    {
        await ExecuteProcessInternalAsync(serviceProvider, source, stabilize, mode, force, date, dryRun, profile, path, sort);
    }

    /// <summary>
    /// Gemeinsame Logik für beide Varianten.
    /// </summary>
    private static async Task ExecuteProcessInternalAsync(
        ServiceProvider serviceProvider,
        string source,
        bool stabilize,
        string mode,
        bool force,
        string? date,
        bool dryRun,
        string? profile = null,
        string? path = null,
        string? sort = null)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Process_Banner);
        Console.WriteLine();

        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var postProcessingService = serviceProvider.GetRequiredService<IPostProcessingService>();

#pragma warning disable CS8604
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
#pragma warning restore CS8604

        var workbench = !string.IsNullOrEmpty(path)
            ? Path.GetFullPath(path)
            : config.GlobalPaths.Workbench;

        using var importLock = new ImportLock(logger);
        importLock.SetSource(source);
        importLock.SetCommand("process");

        if (!importLock.TryAcquire(workbench, out var blockedByInfo))
        {
            ConsoleHelper.WriteError(CliStrings.Common_BlockedByProcess);
            Console.WriteLine();
            ConsoleHelper.PrintLockInfo(blockedByInfo);
            Console.WriteLine();
            ConsoleHelper.WriteWarning(CliStrings.Common_WaitForProcess);
            return;
        }

        if (!string.IsNullOrEmpty(profile))
        {
            ConsoleHelper.WriteOption("Profile", profile);
        }

        SortMode? sortMode = null;
        if (!string.IsNullOrEmpty(sort))
        {
            sortMode = sort.Trim().ToLowerInvariant() switch
            {
                "full" => SortMode.Full,
                "date" => SortMode.Date,
                _ => null,
            };

            if (sortMode == null)
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.Process_UnknownSortValue, sort));
                return;
            }
        }

        if (!stabilize && sortMode == null)
        {
            ConsoleHelper.WriteError(CliStrings.Process_MinOneOption);
            return;
        }

        if (!string.IsNullOrEmpty(path))
            Console.WriteLine($"Pfad: {workbench}");
        Console.WriteLine($"Filter: {source}");
        Console.WriteLine($"Gyroflow: {(stabilize ? CliStrings.Common_Enabled : CliStrings.Common_Disabled)}");
        Console.WriteLine($"Mode: {mode}");
        Console.WriteLine($"Force: {(force ? CliStrings.Common_Yes : CliStrings.Common_No)}");
        Console.WriteLine($"Date: {date ?? CliStrings.Gps_DateAll}");
        if (sortMode.HasValue)
            Console.WriteLine($"Sort: {sortMode.Value.ToString().ToLowerInvariant()}");
        if (dryRun)
            ConsoleHelper.WriteWarning(CliStrings.Common_DryRunYes);
        else
            Console.WriteLine(CliStrings.Common_DryRunNo);
        Console.WriteLine();

        var options = new PostProcessingOptions
        {
            Workbench = workbench,
            Source = source,
            Date = date,
            Mode = mode,
            Force = force,
            DryRun = dryRun,
            GpxSource = config.GlobalPaths.GpxSource,
            StabilizationProgress = new Progress<StabilizationProgress>(p =>
            {
                Console.Write($"\r  [{p.Current}/{p.Total}] {p.CurrentFile,-40} (Jobs: {p.ActiveJobs})          ");
            })
        };

        if (stabilize)
        {
            var videos = await postProcessingService.FindVideosAsync(options, ct);

            if (videos.Count == 0)
            {
                ConsoleHelper.WriteWarning(CliStrings.Process_NoGyroflowVideos);
            }
            else
            {
                Console.WriteLine($"Gefunden: {videos.Count} Videos");
                Console.WriteLine();

                if (dryRun)
                {
                    Console.WriteLine(string.Format(CliStrings.Process_DryRunStabilize, videos.Count, mode));
                }
                else
                {

                    var gpuQueue = serviceProvider.GetRequiredService<IGpuTaskQueue>();
                    await gpuQueue.StartAsync(ct);

                    var requests = videos.Select(v => new GpuTaskRequest
                    {
                        TaskType = GpuTaskTypes.Gyroflow,
                        InputPath = v.FullName,
                        OutputPath = FolderNameConstants.CalculateStabilizedOutputPath(v.FullName),
                        FileSize = v.Length,
                    }).ToList();

                    var batchId = await gpuQueue.EnqueueBatchAsync(requests, ct);

                    ConsoleHelper.WriteSeparator();
                    Console.WriteLine(string.Format(CliStrings.Process_GpuQueueEnqueued, requests.Count, batchId));

                    gpuQueue.TaskProgress += (s, e) =>
                    {
                        if (e.BatchId != batchId) return;
                        Console.Write($"\r  {e.FileName,-40} {e.Percent:F0}% ETA {e.Eta}          ");
                    };

                    gpuQueue.TaskCompleted += (s, e) =>
                    {
                        if (e.BatchId != batchId) return;
                        Console.WriteLine($"\r  ✓ {e.FileName,-40}                              ");
                    };

                    gpuQueue.TaskFailed += (s, e) =>
                    {
                        if (e.BatchId != batchId) return;
                        Console.WriteLine($"\r  ✗ {e.FileName,-40} Error: {e.Error}");
                    };

                    var batchDone = new TaskCompletionSource<bool>();
                    gpuQueue.BatchCompleted += (s, e) =>
                    {
                        if (e.BatchId != batchId) return;
                        batchDone.TrySetResult(true);
                    };

                    using var reg = ct.Register(() => batchDone.TrySetCanceled());
                    await batchDone.Task;

                    await gpuQueue.StopAsync(ct);

                    var stats = await gpuQueue.GetBatchProgressAsync(batchId);
                    Console.WriteLine();
                    ConsoleHelper.WriteSuccess(string.Format(CliStrings.Process_GpuDone, stats.Completed, stats.Failed));
                }
            }
        }

        if (sortMode.HasValue)
        {
            Console.WriteLine();
            ConsoleHelper.WriteSeparator();
            var sortService = serviceProvider.GetRequiredService<IFolderSortService>();
            await SortFilesAsync(workbench, sortMode.Value, config, sortService, dryRun, ct);
        }

        Console.WriteLine();
        ConsoleHelper.WriteSuccess(CliStrings.Process_Completed);
    }

    /// <summary>
    /// Sortiert alle Mediendateien im Arbeitsordner nach EXIF-Datum.
    /// Delegiert an <see cref="IFolderSortService"/> (SSOT — wird auch vom GUI-Process-Tab genutzt).
    /// <para>
    /// <c>full</c>: <c>{workbench}/yyyy-MM-dd/{Camera}/{Type}/datei.ext</c><br/>
    /// <c>date</c>: <c>{workbench}/yyyy-MM-dd/{relative Substruktur}/datei.ext</c>
    /// </para>
    /// </summary>
    private static async Task SortFilesAsync(
        string workbench,
        SortMode sortMode,
        UmiConfig config,
        IFolderSortService sortService,
        bool dryRun,
        CancellationToken ct)
    {
        Console.WriteLine(string.Format("  {0}...", CliStrings.Process_SortingFiles));

        var lastTotal = 0;
        var progress = new Progress<FolderSortProgress>(p =>
        {
            lastTotal = p.Total;
            if (p.Total > 0)
                Console.Write($"\r  [{p.Phase}] {p.Current}/{p.Total}: {p.CurrentFile,-40}");
        });

        var request = new FolderSortRequest(
            Workbench:    workbench,
            Mode:         sortMode == SortMode.Full ? FolderSortMode.Full : FolderSortMode.Date,
            Config:       config,
            // CLI keeps the historic semantics: --sort doesn't trigger burst
            // detection. The GUI exposes that via a toggle on the action card.
            DetectBursts: false,
            DryRun:       dryRun);

        var result = await sortService.SortAsync(request, progress, ct);

        Console.Write("\r" + new string(' ', 80) + "\r");

        if (lastTotal == 0 && result.Moved == 0 && result.Skipped == 0)
        {
            ConsoleHelper.WriteInfo(CliStrings.Process_NoMediaFiles);
            return;
        }

        Console.WriteLine(string.Format(
            CliStrings.Process_SortResult,
            result.Moved,
            dryRun ? CliStrings.Process_DryRunWouldMove : CliStrings.Process_Sorted,
            result.DateFolders));

        if (result.Skipped > 0)
            Console.WriteLine(string.Format(CliStrings.Process_SkippedAtTarget, result.Skipped));

        if (result.Errors > 0)
        {
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Process_MoveErrors, result.Errors));
            foreach (var w in result.Warnings.Take(20))
                ConsoleHelper.WriteWarning("  " + w);
        }
    }
}
