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
using System.Globalization;
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
            var exifTool = serviceProvider.GetRequiredService<IExifToolWrapper>();
            await SortFilesAsync(workbench, sortMode.Value, config, exifTool, dryRun, ct);
        }

        Console.WriteLine();
        ConsoleHelper.WriteSuccess(CliStrings.Process_Completed);
    }

    /// <summary>
    /// Sortiert alle Mediendateien im Arbeitsordner nach EXIF-Datum.
    /// <para>
    /// <c>full</c>: <c>{workbench}/yyyy-MM-dd/{Camera}/{Type}/datei.ext</c><br/>
    /// <c>date</c>: <c>{workbench}/yyyy-MM-dd/{relative Substruktur}/datei.ext</c>
    /// </para>
    /// </summary>
    private static async Task SortFilesAsync(
        string workbench,
        SortMode sortMode,
        UmiConfig config,
        IExifToolWrapper exifTool,
        bool dryRun,
        CancellationToken ct)
    {
        Console.WriteLine(string.Format("  {0}...", CliStrings.Process_SortingFiles));

        var knownExtensions = config.Cameras.Values
            .SelectMany(c => c.FileTypes.Video.Concat(c.FileTypes.Photo))
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        if (knownExtensions.Count == 0)
            knownExtensions = FileExtensions.Videos
                .Concat(FileExtensions.Photos)
                .Select(e => e.ToLowerInvariant())
                .ToHashSet();

        var allFiles = Directory.EnumerateFiles(workbench, "*.*", SearchOption.AllDirectories)
            .Where(f => knownExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f => Path.GetFullPath(f))
            .ToList();

        if (allFiles.Count == 0)
        {
            ConsoleHelper.WriteInfo(CliStrings.Process_NoMediaFiles);
            return;
        }

        Console.WriteLine($"  {allFiles.Count} Datei(en) gefunden");
        Console.WriteLine();

        var movedCount = 0;
        var skippedCount = 0;
        var errorCount = 0;
        var dateFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sourceDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < allFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = allFiles[i];
            var fileName = Path.GetFileName(filePath);
            var fileDir = Path.GetDirectoryName(filePath)!;

            DateTime fileDate;
            try
            {
                var metadata = await exifTool.ReadMetadataAsync(filePath, ["CreateDate"], ct);

                if (metadata.TryGetValue("CreateDate", out var rawDate) && rawDate is string dateStr
                    && DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    fileDate = parsedDate;
                }
                else
                {

                    fileDate = File.GetLastWriteTime(filePath);
                }
            }
            catch
            {
                fileDate = File.GetLastWriteTime(filePath);
            }

            var dateStr2 = fileDate.ToString(DateFormatConstants.FolderFormat, CultureInfo.InvariantCulture);

            string targetDir;
            if (sortMode == SortMode.Full)
            {

                var cameraId = ExtractCameraIdFromPath(filePath, workbench, config);
                var typeFolder = DetermineTypeFolder(fileName, config);
                var procFolderName = config.Cameras.TryGetValue(cameraId, out var procCam)
                    ? procCam.FolderName ?? cameraId
                    : cameraId;

                targetDir = Path.Combine(workbench, dateStr2, procFolderName, typeFolder);
            }
            else
            {

                var relativeDirToWorkbench = Path.GetRelativePath(workbench, fileDir);

                if (relativeDirToWorkbench == ".")
                {
                    targetDir = Path.Combine(workbench, dateStr2);
                }
                else
                {
                    targetDir = Path.Combine(workbench, dateStr2, relativeDirToWorkbench);
                }
            }

            var targetPath = Path.Combine(targetDir, fileName);

            if (string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                skippedCount++;
                continue;
            }

            Console.Write($"\r  {i + 1}/{allFiles.Count}: {fileName,-40} → {dateStr2}/");

            if (File.Exists(targetPath))
            {
                skippedCount++;
                continue;
            }

            dateFolders.Add(Path.Combine(workbench, dateStr2));
            sourceDirs.Add(fileDir);

            if (!dryRun)
            {
                try
                {
                    Directory.CreateDirectory(targetDir);
                    File.Move(filePath, targetPath);
                    movedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine();
                    ConsoleHelper.WriteWarning(string.Format(CliStrings.Process_MoveError, fileName, ex.Message));
                }
            }
            else
            {
                movedCount++;
            }
        }

        Console.Write("\r" + new string(' ', 80) + "\r");

        if (!dryRun)
        {
            CleanEmptyDirectories(workbench, sourceDirs);
        }

        Console.WriteLine(string.Format(CliStrings.Process_SortResult, movedCount, dryRun ? CliStrings.Process_DryRunWouldMove : CliStrings.Process_Sorted, dateFolders.Count));
        if (skippedCount > 0)
            Console.WriteLine(string.Format(CliStrings.Process_SkippedAtTarget, skippedCount));
        if (errorCount > 0)
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Process_MoveErrors, errorCount));
    }

    /// <summary>
    /// Extrahiert Camera-ID aus dem Ordnerpfad einer Datei.
    /// Vergleicht Ordnernamen gegen <c>config.Cameras.Keys</c>.
    /// Fallback: <c>_unsorted</c>.
    /// </summary>
    private static string ExtractCameraIdFromPath(string filePath, string workbench, UmiConfig config)
    {

        var relativePath = Path.GetRelativePath(workbench, filePath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        var cameraKeys = config.Cameras.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in segments)
        {
            if (cameraKeys.Contains(segment))
                return config.Cameras.Keys.First(k => k.Equals(segment, StringComparison.OrdinalIgnoreCase));
        }

        return "_unsorted";
    }

    /// <summary>
    /// Bestimmt Video oder Photo Typordner anhand der Dateiextension.
    /// </summary>
    private static string DetermineTypeFolder(string fileName, UmiConfig config)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        var isVideo = config.Cameras.Values
            .Any(c => c.FileTypes.Video.Any(v => v.Equals(ext, StringComparison.OrdinalIgnoreCase)));

        if (isVideo)
            return FolderNameConstants.Video;

        var isPhoto = config.Cameras.Values
            .Any(c => c.FileTypes.Photo.Any(p => p.Equals(ext, StringComparison.OrdinalIgnoreCase)));

        if (isPhoto)
            return FolderNameConstants.Photo;

        return ext is ".mp4" or ".mov" or ".avi" or ".mkv" ? FolderNameConstants.Video : FolderNameConstants.Photo;
    }

    /// <summary>
    /// Delegiert an <see cref="DirectoryCleanupHelper.CleanEmptyDirectories"/> (Core, SSOT).
    /// Bleibt hier als private Methode damit der Aufruf-Kontext (workbench/sourceDirs) lokal bleibt.
    /// </summary>
    private static void CleanEmptyDirectories(string workbench, IEnumerable<string> sourceDirs)
        => DirectoryCleanupHelper.CleanEmptyDirectories(workbench, sourceDirs);
}
